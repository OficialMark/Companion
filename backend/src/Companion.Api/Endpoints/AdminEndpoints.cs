using Companion.Application.Campaigns;

namespace Companion.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin").WithTags("Admin").RequireRateLimiting("admin");

        group.MapGet("/campaigns", (HttpRequest request, AdminCampaignService service) =>
            Results.Ok(service.List(Credential(request))));

        group.MapDelete("/campaigns/{campaignId:guid}", async (
            Guid campaignId, HttpRequest request, AdminCampaignService service,
            ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            await service.DeleteAsync(campaignId, Credential(request), ct);
            loggerFactory.CreateLogger("Administration")
                .LogInformation("Campaign {CampaignId} permanently deleted by administrator", campaignId);
            return Results.NoContent();
        });

        return endpoints;
    }

    private static string Credential(HttpRequest request) => request.Headers["X-Admin-Key"].ToString();
}
