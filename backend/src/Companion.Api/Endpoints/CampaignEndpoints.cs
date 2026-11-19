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

        group.MapPut("/{campaignId:guid}/participants/me/avatar", async (
            Guid campaignId, HttpRequest request, CampaignService service, CancellationToken ct) =>
        {
            if (request.ContentLength is > 2 * 1024 * 1024)
                return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, detail: "A foto excede 1 MB.");
            var form = await request.ReadFormAsync(ct);
            var image = form.Files.GetFile("image")
                ?? throw new AppException(AppErrorKind.Validation, "Selecione uma foto.");
            if (image.Length > Companion.Domain.Models.ParticipantAvatar.MaxImageBytes)
                return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, detail: "A foto excede 1 MB.");
            await using var stream = image.OpenReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            return Results.Ok(await service.UploadAvatarAsync(
                campaignId, request.Headers["X-Participant-Token"].ToString(), buffer.ToArray(), image.ContentType, ct));
        }).DisableAntiforgery();

        group.MapDelete("/{campaignId:guid}/participants/me/avatar", async (
            Guid campaignId, HttpRequest request, CampaignService service, CancellationToken ct) =>
            Results.Ok(await service.RemoveAvatarAsync(campaignId, request.Headers["X-Participant-Token"].ToString(), ct)));

        group.MapGet("/{campaignId:guid}/participants/{participantId:guid}/avatar", async (
            Guid campaignId, Guid participantId, Guid revision, HttpContext context,
            CampaignService service, CancellationToken ct) =>
        {
            var avatar = await service.GetAvatarAsync(
                campaignId, participantId, revision, context.Request.Headers["X-Participant-Token"].ToString(), ct);
            var etag = string.Concat('"', avatar.Revision, '"');
            if (context.Request.Headers.IfNoneMatch.Any(value => value == etag))
                return Results.StatusCode(StatusCodes.Status304NotModified);
            context.Response.Headers.ETag = etag;
            context.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
            return Results.File(avatar.Content, avatar.MediaType, enableRangeProcessing: false);
        });

        return endpoints;
    }
}
