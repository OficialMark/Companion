using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Companion.Infrastructure.Persistence;

public sealed class CompanionDbContextFactory : IDesignTimeDbContextFactory<CompanionDbContext>
{
    public CompanionDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("COMPANION_DESIGN_CONNECTION")
            ?? "Server=localhost,1433;Database=Companion;Integrated Security=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<CompanionDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new CompanionDbContext(options);
    }
}
