using Companion.Api.Realtime;
using Companion.Application.Contracts;
using Companion.Application.Creatures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace Companion.IntegrationTests;

public sealed class CompanionApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly PostgreSqlContainer? _postgres =
        Environment.GetEnvironmentVariable("COMPANION_TEST_PROVIDER") == "PostgreSql"
            ? new PostgreSqlBuilder("postgres:17-alpine").Build()
            : null;
    private readonly string _sqlitePath = Path.Combine(Path.GetTempPath(), $"companion-integration-{Guid.NewGuid():N}.db");

    /// <summary>Stands in for pocketdm.com.br so tests never touch the network.</summary>
    public FakeCreatureImageSource ImageSource { get; } = new();

    /// <summary>Every realtime notification the API emitted (the real SignalR notifier still runs).</summary>
    public NotificationLog Notifications { get; } = new();

    /// <summary>Servidor SQL descartável do Testcontainers (testes que precisam de outro banco no mesmo servidor).</summary>
    public string ConnectionString => _sqlServer.GetConnectionString();

    /// <summary>Dados roteirizados para testes de rolagem (spec 011); sem roteiro, rola aleatório como antes.</summary>
    public ScriptedDiceRandomizer Dice { get; } = new();

    public Task InitializeAsync() => Task.WhenAll(_sqlServer.StartAsync(), _postgres?.StartAsync() ?? Task.CompletedTask);

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await Task.WhenAll(_sqlServer.DisposeAsync().AsTask(), _postgres?.DisposeAsync().AsTask() ?? Task.CompletedTask);
        File.Delete(_sqlitePath);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var provider = _postgres is null ? "Sqlite" : "PostgreSql";
        var connection = _postgres?.GetConnectionString() ?? $"Data Source={_sqlitePath};Foreign Keys=True;Pooling=False";
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Provider", provider);
        builder.UseSetting("ConnectionStrings:Companion", connection);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Database:Provider"] = provider,
                ["ConnectionStrings:Companion"] = connection,
                ["Admin:AccessKey"] = "integration-admin-secret"
            }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICreatureImageSource>();
            services.AddSingleton<ICreatureImageSource>(ImageSource);
            services.RemoveAll<IDiceRandomizer>();
            services.AddSingleton<IDiceRandomizer>(Dice);
            services.RemoveAll<ICampaignNotifier>();
            services.AddScoped<ICampaignNotifier>(provider => new RecordingCampaignNotifier(
                ActivatorUtilities.CreateInstance<SignalRCampaignNotifier>(provider), Notifications));
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

public sealed class ScriptedDiceRandomizer : IDiceRandomizer
{
    private readonly Queue<int> _script = new();

    /// <summary>Os próximos dados saem nesta ordem; depois deles, volta ao aleatório.</summary>
    public void Enqueue(params int[] values) { lock (_script) foreach (var value in values) _script.Enqueue(value); }

    public int Roll(int faces)
    {
        lock (_script)
        {
            if (_script.TryDequeue(out var value)) return value;
        }
        return Random.Shared.Next(1, faces + 1);
    }
}

public sealed record NotificationEntry(Guid CampaignId, string Type, Guid? EncounterId);

public sealed class NotificationLog
{
    private readonly List<NotificationEntry> _entries = [];
    public void Add(NotificationEntry entry) { lock (_entries) _entries.Add(entry); }
    public IReadOnlyList<NotificationEntry> For(Guid campaignId) { lock (_entries) return _entries.Where(x => x.CampaignId == campaignId).ToArray(); }
}

public sealed class RecordingCampaignNotifier(ICampaignNotifier inner, NotificationLog log) : ICampaignNotifier
{
    public Task NotifyAsync(Guid campaignId, string type, object payload, CancellationToken cancellationToken = default)
    { log.Add(new(campaignId, type, null)); return inner.NotifyAsync(campaignId, type, payload, cancellationToken); }
    public Task ParticipantsChangedAsync(Guid campaignId, CancellationToken cancellationToken = default)
    { log.Add(new(campaignId, "ParticipantsChanged", null)); return inner.ParticipantsChangedAsync(campaignId, cancellationToken); }
    public Task EncounterInvalidatedAsync(Guid campaignId, Guid encounterId, CancellationToken cancellationToken = default)
    { log.Add(new(campaignId, "EncounterInvalidated", encounterId)); return inner.EncounterInvalidatedAsync(campaignId, encounterId, cancellationToken); }
    public Task BattlemapInvalidatedAsync(Guid campaignId, Guid encounterId, string revision, CancellationToken cancellationToken = default)
    { log.Add(new(campaignId, "BattlemapInvalidated", encounterId)); return inner.BattlemapInvalidatedAsync(campaignId, encounterId, revision, cancellationToken); }
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
