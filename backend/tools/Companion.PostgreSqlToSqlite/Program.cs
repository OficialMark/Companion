using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Companion.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

if (args is not [var destination] || Environment.GetEnvironmentVariable("COMPANION_POSTGRES_SOURCE") is not { Length: > 0 } source)
    throw new ArgumentException("Defina COMPANION_POSTGRES_SOURCE e informe o caminho do novo arquivo SQLite.");

var output = Path.GetFullPath(destination);
if (!Directory.Exists(Path.GetDirectoryName(output)))
    throw new DirectoryNotFoundException("Crie a pasta de destino antes de rodar a conversão.");
if (File.Exists(output))
    throw new IOException("O arquivo de destino já existe. Escolha um novo caminho.");

// Stage on the same filesystem so only a complete, checked database appears at the final path.
var staged = output + "." + Guid.NewGuid().ToString("N") + ".partial";
try
{
    await ConvertAsync(source, staged);
    if (!OperatingSystem.IsWindows())
        File.SetUnixFileMode(staged, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    File.Move(staged, output);
    Console.WriteLine($"Arquivo SQLite pronto: {output}");
}
finally
{
    File.Delete(staged);
    File.Delete(staged + "-wal");
    File.Delete(staged + "-shm");
    File.Delete(staged + "-journal");
}

static async Task ConvertAsync(string sourceString, string output)
{
    await using var postgres = new NpgsqlConnection(sourceString);
    await postgres.OpenAsync();
    await using var readTransaction = await postgres.BeginTransactionAsync(IsolationLevel.RepeatableRead);
    await using (var command = new NpgsqlCommand("SET TRANSACTION READ ONLY", postgres, readTransaction))
        await command.ExecuteNonQueryAsync();

    var tables = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    await using (var command = new NpgsqlCommand("""
        SELECT c.table_name, c.column_name
        FROM information_schema.columns c
        JOIN information_schema.tables t USING (table_schema, table_name)
        WHERE c.table_schema = 'public' AND t.table_type = 'BASE TABLE'
          AND c.table_name <> '__EFMigrationsHistory'
        ORDER BY c.table_name, c.ordinal_position
        """, postgres, readTransaction))
    await using (var reader = await command.ExecuteReaderAsync())
        while (await reader.ReadAsync())
        {
            var table = reader.GetString(0);
            if (!tables.TryGetValue(table, out var columns))
                tables.Add(table, columns = []);
            columns.Add(reader.GetString(1));
        }

    await using var sqlite = new SqliteConnection(new SqliteConnectionStringBuilder
    {
        DataSource = output,
        ForeignKeys = true,
        Pooling = false
    }.ToString());
    await sqlite.OpenAsync();
    await using (var command = sqlite.CreateCommand())
    {
        command.CommandText = "PRAGMA journal_mode=WAL";
        await command.ExecuteScalarAsync();
    }
    await using (var context = new SqliteCompanionDbContext(
        new DbContextOptionsBuilder<SqliteCompanionDbContext>().UseSqlite(sqlite).Options))
    {
        await context.Database.MigrateAsync();
    }

    await using (var command = sqlite.CreateCommand())
    {
        command.CommandText = """
            SELECT name FROM sqlite_schema WHERE type = 'table'
              AND name NOT IN ('__EFMigrationsHistory', '__EFMigrationsLock', 'sqlite_sequence')
            """;
        await using var reader = await command.ExecuteReaderAsync();
        var targetTables = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync())
            targetTables.Add(reader.GetString(0));
        if (!targetTables.SetEquals(tables.Keys))
            throw new InvalidOperationException("O esquema PostgreSQL contém um conjunto de tabelas diferente do SQLite.");
    }

    await using var writeTransaction = (SqliteTransaction)await sqlite.BeginTransactionAsync();
    await using (var command = sqlite.CreateCommand())
    {
        // SQLite checks every foreign key at commit, including self-references and
        // rows whose parent table is copied later in this same transaction.
        command.Transaction = writeTransaction;
        command.CommandText = "PRAGMA defer_foreign_keys=ON";
        await command.ExecuteNonQueryAsync();
    }

    long total = 0;
    foreach (var (table, columns) in tables)
    {
        await using (var command = sqlite.CreateCommand())
        {
            command.Transaction = writeTransaction;
            command.CommandText = $"PRAGMA table_info({Quote(table)})";
            await using var reader = await command.ExecuteReaderAsync();
            var targetColumns = new HashSet<string>(StringComparer.Ordinal);
            while (await reader.ReadAsync())
                targetColumns.Add(reader.GetString(1));
            if (!targetColumns.SetEquals(columns))
                throw new InvalidOperationException($"Colunas diferentes entre os bancos na tabela {table}.");
        }

        var tableName = Quote(table);
        var columnNames = string.Join(", ", columns.Select(Quote));
        var parameters = Enumerable.Range(0, columns.Count).Select(i => $"$p{i}").ToArray();
        await using var insert = sqlite.CreateCommand();
        insert.Transaction = writeTransaction;
        insert.CommandText = $"INSERT INTO {tableName} ({columnNames}) VALUES ({string.Join(", ", parameters)})";
        foreach (var parameter in parameters)
            insert.Parameters.AddWithValue(parameter, DBNull.Value);
        insert.Prepare();

        var sourceHashes = new List<string>();
        var row = new object?[columns.Count];
        await using var select = new NpgsqlCommand($"SELECT {columnNames} FROM public.{tableName}", postgres, readTransaction);
        select.CommandTimeout = 300;
        await using (var reader = await select.ExecuteReaderAsync(CommandBehavior.SequentialAccess))
            while (await reader.ReadAsync())
            {
                for (var i = 0; i < row.Length; i++)
                {
                    row[i] = Normalize(reader.GetValue(i));
                    insert.Parameters[i].Value = row[i] ?? DBNull.Value;
                }
                sourceHashes.Add(Hash(row));
                await insert.ExecuteNonQueryAsync();
            }

        var targetHashes = new List<string>();
        await using (var readback = sqlite.CreateCommand())
        {
            readback.Transaction = writeTransaction;
            readback.CommandText = $"SELECT {columnNames} FROM {tableName}";
            await using var reader = await readback.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                for (var i = 0; i < row.Length; i++)
                    row[i] = Normalize(reader.GetValue(i));
                targetHashes.Add(Hash(row));
            }
        }

        sourceHashes.Sort(StringComparer.Ordinal);
        targetHashes.Sort(StringComparer.Ordinal);
        if (!sourceHashes.SequenceEqual(targetHashes, StringComparer.Ordinal))
            throw new InvalidOperationException($"O conteúdo restaurado diverge na tabela {table}.");
        total += sourceHashes.Count;
        Console.WriteLine($"{table}: {sourceHashes.Count}");
    }

    await writeTransaction.CommitAsync();
    await readTransaction.CommitAsync();
    await using (var command = sqlite.CreateCommand())
    {
        command.CommandText = "PRAGMA integrity_check";
        if (!Equals(await command.ExecuteScalarAsync(), "ok"))
            throw new InvalidOperationException("O SQLite não passou na verificação de integridade.");
        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
        await command.ExecuteNonQueryAsync();
    }
    Console.WriteLine($"Conversão conferida: {total} linhas em {tables.Count} tabelas.");
}

static object? Normalize(object value) => value switch
{
    DBNull => null,
    DateTimeOffset timestamp => timestamp.UtcTicks,
    DateTime { Kind: DateTimeKind.Utc } timestamp => timestamp.Ticks,
    Guid id => id.ToString().ToUpperInvariant(),
    bool flag => flag ? 1L : 0L,
    short number => (long)number,
    int number => (long)number,
    float number => (double)number,
    decimal number => number.ToString(CultureInfo.InvariantCulture),
    string or long or double or byte[] => value,
    _ => throw new NotSupportedException($"Tipo sem conversão SQLite: {value.GetType().Name}.")
};

static string Hash(object?[] row) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(row)));
static string Quote(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";
