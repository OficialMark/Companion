using Companion.Application.Campaigns;
using Companion.Application.Contracts;
using Companion.Domain.Campaigns;

namespace Companion.Api.Endpoints;

public static class CampaignKnowledgeEndpoints
{
    public static IEndpointRouteBuilder MapCampaignKnowledgeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/campaigns/{campaignId:guid}/knowledge").WithTags("Campaign knowledge");
        group.MapGet("/", async (Guid campaignId, string? kind, string? q, int? page, int? pageSize, HttpRequest request, CampaignKnowledgeService service, CancellationToken ct) =>
        {
            var parsed = Enum.TryParse<Companion.Domain.Campaigns.CampaignKnowledgeKind>(kind, true, out var value) ? value : (Companion.Domain.Campaigns.CampaignKnowledgeKind?)null;
            if (!string.IsNullOrWhiteSpace(kind) && parsed is null) throw new AppException(AppErrorKind.Validation, "Tipo de conteúdo inválido.");
            return Results.Ok(await service.ListAsync(campaignId, request.Headers["X-Participant-Token"].ToString(), parsed, q, page ?? 1, pageSize ?? 24, ct));
        });
        group.MapPost("/", async (Guid campaignId, SaveCampaignKnowledgeRequest body, HttpRequest request, CampaignKnowledgeService service, CancellationToken ct) =>
            Results.Created("/api/campaigns/" + campaignId + "/knowledge", await service.CreateAsync(campaignId, request.Headers["X-Participant-Token"].ToString(), body, ct)));
        group.MapGet("/{entryId:guid}", async (Guid campaignId, Guid entryId, HttpRequest request, CampaignKnowledgeService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(campaignId, request.Headers["X-Participant-Token"].ToString(), entryId, ct)));
        group.MapPut("/{entryId:guid}", async (Guid campaignId, Guid entryId, SaveCampaignKnowledgeRequest body, HttpRequest request, CampaignKnowledgeService service, CancellationToken ct) =>
            Results.Ok(await service.SaveAsync(campaignId, request.Headers["X-Participant-Token"].ToString(), entryId, body, ct)));
        group.MapDelete("/{entryId:guid}", async (Guid campaignId, Guid entryId, HttpRequest request, CampaignKnowledgeService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(campaignId, request.Headers["X-Participant-Token"].ToString(), entryId, ct);
            return Results.NoContent();
        });
        group.MapPost("/{entryId:guid}/contributions", async (Guid campaignId, Guid entryId, SaveKnowledgeContributionRequest body, HttpRequest request, CampaignKnowledgeService service, CancellationToken ct) =>
            Results.Created("/api/campaigns/" + campaignId + "/knowledge/" + entryId, await service.AddContributionAsync(campaignId, request.Headers["X-Participant-Token"].ToString(), entryId, body, ct)));
        group.MapPut("/{entryId:guid}/contributions/{contributionId:guid}", async (Guid campaignId, Guid entryId, Guid contributionId, SaveKnowledgeContributionRequest body, HttpRequest request, CampaignKnowledgeService service, CancellationToken ct) =>
            Results.Ok(await service.SaveContributionAsync(campaignId, request.Headers["X-Participant-Token"].ToString(), entryId, contributionId, body, ct)));
        group.MapDelete("/{entryId:guid}/contributions/{contributionId:guid}", async (Guid campaignId, Guid entryId, Guid contributionId, HttpRequest request, CampaignKnowledgeService service, CancellationToken ct) =>
        {
            await service.DeleteContributionAsync(campaignId, request.Headers["X-Participant-Token"].ToString(), entryId, contributionId, ct);
            return Results.NoContent();
        });
        group.MapPut("/{entryId:guid}/master-note", async (Guid campaignId, Guid entryId, SaveMasterNoteRequest body, HttpRequest request, CampaignKnowledgeService service, CancellationToken ct) =>
            Results.Ok(await service.SaveMasterNoteAsync(campaignId, request.Headers["X-Participant-Token"].ToString(), entryId, body, ct)));
        group.MapGet("/{entryId:guid}/image", async (Guid campaignId, Guid entryId, HttpRequest request, CampaignKnowledgeService service, CancellationToken ct) =>
        {
            var image = await service.GetImageAsync(campaignId, request.Headers["X-Participant-Token"].ToString(), entryId, ct);
            return Results.File(image.Content, image.MediaType, image.FileName, enableRangeProcessing: false);
        });
        group.MapPost("/{entryId:guid}/image", async (Guid campaignId, Guid entryId, HttpRequest request, CampaignKnowledgeService service, CancellationToken ct) =>
        {
            if (request.ContentLength is > 22 * 1024 * 1024)
                return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, detail: "A imagem excede 20 MB.");
            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("image") ?? throw new AppException(AppErrorKind.Validation, "Selecione uma imagem.");
            if (file.Length is < 1 or > 20 * 1024 * 1024)
                return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, detail: "A imagem deve ter até 20 MB.");
            await using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            return Results.Ok(await service.UploadImageAsync(campaignId, request.Headers["X-Participant-Token"].ToString(), entryId,
                new CampaignKnowledgeImage(buffer.ToArray(), file.ContentType, file.FileName), ct));
        }).DisableAntiforgery();
        group.MapGet("/{entryId:guid}/pdf", (Guid campaignId, Guid entryId, HttpContext context, CampaignKnowledgeService service) =>
        {
            var pdf = service.GetPdf(campaignId, context.Request.Headers["X-Participant-Token"].ToString(), entryId);
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(pdf.Content, "application/pdf", pdf.FileName);
        });
        group.MapPut("/{entryId:guid}/pdf", async (Guid campaignId, Guid entryId, HttpRequest request, CampaignKnowledgeService service, CancellationToken ct) =>
        {
            const int limit = CampaignKnowledgeEntry.MaxPdfBytes;
            if (request.ContentLength > limit + 1024 * 1024)
                return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, detail: "O PDF deve ter até 20 MB.");
            if (!request.HasFormContentType)
                throw new AppException(AppErrorKind.Validation, "Selecione um PDF.");

            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("pdf") ?? throw new AppException(AppErrorKind.Validation, "Selecione um PDF.");
            if (file.Length > limit)
                return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, detail: "O PDF deve ter até 20 MB.");
            await using var stream = file.OpenReadStream();
            var content = new byte[(int)file.Length];
            await stream.ReadExactlyAsync(content, ct);
            return Results.Ok(await service.SetPdfAsync(campaignId, request.Headers["X-Participant-Token"].ToString(),
                entryId, new CampaignKnowledgePdf(content, file.FileName), ct));
        }).DisableAntiforgery();
        group.MapDelete("/{entryId:guid}/pdf", async (Guid campaignId, Guid entryId, HttpRequest request, CampaignKnowledgeService service, CancellationToken ct) =>
            Results.Ok(await service.SetPdfAsync(campaignId, request.Headers["X-Participant-Token"].ToString(), entryId, null, ct)));
        group.MapPost("/{entryId:guid}/points", async (Guid campaignId, Guid entryId, SaveCampaignMapPointRequest body, HttpRequest request, CampaignKnowledgeService service, CancellationToken ct) =>
            Results.Created("/api/campaigns/" + campaignId + "/knowledge/" + entryId + "/points", await service.AddPointAsync(campaignId, request.Headers["X-Participant-Token"].ToString(), entryId, body, ct)));
        group.MapPut("/{entryId:guid}/points/{pointId:guid}", async (Guid campaignId, Guid entryId, Guid pointId, SaveCampaignMapPointRequest body, HttpRequest request, CampaignKnowledgeService service, CancellationToken ct) =>
            Results.Ok(await service.SavePointAsync(campaignId, request.Headers["X-Participant-Token"].ToString(), entryId, pointId, body, ct)));
        group.MapDelete("/{entryId:guid}/points/{pointId:guid}", async (Guid campaignId, Guid entryId, Guid pointId, HttpRequest request, CampaignKnowledgeService service, CancellationToken ct) =>
        {
            await service.DeletePointAsync(campaignId, request.Headers["X-Participant-Token"].ToString(), entryId, pointId, ct);
            return Results.NoContent();
        });
        return endpoints;
    }
}
