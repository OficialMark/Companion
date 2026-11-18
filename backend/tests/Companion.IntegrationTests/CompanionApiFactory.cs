using Companion.Application.Creatures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace Companion.IntegrationTests;

public sealed class CompanionApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    /// <summary>Stands in for pocketdm.com.br so tests never touch the network.</summary>
    public FakeCreatureImageSource ImageSource { get; } = new();

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
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICreatureImageSource>();
            services.AddSingleton<ICreatureImageSource>(ImageSource);
        });
    }
}

public sealed class FakeCreatureImageSource : ICreatureImageSource
{
    public static readonly byte[] Webp = [.. "RIFF"u8, 0x10, 0, 0, 0, .. "WEBP"u8, .. "VP8 "u8, 1, 2, 3, 4];
    private readonly List<string> _requests = [];
    public bool Available { get; set; } = true;
    public IReadOnlyList<string> Requests { get { lock (_requests) return _requests.ToArray(); } }

    public Task<(byte[] Content, string MediaType)?> FetchAsync(string url, CancellationToken cancellationToken)
    {
        lock (_requests) _requests.Add(url);
        return Task.FromResult<(byte[] Content, string MediaType)?>(Available ? (Webp, "image/webp") : null);
    }
}

[CollectionDefinition(Name)]
public sealed class CompanionCollection : ICollectionFixture<CompanionApiFactory>
{
    public const string Name = "Companion API";
}

internal static class ServiceCollectionTestExtensions
{
    public static void RemoveAll<T>(this IServiceCollection services)
    {
        foreach (var descriptor in services.Where(x => x.ServiceType == typeof(T)).ToArray()) services.Remove(descriptor);
    }
}
