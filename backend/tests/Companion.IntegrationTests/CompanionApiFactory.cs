using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.MsSql;

namespace Companion.IntegrationTests;

public sealed class CompanionApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public Task InitializeAsync() => _sqlServer.StartAsync();
    public new Task DisposeAsync() => _sqlServer.DisposeAsync().AsTask();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Companion", _sqlServer.GetConnectionString());
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Companion"] = _sqlServer.GetConnectionString(),
                ["Admin:AccessKey"] = "integration-admin-secret"
            }));
    }
}

[CollectionDefinition(Name)]
public sealed class CompanionCollection : ICollectionFixture<CompanionApiFactory>
{
    public const string Name = "Companion API";
}
