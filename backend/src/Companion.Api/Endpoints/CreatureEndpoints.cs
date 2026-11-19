using Companion.Application.Contracts;
using Companion.Application.Creatures;
using Companion.Domain.Models;

namespace Companion.Api.Endpoints;

/// <summary>Creature library (spec 008). Master-only except the image, which every participant's tokens need.</summary>
public static class CreatureEndpoints
{
    public static IEndpointRouteBuilder MapCreatureEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/campaigns/{campaignId:guid}/creatures").WithTags("Creatures");

        group.MapGet("/", (Guid campaignId, HttpRequest request, CreatureService service,
            string? q, CreatureSize? size, string? type, decimal? crMin, decimal? crMax, CreatureOrigin? origin,
            int? page, int? pageSize) =>
            Results.Ok(service.List(campaignId, Token(request),
                new CreatureQuery(q, size, type, crMin, crMax, origin, page ?? 1, pageSize ?? 30))));

        group.MapGet("/types", (Guid campaignId, HttpRequest request, CreatureService service) =>
            Results.Ok(service.Types(campaignId, Token(request))));

        group.MapGet("/{creatureId:guid}", (Guid campaignId, Guid creatureId, HttpRequest request, CreatureService service) =>
            Results.Ok(service.Get(campaignId, Token(request), creatureId)));

        group.MapPost("/", async (Guid campaignId, SaveCreatureRequest body, HttpRequest request, CreatureService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(campaignId, Token(request), body, ct);
            return Results.Created($"/api/campaigns/{campaignId}/creatures/{created.Id}", created);
        });

        group.MapPut("/{creatureId:guid}", async (Guid campaignId, Guid creatureId, SaveCreatureRequest body, HttpRequest request,
            CreatureService service, CancellationToken ct) =>
            Results.Ok(await service.SaveAsync(campaignId, Token(request), creatureId, body, ct)));

        group.MapPut("/{creatureId:guid}/notes", async (Guid campaignId, Guid creatureId, SaveCreatureNotesRequest body,
            HttpRequest request, CreatureService service, CancellationToken ct) =>
            Results.Ok(await service.SaveNotesAsync(campaignId, Token(request), creatureId, body, ct)));

        group.MapDelete("/{creatureId:guid}", async (Guid campaignId, Guid creatureId, HttpRequest request,
            CreatureService service, CancellationToken ct) =>
        {
            var restored = await service.DeleteAsync(campaignId, Token(request), creatureId, ct);
            return restored is null ? Results.NoContent() : Results.Ok(restored);
        });

        group.MapPut("/{creatureId:guid}/image", async (Guid campaignId, Guid creatureId, HttpRequest request,
            CreatureService service, CancellationToken ct) =>
        {
            if (request.ContentLength is > CreatureImage.MaxBytes)
                return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, detail: "A imagem excede 1 MB.");
            using var buffer = new MemoryStream();
            await request.Body.CopyToAsync(buffer, ct);
            if (buffer.Length > CreatureImage.MaxBytes)
                return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, detail: "A imagem excede 1 MB.");
            var mediaType = request.ContentType?.Split(';')[0].Trim() ?? string.Empty;
            return Results.Ok(await service.SetImageAsync(campaignId, Token(request), creatureId, buffer.ToArray(), mediaType, ct));
        });

        group.MapDelete("/{creatureId:guid}/image", async (Guid campaignId, Guid creatureId, HttpRequest request,
            CreatureService service, CancellationToken ct) =>
            Results.Ok(await service.RemoveImageAsync(campaignId, Token(request), creatureId, ct)));

        group.MapGet("/{creatureId:guid}/image", async (Guid campaignId, Guid creatureId, HttpContext context,
            CreatureService service, CancellationToken ct) =>
        {
            var image = await service.GetImageAsync(campaignId, Token(context.Request), creatureId, ct);
            var etag = string.Concat('"', image.Revision, '"');
            if (context.Request.Headers.IfNoneMatch.Any(value => value == etag))
                return Results.StatusCode(StatusCodes.Status304NotModified);
            context.Response.Headers.ETag = etag;
            // The URL carries the revision, so a changed image is a new URL.
            context.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
            return Results.File(image.Content, image.MediaType, enableRangeProcessing: false);
        });

        return endpoints;
    }

    private static string Token(HttpRequest request) => request.Headers["X-Participant-Token"].ToString();
}
