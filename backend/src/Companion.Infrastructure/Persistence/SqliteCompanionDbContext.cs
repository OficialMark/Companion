using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Companion.Infrastructure.Persistence;

public sealed class SqliteCompanionDbContext(DbContextOptions<SqliteCompanionDbContext> options)
    : CompanionDbContext(options)
{
    private static readonly ValueConverter<DateTimeOffset, long> UtcTicks = new(
        date => date.UtcTicks, ticks => new DateTimeOffset(ticks, TimeSpan.Zero));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        foreach (var property in entity.GetProperties())
        {
            var columnType = property.FindAnnotation(RelationalAnnotationNames.ColumnType)?.Value as string;
            if (columnType?.StartsWith("varbinary", StringComparison.Ordinal) == true)
                property.SetColumnType("BLOB");
            else if (columnType == "nvarchar(max)")
                property.SetColumnType("TEXT");

            if (property.GetCollation() == "Latin1_General_100_BIN2")
                property.SetCollation("BINARY");

            var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            if (clrType == typeof(DateTimeOffset))
                property.SetValueConverter(UtcTicks);

            // Keep the existing byte[] revision in the HTTP contract. SQLite needs
            // the application to write the new revision as part of the same UPDATE.
            if (property.ClrType == typeof(byte[]) &&
                property.IsConcurrencyToken && property.ValueGenerated == ValueGenerated.OnAddOrUpdate)
                property.ValueGenerated = ValueGenerated.Never;
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AdvanceRevisions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AdvanceRevisions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void AdvanceRevisions()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
                continue;

            foreach (var property in entry.Properties)
                if (property.Metadata.IsConcurrencyToken && property.Metadata.ClrType == typeof(byte[]))
                    property.CurrentValue = RandomNumberGenerator.GetBytes(16);
        }
    }
}

public sealed class SqliteCompanionDbContextFactory : IDesignTimeDbContextFactory<SqliteCompanionDbContext>
{
    public SqliteCompanionDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("COMPANION_SQLITE_DESIGN_CONNECTION")
            ?? "Data Source=companion.db;Foreign Keys=True";
        return new SqliteCompanionDbContext(
            new DbContextOptionsBuilder<SqliteCompanionDbContext>().UseSqlite(connection).Options);
    }
}
