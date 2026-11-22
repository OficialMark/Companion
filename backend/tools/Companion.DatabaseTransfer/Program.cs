using System.Data;
using System.Data.Common;
using Companion.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var sourceConnection = Required("COMPANION_SQLSERVER_SOURCE");
var targetConnection = Required("COMPANION_POSTGRES_TARGET");
await using var source = new SqlConnection(sourceConnection);
await source.OpenAsync();
await using var sourceTransaction = (SqlTransaction)await source.BeginTransactionAsync(IsolationLevel.Serializable);

await using var target = new NpgsqlConnection(targetConnection);
await target.OpenAsync();
await using (var db = new PostgreSqlCompanionDbContext(
    new DbContextOptionsBuilder<PostgreSqlCompanionDbContext>().UseNpgsql(target).Options))
{
    await db.Database.MigrateAsync();
}
await using var targetTransaction = await target.BeginTransactionAsync();

var sourceColumns = await ReadColumns(new SqlCommand("""
    SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME <> '__EFMigrationsHistory'
    ORDER BY ORDINAL_POSITION
    """, source, sourceTransaction));
var targetColumns = await ReadColumns(new NpgsqlCommand("""
    SELECT table_name, column_name, udt_name
    FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name <> '__EFMigrationsHistory'
    ORDER BY ordinal_position
    """, target, targetTransaction));

RequireSameNames("tabelas", sourceColumns.Keys, targetColumns.Keys);
foreach (var (table, columns) in sourceColumns)
    RequireSameNames($"colunas de {table}", columns.Select(c => c.Name), targetColumns[table].Select(c => c.Name));

var dependencies = targetColumns.Keys.ToDictionary(name => name, _ => new HashSet<string>());
await using (var command = new NpgsqlCommand("""
    SELECT child.relname, parent.relname
    FROM pg_constraint fk
    JOIN pg_class child ON child.oid = fk.conrelid
    JOIN pg_class parent ON parent.oid = fk.confrelid
    WHERE fk.contype = 'f'
      AND child.relnamespace = 'public'::regnamespace
      AND parent.relnamespace = 'public'::regnamespace
      AND child.oid <> parent.oid
    """, target, targetTransaction))
await using (var reader = await command.ExecuteReaderAsync())
    while (await reader.ReadAsync())
        dependencies[reader.GetString(0)].Add(reader.GetString(1));

var tableNames = targetColumns.Keys.Order(StringComparer.Ordinal).Select(Table);
await using (var command = new NpgsqlCommand(
    $"LOCK TABLE {string.Join(", ", tableNames)} IN ACCESS EXCLUSIVE MODE", target, targetTransaction))
    await command.ExecuteNonQueryAsync();

foreach (var table in targetColumns.Keys)
    if (await CountRows(table) != 0)
        throw new InvalidOperationException($"O destino contém dados em {table}. Use um banco PostgreSQL vazio.");

var copied = new HashSet<string>();
long total = 0;
while (copied.Count < dependencies.Count)
{
    var ready = dependencies.Where(entry => !copied.Contains(entry.Key) && entry.Value.IsSubsetOf(copied)).ToArray();
    if (ready.Length == 0)
        throw new InvalidOperationException("Há dependências circulares entre tabelas.");

    foreach (var (table, _) in ready)
    {
        var targetTypes = targetColumns[table].ToDictionary(column => column.Name, column => column.Type);
        // PostgreSQL generates replacement revision tokens when rows are inserted.
        var columns = sourceColumns[table].Where(column => column.Type != "timestamp").ToArray();
        var sqlNames = string.Join(", ", columns.Select(column => Bracket(column.Name)));
        var pgNames = string.Join(", ", columns.Select(column => Quote(column.Name)));
        await using var read = new SqlCommand($"SELECT {sqlNames} FROM dbo.{Bracket(table)}", source, sourceTransaction);
        read.CommandTimeout = 300;
        await using var reader = await read.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
        ulong readCount = 0;
        await using (var writer = await target.BeginBinaryImportAsync($"COPY {Table(table)} ({pgNames}) FROM STDIN (FORMAT BINARY)"))
        {
            writer.Timeout = TimeSpan.FromMinutes(5);
            while (await reader.ReadAsync())
            {
                await writer.StartRowAsync();
                for (var i = 0; i < columns.Length; i++)
                {
                    if (await reader.IsDBNullAsync(i))
                        await writer.WriteNullAsync();
                    else
                    {
                        var type = targetTypes[columns[i].Name];
                        var value = reader.GetValue(i) switch
                        {
                            DateTimeOffset date => (object)date.ToUniversalTime(),
                            byte number when type == "int2" => (short)number,
                            var original => original
                        };
                        await writer.WriteAsync(value, type);
                    }
                }
                readCount++;
            }
            if (await writer.CompleteAsync() != readCount)
                throw new InvalidOperationException($"COPY incompleto em {table}.");
        }
        if ((ulong)await CountRows(table) != readCount)
            throw new InvalidOperationException($"Contagem divergente em {table}.");
        copied.Add(table);
        total += (long)readCount;
        Console.WriteLine($"{table}: {readCount}");
    }
}

await sourceTransaction.CommitAsync();
await targetTransaction.CommitAsync();
Console.WriteLine($"Importação confirmada: {total} linhas em {copied.Count} tabelas.");

async Task<long> CountRows(string table)
{
    await using var command = new NpgsqlCommand($"SELECT count(*) FROM {Table(table)}", target, targetTransaction);
    return (long)(await command.ExecuteScalarAsync())!;
}

static async Task<Dictionary<string, List<(string Name, string Type)>>> ReadColumns(DbCommand command)
{
    await using var ownedCommand = command;
    await using var reader = await command.ExecuteReaderAsync();
    var tables = new Dictionary<string, List<(string, string)>>(StringComparer.Ordinal);
    while (await reader.ReadAsync())
    {
        var table = reader.GetString(0);
        if (!tables.TryGetValue(table, out var columns))
            tables.Add(table, columns = []);
        columns.Add((reader.GetString(1), reader.GetString(2)));
    }
    return tables;
}

static void RequireSameNames(string description, IEnumerable<string> source, IEnumerable<string> target)
{
    var left = source.ToHashSet(StringComparer.Ordinal);
    var right = target.ToHashSet(StringComparer.Ordinal);
    if (!left.SetEquals(right))
        throw new InvalidOperationException(
            $"Diferença em {description}. Só no SQL Server: {string.Join(", ", left.Except(right))}. " +
            $"Só no PostgreSQL: {string.Join(", ", right.Except(left))}.");
}

static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
    ? value
    : throw new InvalidOperationException($"Configure {name}.");

static string Bracket(string name) => "[" + name.Replace("]", "]]") + "]";
static string Quote(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";
static string Table(string name) => "public." + Quote(name);
