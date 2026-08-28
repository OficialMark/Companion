using System.Text.Json.Serialization;
using Companion.Api;
using Companion.Api.Endpoints;
using Companion.Api.Realtime;
using Companion.Application.Campaigns;
using Companion.Application.Contracts;
using Companion.Application.DiceRolls;
using Companion.Application.Encounters;
using Companion.Infrastructure.Persistence;
using Companion.Infrastructure.Randomness;
using Companion.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR().AddJsonProtocol(options =>
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
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
builder.Services.AddScoped<AttackService>();

var app = builder.Build();
app.UseRateLimiter();

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
app.MapAdminEndpoints();
app.MapHub<CampaignHub>("/hubs/campaign");

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
    await db.Database.MigrateAsync();
}

await app.RunAsync();

public partial class Program;
