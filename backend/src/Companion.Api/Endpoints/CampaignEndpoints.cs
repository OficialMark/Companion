using Companion.Application.Campaigns;
using Companion.Application.Contracts;

namespace Companion.Api.Endpoints;

public static class CampaignEndpoints
{
    public static IEndpointRouteBuilder MapCampaignEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/campaigns").WithTags("Campaigns");

        group.MapPost("/", async (CreateCampaignRequest request, CampaignService service, CancellationToken ct) =>
            Results.Created("/api/campaigns", await service.CreateAsync(request, ct)));

        group.MapPost("/join", async (JoinCampaignRequest request, CampaignService service, CancellationToken ct) =>
            Results.Ok(await service.JoinAsync(request, ct))).RequireRateLimiting("access");

        group.MapGet("/{campaignId:guid}/snapshot", async (
            Guid campaignId,
            HttpRequest request,
            CampaignService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetSnapshotAsync(campaignId, request.Headers["X-Participant-Token"].ToString(), ct)));

        group.MapPut("/{campaignId:guid}/participants/me/combat-profile", async (
            Guid campaignId, UpdateCombatProfileRequest body, HttpRequest request,
            CampaignService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateCombatProfileAsync(
                campaignId, request.Headers["X-Participant-Token"].ToString(), body, ct)));
        group.MapPut("/{campaignId:guid}/participants/me/initiative-modifier", async (
            Guid campaignId, UpdateInitiativeModifierRequest body, HttpRequest request,
            CampaignService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateInitiativeModifierAsync(
                campaignId, request.Headers["X-Participant-Token"].ToString(), body, ct)));

        return endpoints;
    }
}
