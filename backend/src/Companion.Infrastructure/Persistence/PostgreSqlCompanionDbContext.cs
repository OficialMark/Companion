using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Companion.Infrastructure.Persistence;

public sealed class PostgreSqlCompanionDbContext(DbContextOptions<PostgreSqlCompanionDbContext> options)
    : CompanionDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Keep the byte[] revision used by the public API. The initial migration
        // installs triggers that advance it on inserts and updates.
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                var columnType = property.FindAnnotation(RelationalAnnotationNames.ColumnType)?.Value as string;
                if (columnType?.StartsWith("varbinary", StringComparison.Ordinal) == true)
                    property.SetColumnType("bytea");
                else if (columnType == "nvarchar(max)")
                    property.SetColumnType("text");

                if (property.GetCollation() == "Latin1_General_100_BIN2")
                    property.SetCollation("C");
            }

            foreach (var index in entity.GetIndexes())
                if (index.GetFilter() is { } filter)
                    index.SetFilter(QuoteIdentifiers(filter));

            foreach (var constraint in entity.GetCheckConstraints().ToArray())
            {
                entity.RemoveCheckConstraint(constraint.ModelName);
                entity.AddCheckConstraint(constraint.ModelName, QuoteIdentifiers(constraint.Sql));
            }
        }
    }

    private static string QuoteIdentifiers(string sql) => Regex.Replace(sql, @"\[(\w+)\]", "\"$1\"");
}

public sealed class PostgreSqlCompanionDbContextFactory
    : IDesignTimeDbContextFactory<PostgreSqlCompanionDbContext>
{
    public PostgreSqlCompanionDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("COMPANION_POSTGRES_DESIGN_CONNECTION")
            ?? "Host=localhost;Database=Companion;Username=companion";
        return new PostgreSqlCompanionDbContext(
            new DbContextOptionsBuilder<PostgreSqlCompanionDbContext>().UseNpgsql(connection).Options);
    }
}
