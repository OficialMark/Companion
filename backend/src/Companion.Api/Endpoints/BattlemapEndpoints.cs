using Companion.Application.Battlemaps;
using Companion.Application.Contracts;

namespace Companion.Api.Endpoints;

public static class BattlemapEndpoints
{
    public static IEndpointRouteBuilder MapBattlemapEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/campaigns/{campaignId:guid}/encounters/{encounterId:guid}/battlemap")
            .WithTags("Battlemaps");

        group.MapGet("/", async (
            Guid campaignId,
            Guid encounterId,
            HttpRequest request,
            BattlemapService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetAsync(campaignId, encounterId, Token(request), ct)));

        group.MapPut("/", async (
            Guid campaignId,
            Guid encounterId,
            HttpRequest request,
            BattlemapService service,
            CancellationToken ct) =>
        {
            if (request.ContentLength is > 21 * 1024 * 1024)
                return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, detail: "A imagem excede 20 MB.");
            var form = await request.ReadFormAsync(ct);
            var image = form.Files.GetFile("image")
                ?? throw new AppException(AppErrorKind.Validation, "Selecione uma imagem.");
            if (!int.TryParse(form["columns"], out var columns) ||
                !int.TryParse(form["rows"], out var rows))
                throw new AppException(AppErrorKind.Validation, "Informe linhas e colunas inteiras.");
            var isGridVisible = !bool.TryParse(form["isGridVisible"], out var parsedGridVisibility) ||
                parsedGridVisibility;
            if (image.Length > 20 * 1024 * 1024)
                return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, detail: "A imagem excede 20 MB.");
            await using var stream = image.OpenReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            var expectedRevision = form["expectedRevision"].ToString();
            var result = await service.ConfigureAsync(
                campaignId,
                encounterId,
                Token(request),
                new ConfigureBattlemapRequest(
                    buffer.ToArray(),
                    image.ContentType,
                    image.FileName,
                    columns,
                    rows,
                    isGridVisible,
                    string.IsNullOrWhiteSpace(expectedRevision) ? null : expectedRevision),
                ct);
            return result.Created
                ? Results.Created($"/api/campaigns/{campaignId}/encounters/{encounterId}/battlemap", result.Result)
                : Results.Ok(result.Result);
        }).DisableAntiforgery();

        group.MapGet("/image", async (
            Guid campaignId,
            Guid encounterId,
            Guid revision,
            HttpContext context,
            BattlemapService service,
            CancellationToken ct) =>
        {
            var image = await service.GetImageAsync(
                campaignId, encounterId, revision, Token(context.Request), ct);
            var etag = string.Concat('"', image.ImageRevision, '"');
            if (context.Request.Headers.IfNoneMatch.Any(value => value == etag))
                return Results.StatusCode(StatusCodes.Status304NotModified);
            context.Response.Headers.ETag = etag;
            context.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
            return Results.File(image.Content, image.MediaType, enableRangeProcessing: false);
        });

        group.MapPut("/tokens/{tokenId:guid}/position", async (
            Guid campaignId,
            Guid encounterId,
            Guid tokenId,
            MoveBattlemapTokenRequest body,
            HttpRequest request,
            BattlemapService service,
            CancellationToken ct) =>
            Results.Ok(await service.MoveAsync(
                campaignId, encounterId, tokenId, Token(request), body, ct)));

        group.MapPut("/tokens/{tokenId:guid}/attack-area", async (
            Guid campaignId,
            Guid encounterId,
            Guid tokenId,
            SetAttackAreaRequest body,
            HttpRequest request,
            BattlemapService service,
            CancellationToken ct) =>
            Results.Ok(await service.SetAttackAreaAsync(
                campaignId, encounterId, tokenId, Token(request), body, ct)));

        group.MapDelete("/tokens/{tokenId:guid}/attack-area", async (
            Guid campaignId,
            Guid encounterId,
            Guid tokenId,
            HttpRequest request,
            BattlemapService service,
            CancellationToken ct) =>
            Results.Ok(await service.RemoveAttackAreaAsync(
                campaignId, encounterId, tokenId, Token(request), ct)));

        group.MapPut("/tokens/{tokenId:guid}/appearance", async (
            Guid campaignId,
            Guid encounterId,
            Guid tokenId,
            SetTokenAppearanceRequest body,
            HttpRequest request,
            BattlemapService service,
            CancellationToken ct) =>
            Results.Ok(await service.SetTokenAppearanceAsync(
                campaignId, encounterId, tokenId, Token(request), body, ct)));

        group.MapPost("/markers", async (
            Guid campaignId,
            Guid encounterId,
            CreateMarkerRequest body,
            HttpRequest request,
            BattlemapService service,
            CancellationToken ct) =>
            Results.Created(
                $"/api/campaigns/{campaignId}/encounters/{encounterId}/battlemap",
                await service.CreateMarkerAsync(campaignId, encounterId, Token(request), body, ct)));

        group.MapDelete("/markers/{markerId:guid}", async (
            Guid campaignId,
            Guid encounterId,
            Guid markerId,
            HttpRequest request,
            BattlemapService service,
            CancellationToken ct) =>
            Results.Ok(await service.RemoveMarkerAsync(
                campaignId, encounterId, markerId, Token(request), ct)));

        return endpoints;
    }

    private static string Token(HttpRequest request) =>
        request.Headers["X-Participant-Token"].ToString();
}
