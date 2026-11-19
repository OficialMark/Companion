using Companion.Application.Creatures;
using Companion.Infrastructure.Creatures;
using System.Text.Json.Serialization;
using Companion.Api;
using Companion.Api.Endpoints;
using Companion.Api.Realtime;
using Companion.Application.Battlemaps;
using Companion.Application.Campaigns;
using Companion.Application.Contracts;
using Companion.Application.DiceRolls;
using Companion.Application.Encounters;
using Companion.Infrastructure.Persistence;
using Companion.Infrastructure.Randomness;
using Companion.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR().AddJsonProtocol(options =>
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = 21 * 1024 * 1024);
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("sqlserver");
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("access", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 12,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.AddPolicy("admin", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 8,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

var connectionString = builder.Configuration.GetConnectionString("Companion")
    ?? throw new InvalidOperationException("ConnectionStrings:Companion não foi configurada.");
builder.Services.AddDbContext<CompanionDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<ICompanionDbContext>(provider => provider.GetRequiredService<CompanionDbContext>());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IParticipantTokenService, ParticipantTokenService>();
builder.Services.AddSingleton<IAdminCredentialValidator, AdminCredentialValidator>();
builder.Services.AddSingleton<IDiceRandomizer, CryptoDiceRandomizer>();
builder.Services.AddSingleton<IPresenceTracker, CampaignPresenceTracker>();
builder.Services.AddScoped<ICampaignNotifier, SignalRCampaignNotifier>();
builder.Services.AddScoped<ParticipantAuthorization>();
builder.Services.AddScoped<CampaignService>();
builder.Services.AddScoped<AdminCampaignService>();
builder.Services.AddScoped<DiceRollService>();
builder.Services.AddScoped<EncounterService>();
builder.Services.AddScoped<BattlemapService>();
builder.Services.AddScoped<AttackService>();
builder.Services.AddScoped<CreatureService>();
builder.Services.AddScoped<CompendiumSeeder>();
builder.Services.AddScoped<ParticipantNameBackfill>();
builder.Services.AddScoped<Companion.Application.Characters.CharacterService>();
builder.Services.AddScoped<Companion.Application.Catalog.CatalogService>();
builder.Services.AddSingleton<ICompendiumSeedSource, EmbeddedCompendiumSeedSource>();
builder.Services.AddSingleton<CreatureImageBackoff>();
// No automatic redirects: the host allowlist is checked once, before the request.
builder.Services.AddHttpClient<ICreatureImageSource, HttpCreatureImageSource>()
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });

var app = builder.Build();
if (!app.Environment.IsEnvironment("Testing")) app.UseRateLimiter();

app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Request");
    using var scope = logger.BeginScope(new Dictionary<string, object>
    {
        ["TraceId"] = context.TraceIdentifier,
        ["Path"] = context.Request.Path.Value ?? string.Empty
    });

    try
    {
        await next();
    }
    catch (AppException exception)
    {
        var status = exception.Kind switch
        {
            AppErrorKind.Validation => StatusCodes.Status400BadRequest,
            AppErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
            AppErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            AppErrorKind.NotFound => StatusCodes.Status404NotFound,
            AppErrorKind.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
        logger.LogWarning("Request rejected with {Kind}: {Message}", exception.Kind, exception.Message);
        await Results.Problem(statusCode: status, title: "Não foi possível concluir a operação", detail: exception.Message)
            .ExecuteAsync(context);
    }
    catch (DbUpdateConcurrencyException)
    {
        logger.LogWarning("Concurrent update rejected");
        await Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "O estado mudou",
            detail: "Atualize a mesa e tente novamente.").ExecuteAsync(context);
    }
    catch (DbUpdateException exception)
    {
        logger.LogWarning(exception, "Database constraint rejected request");
        await Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflito de dados",
            detail: "A operação conflita com um registro existente.").ExecuteAsync(context);
    }
});

app.MapHealthChecks("/health");
app.MapCampaignEndpoints();
app.MapDiceRollEndpoints();
app.MapEncounterEndpoints();
app.MapBattlemapEndpoints();
app.MapCreatureEndpoints();
app.MapCharacterEndpoints();
app.MapAdminEndpoints();
app.MapHub<CampaignHub>("/hubs/campaign");

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
    await db.Database.MigrateAsync();
    // Contas por nome (spec 011, FR-044): normaliza nomes antigos e escolhe a dona de cada nome repetido.
    // Idempotente (só pega quem tem NameKey nula). Nunca fatal: sem ele, os tokens antigos seguem
    // valendo; só o join pelo nome não acha as contas antigas até a próxima subida.
    // Escopo próprio: se falhar no meio, as alterações pendentes não vazam para o SaveChanges do seeder.
    var backfillLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ParticipantNameBackfill");
    try
    {
        await using var backfillScope = app.Services.CreateAsyncScope();
        var backfill = await backfillScope.ServiceProvider.GetRequiredService<ParticipantNameBackfill>().RunAsync();
        if (backfill.Changed)
            backfillLogger.LogInformation("Participant names backfilled: {Renamed} renamed, {Keys} account keys assigned",
                backfill.Renamed, backfill.KeysAssigned);
        foreach (var participantId in backfill.Invalid)
            backfillLogger.LogWarning("Participant {ParticipantId} kept its name: it does not normalize", participantId);
    }
    catch (Exception exception)
    {
        backfillLogger.LogError(exception, "Participant name backfill failed; the API keeps running without it");
    }
    // Global creature compendium (spec 008): a no-op unless the embedded seed changed. Never fatal:
    // bad rows are skipped and logged, and any other failure leaves the API up (library degraded).
    var seedLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("CompendiumSeeder");
    try
    {
        var seeded = await scope.ServiceProvider.GetRequiredService<CompendiumSeeder>().SeedAsync();
        if (seeded.Applied) seedLogger.LogInformation("Creature compendium seeded: {Count} entries", seeded.Upserted);
        foreach (var skipped in seeded.Skipped) seedLogger.LogWarning("Creature compendium entry skipped: {Entry}", skipped);
    }
    catch (Exception exception)
    {
        seedLogger.LogError(exception, "Creature compendium seed failed; the API keeps running without it");
    }
}

await app.RunAsync();

public partial class Program;
