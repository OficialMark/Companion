using Companion.Application.Contracts;
using Companion.Domain.Campaigns;
using Companion.Domain.Models;

namespace Companion.Application.Campaigns;

public sealed record CampaignKnowledgeSummary(Guid Id, CampaignKnowledgeKind Kind, string Title, string? Summary, bool IsPlayerVisible, string Author, Guid AuthorId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, bool HasImage, int PointCount);
public sealed record CampaignKnowledgeDetail(Guid Id, CampaignKnowledgeKind Kind, string Title, string? Summary, string? SharedDescription,
    bool IsPlayerVisible, string Author, Guid AuthorId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string Version,
    bool HasImage, string? MasterNote, IReadOnlyList<CampaignKnowledgeContributionDto> Contributions, IReadOnlyList<CampaignMapPointDto> Points,
    string? PdfFileName = null);
public sealed record CampaignKnowledgeContributionDto(Guid Id, string Body, string Author, Guid AuthorId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record CampaignMapPointDto(Guid Id, string Title, string? Description, decimal X, decimal Y, string Author, Guid AuthorId, DateTimeOffset CreatedAt);
public sealed record CampaignKnowledgePage(IReadOnlyList<CampaignKnowledgeSummary> Items, int Total, int Page, int PageSize);
public sealed record SaveCampaignKnowledgeRequest(CampaignKnowledgeKind Kind, string Title, string? Summary, string? SharedDescription, bool? IsPlayerVisible, string? ExpectedVersion);
public sealed record SaveKnowledgeContributionRequest(string Body);
public sealed record SaveMasterNoteRequest(string? Body);
public sealed record SaveCampaignMapPointRequest(string Title, string? Description, decimal X, decimal Y);
public sealed record CampaignKnowledgeImage(byte[] Content, string MediaType, string FileName);
public sealed record CampaignKnowledgePdf(byte[] Content, string FileName);

public sealed class CampaignKnowledgeService(
    ICompanionDbContext db,
    ParticipantAuthorization authorization,
    ICampaignNotifier notifier,
    TimeProvider timeProvider)
{
    private const int MaxPageSize = 50;

    public Task<CampaignKnowledgePage> ListAsync(Guid campaignId, string token, CampaignKnowledgeKind? kind, string? query, int page, int pageSize, CancellationToken ct)
    {
        var actor = authorization.Require(campaignId, token);
        var rows = VisibleEntries(campaignId, actor);
        if (kind is not null) rows = rows.Where(x => x.Kind == kind);
        var term = query?.Trim();
        if (!string.IsNullOrWhiteSpace(term)) rows = rows.Where(x => x.Title.Contains(term) || (x.Summary != null && x.Summary.Contains(term)));
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var total = rows.Count();
        // Select only metadata: map bytes never travel in or load for a listing.
        var selected = rows.OrderByDescending(x => x.UpdatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.Kind, x.Title, x.Summary, x.IsPlayerVisible, x.CreatedByParticipantId, x.CreatedAt, x.UpdatedAt, HasImage = x.ImageContent != null })
            .ToArray();
        var authors = db.Participants.Where(x => selected.Select(e => e.CreatedByParticipantId).Contains(x.Id)).ToDictionary(x => x.Id, x => x.DisplayName);
        var counts = db.CampaignMapPoints.Where(x => selected.Select(e => e.Id).Contains(x.MapEntryId)).GroupBy(x => x.MapEntryId).ToDictionary(x => x.Key, x => x.Count());
        var items = selected.Select(x => new CampaignKnowledgeSummary(x.Id, x.Kind, x.Title, x.Summary, x.IsPlayerVisible,
            authors.GetValueOrDefault(x.CreatedByParticipantId, "Participante"), x.CreatedByParticipantId, x.CreatedAt, x.UpdatedAt,
            x.HasImage, counts.GetValueOrDefault(x.Id))).ToArray();
        return Task.FromResult(new CampaignKnowledgePage(items, total, page, pageSize));
    }

    public Task<CampaignKnowledgeDetail> GetAsync(Guid campaignId, string token, Guid entryId, CancellationToken ct)
    {
        var actor = authorization.Require(campaignId, token);
        var entry = FindEntry(campaignId, actor, entryId);
        return Task.FromResult(Detail(entry, actor.Role == ParticipantRole.Master));
    }

    public async Task<CampaignKnowledgeDetail> CreateAsync(Guid campaignId, string token, SaveCampaignKnowledgeRequest request, CancellationToken ct)
    {
        var actor = authorization.Require(campaignId, token);
        if (request.Kind == CampaignKnowledgeKind.MasterRecord && actor.Role != ParticipantRole.Master)
            throw new AppException(AppErrorKind.Forbidden, "Somente o mestre pode criar registros privados.");
        if (actor.Role != ParticipantRole.Master && request.IsPlayerVisible == false)
            throw new AppException(AppErrorKind.Forbidden, "Somente o mestre pode criar conteúdo oculto.");
        var now = timeProvider.GetUtcNow();
        var entry = new CampaignKnowledgeEntry(Guid.NewGuid(), campaignId, request.Kind, request.Title, request.Summary,
            request.SharedDescription, actor.Id, request.IsPlayerVisible ?? true, now);
        db.Add(entry);
        await db.SaveChangesAsync(ct);
        await Invalidate(campaignId, ct);
        return Detail(entry, actor.Role == ParticipantRole.Master);
    }

    public async Task<CampaignKnowledgeDetail> SaveAsync(Guid campaignId, string token, Guid entryId, SaveCampaignKnowledgeRequest request, CancellationToken ct)
    {
        var actor = authorization.Require(campaignId, token);
        var entry = FindEntry(campaignId, actor, entryId);
        if (entry.Kind != request.Kind) throw new AppException(AppErrorKind.Validation, "O tipo do registro não pode ser alterado.");
        if (actor.Role != ParticipantRole.Master && request.IsPlayerVisible is not null)
            throw new AppException(AppErrorKind.Forbidden, "Somente o mestre pode alterar a visibilidade.");
        var version = ParseVersion(request.ExpectedVersion);
        db.ExpectVersion(entry, version);
        entry.Apply(request.Title, request.Summary, request.SharedDescription,
            actor.Role == ParticipantRole.Master ? request.IsPlayerVisible ?? entry.IsPlayerVisible : entry.IsPlayerVisible,
            timeProvider.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await Invalidate(campaignId, ct);
        return Detail(entry, actor.Role == ParticipantRole.Master);
    }

    public async Task DeleteAsync(Guid campaignId, string token, Guid entryId, CancellationToken ct)
    {
        var actor = authorization.Require(campaignId, token);
        var entry = FindEntry(campaignId, actor, entryId);
        if (actor.Role != ParticipantRole.Master && actor.Id != entry.CreatedByParticipantId)
            throw new AppException(AppErrorKind.Forbidden, "Você só pode remover seus próprios registros.");
        db.Remove(entry);
        await db.SaveChangesAsync(ct);
        await Invalidate(campaignId, ct);
    }

    public async Task<CampaignKnowledgeContributionDto> AddContributionAsync(Guid campaignId, string token, Guid entryId, SaveKnowledgeContributionRequest request, CancellationToken ct)
    {
        var actor = authorization.Require(campaignId, token);
        var entry = FindEntry(campaignId, actor, entryId);
        var now = timeProvider.GetUtcNow();
        var contribution = new CampaignKnowledgeContribution(Guid.NewGuid(), entry.Id, request.Body, actor.Id, now);
        db.Add(contribution);
        await db.SaveChangesAsync(ct);
        await Invalidate(campaignId, ct);
        return ToDto(contribution, actor.DisplayName);
    }

    public async Task<CampaignKnowledgeContributionDto> SaveContributionAsync(Guid campaignId, string token, Guid entryId, Guid contributionId, SaveKnowledgeContributionRequest request, CancellationToken ct)
    {
        var actor = authorization.Require(campaignId, token);
        _ = FindEntry(campaignId, actor, entryId);
        var contribution = db.CampaignKnowledgeContributions.FirstOrDefault(x => x.Id == contributionId && x.EntryId == entryId)
            ?? throw new AppException(AppErrorKind.NotFound, "Contribuição não encontrada.");
        if (actor.Role != ParticipantRole.Master && actor.Id != contribution.CreatedByParticipantId)
            throw new AppException(AppErrorKind.Forbidden, "Você só pode editar suas próprias contribuições.");
        contribution.Apply(request.Body, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await Invalidate(campaignId, ct);
        return ToDto(contribution, actor.DisplayName);
    }

    public async Task DeleteContributionAsync(Guid campaignId, string token, Guid entryId, Guid contributionId, CancellationToken ct)
    {
        var actor = authorization.Require(campaignId, token);
        _ = FindEntry(campaignId, actor, entryId);
        var contribution = db.CampaignKnowledgeContributions.FirstOrDefault(x => x.Id == contributionId && x.EntryId == entryId)
            ?? throw new AppException(AppErrorKind.NotFound, "Contribuição não encontrada.");
        if (actor.Role != ParticipantRole.Master && actor.Id != contribution.CreatedByParticipantId)
            throw new AppException(AppErrorKind.Forbidden, "Você só pode remover suas próprias contribuições.");
        db.Remove(contribution);
        await db.SaveChangesAsync(ct);
        await Invalidate(campaignId, ct);
    }

    public async Task<CampaignKnowledgeDetail> SaveMasterNoteAsync(Guid campaignId, string token, Guid entryId, SaveMasterNoteRequest request, CancellationToken ct)
    {
        var actor = authorization.Require(campaignId, token, masterOnly: true);
        var entry = db.CampaignKnowledgeEntries.FirstOrDefault(x => x.CampaignId == campaignId && x.Id == entryId)
            ?? throw new AppException(AppErrorKind.NotFound, "Registro não encontrado.");
        var note = db.CampaignKnowledgeMasterNotes.FirstOrDefault(x => x.EntryId == entryId);
        var now = timeProvider.GetUtcNow();
        if (note is null) db.Add(new CampaignKnowledgeMasterNote(entryId, request.Body, actor.Id, now));
        else note.Apply(request.Body, now);
        await db.SaveChangesAsync(ct);
        await Invalidate(campaignId, ct);
        return Detail(entry, true);
    }

    public async Task<CampaignMapPointDto> AddPointAsync(Guid campaignId, string token, Guid mapId, SaveCampaignMapPointRequest request, CancellationToken ct)
    {
        var actor = authorization.Require(campaignId, token);
        var map = FindEntry(campaignId, actor, mapId);
        if (map.Kind != CampaignKnowledgeKind.Map) throw new AppException(AppErrorKind.NotFound, "Mapa não encontrado.");
        var point = new CampaignMapPoint(Guid.NewGuid(), mapId, request.Title, request.Description, request.X, request.Y, actor.Id, timeProvider.GetUtcNow());
        db.Add(point); await db.SaveChangesAsync(ct); await Invalidate(campaignId, ct);
        return ToDto(point, actor.DisplayName);
    }

    public async Task<CampaignMapPointDto> SavePointAsync(Guid campaignId, string token, Guid mapId, Guid pointId, SaveCampaignMapPointRequest request, CancellationToken ct)
    {
        var actor = authorization.Require(campaignId, token);
        var map = FindEntry(campaignId, actor, mapId);
        if (map.Kind != CampaignKnowledgeKind.Map) throw new AppException(AppErrorKind.NotFound, "Mapa não encontrado.");
        var point = db.CampaignMapPoints.FirstOrDefault(x => x.MapEntryId == mapId && x.Id == pointId)
            ?? throw new AppException(AppErrorKind.NotFound, "Ponto não encontrado.");
        if (actor.Role != ParticipantRole.Master && actor.Id != point.CreatedByParticipantId)
            throw new AppException(AppErrorKind.Forbidden, "Você só pode editar seus próprios pontos.");
        point.Apply(request.Title, request.Description, request.X, request.Y, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(ct); await Invalidate(campaignId, ct);
        return ToDto(point, actor.DisplayName);
    }

    public async Task DeletePointAsync(Guid campaignId, string token, Guid mapId, Guid pointId, CancellationToken ct)
    {
        var actor = authorization.Require(campaignId, token);
        var map = FindEntry(campaignId, actor, mapId);
        if (map.Kind != CampaignKnowledgeKind.Map) throw new AppException(AppErrorKind.NotFound, "Mapa não encontrado.");
        var point = db.CampaignMapPoints.FirstOrDefault(x => x.MapEntryId == mapId && x.Id == pointId)
            ?? throw new AppException(AppErrorKind.NotFound, "Ponto não encontrado.");
        if (actor.Role != ParticipantRole.Master && actor.Id != point.CreatedByParticipantId)
            throw new AppException(AppErrorKind.Forbidden, "Você só pode remover seus próprios pontos.");
        db.Remove(point); await db.SaveChangesAsync(ct); await Invalidate(campaignId, ct);
    }

    public async Task<CampaignKnowledgeImage> GetImageAsync(Guid campaignId, string token, Guid entryId, CancellationToken ct)
    {
        var actor = authorization.Require(campaignId, token);
        var entry = FindEntry(campaignId, actor, entryId);
        if (entry.ImageContent is null || entry.ImageMediaType is null)
            throw new AppException(AppErrorKind.NotFound, "Imagem não encontrada.");
        return await Task.FromResult(new CampaignKnowledgeImage(entry.ImageContent, entry.ImageMediaType, entry.ImageFileName ?? "imagem"));
    }

    public async Task<CampaignKnowledgeDetail> UploadImageAsync(Guid campaignId, string token, Guid entryId, CampaignKnowledgeImage image, CancellationToken ct)
    {
        var actor = authorization.Require(campaignId, token);
        var entry = FindEntry(campaignId, actor, entryId);
        try
        {
            entry.Apply(entry.Title, entry.Summary, entry.SharedDescription, entry.IsPlayerVisible, timeProvider.GetUtcNow());
            entry.SetImage(image.Content, image.MediaType, image.FileName);
        }
        catch (ArgumentException exception) { throw new AppException(AppErrorKind.Validation, exception.Message); }
        await db.SaveChangesAsync(ct); await Invalidate(campaignId, ct);
        return Detail(entry, actor.Role == ParticipantRole.Master);
    }

    public CampaignKnowledgePdf GetPdf(Guid campaignId, string token, Guid entryId)
    {
        var actor = authorization.Require(campaignId, token);
        var entry = VisibleEntries(campaignId, actor).Where(x => x.Id == entryId && x.PdfContent != null)
            .Select(x => new CampaignKnowledgePdf(x.PdfContent!, x.PdfFileName!)).FirstOrDefault();
        return entry ?? throw new AppException(AppErrorKind.NotFound, "PDF não encontrado.");
    }

    public async Task<CampaignKnowledgeDetail> SetPdfAsync(
        Guid campaignId, string token, Guid entryId, CampaignKnowledgePdf? pdf, CancellationToken ct)
    {
        var actor = authorization.Require(campaignId, token);
        var entry = FindEntry(campaignId, actor, entryId);
        if (actor.Role != ParticipantRole.Master && actor.Id != entry.CreatedByParticipantId)
            throw new AppException(AppErrorKind.Forbidden, "Somente o autor ou um mestre pode alterar o PDF.");

        try { entry.SetPdf(pdf?.Content, pdf?.FileName, timeProvider.GetUtcNow()); }
        catch (ArgumentException exception) { throw new AppException(AppErrorKind.Validation, exception.Message); }

        await db.SaveChangesAsync(ct);
        await Invalidate(campaignId, ct);
        return Detail(entry, actor.Role == ParticipantRole.Master);
    }

    private IQueryable<CampaignKnowledgeEntry> VisibleEntries(Guid campaignId, Participant actor) =>
        db.CampaignKnowledgeEntries.Where(x => x.CampaignId == campaignId &&
            (actor.Role == ParticipantRole.Master || (x.Kind != CampaignKnowledgeKind.MasterRecord && x.IsPlayerVisible)));

    private CampaignKnowledgeEntry FindEntry(Guid campaignId, Participant actor, Guid entryId) =>
        VisibleEntries(campaignId, actor).FirstOrDefault(x => x.Id == entryId)
        ?? throw new AppException(AppErrorKind.NotFound, "Registro não encontrado.");

    private CampaignKnowledgeDetail Detail(CampaignKnowledgeEntry entry, bool master)
    {
        var author = db.Participants.FirstOrDefault(x => x.Id == entry.CreatedByParticipantId)?.DisplayName ?? "Participante";
        var contributions = db.CampaignKnowledgeContributions.Where(x => x.EntryId == entry.Id).OrderBy(x => x.CreatedAt).ToArray()
            .Select(x => ToDto(x, db.Participants.FirstOrDefault(p => p.Id == x.CreatedByParticipantId)?.DisplayName ?? "Participante")).ToArray();
        CampaignMapPointDto[] points = entry.Kind == CampaignKnowledgeKind.Map
            ? db.CampaignMapPoints.Where(x => x.MapEntryId == entry.Id).OrderBy(x => x.CreatedAt).ToArray()
                .Select(x => ToDto(x, db.Participants.FirstOrDefault(p => p.Id == x.CreatedByParticipantId)?.DisplayName ?? "Participante")).ToArray()
            : [];
        var note = master ? db.CampaignKnowledgeMasterNotes.FirstOrDefault(x => x.EntryId == entry.Id)?.Body : null;
        return new CampaignKnowledgeDetail(entry.Id, entry.Kind, entry.Title, entry.Summary, entry.SharedDescription, entry.IsPlayerVisible,
            author, entry.CreatedByParticipantId, entry.CreatedAt, entry.UpdatedAt, Convert.ToBase64String(entry.Version), entry.ImageContent is not null, note, contributions, points, entry.PdfFileName);
    }
    private static CampaignKnowledgeContributionDto ToDto(CampaignKnowledgeContribution x, string author) =>
        new(x.Id, x.Body, author, x.CreatedByParticipantId, x.CreatedAt, x.UpdatedAt);
    private static CampaignMapPointDto ToDto(CampaignMapPoint x, string author) =>
        new(x.Id, x.Title, x.Description, x.X, x.Y, author, x.CreatedByParticipantId, x.CreatedAt);
    private static byte[] ParseVersion(string? value)
    {
        try { return Convert.FromBase64String(value ?? string.Empty); }
        catch (FormatException) { throw new AppException(AppErrorKind.Validation, "Versão do registro inválida."); }
    }
    private Task Invalidate(Guid campaignId, CancellationToken ct) => notifier.NotifyAsync(campaignId, "CampaignNotebookInvalidated", new { }, ct);
}
