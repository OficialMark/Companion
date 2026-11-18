using Companion.Application.Campaigns;
using Companion.Application.Contracts;
using Companion.Domain.Models;

namespace Companion.Application.Creatures;

/// <summary>Remote image fetch for compendium creatures (spec 008, research R5).</summary>
public interface ICreatureImageSource
{
    /// <summary>Returns the image or null when unavailable; never throws for network failures.</summary>
    Task<(byte[] Content, string MediaType)?> FetchAsync(string url, CancellationToken cancellationToken);
}

/// <summary>
/// The master's creature library (spec 008). The compendium is global and never edited in
/// place: editing, re-imaging or annotating a compendium creature creates (or updates) the
/// campaign's own copy, which shadows the original for that campaign only.
/// </summary>
public sealed class CreatureService(
    ICompanionDbContext db,
    ParticipantAuthorization authorization,
    ICreatureImageSource imageSource,
    TimeProvider timeProvider)
{
    public const int MaxPageSize = 60;
    private static readonly TimeSpan ImageRetryAfter = TimeSpan.FromHours(24);

    public CreaturePageDto List(Guid campaignId, string token, CreatureQuery query)
    {
        authorization.Require(campaignId, token, masterOnly: true);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var campaignRows = db.Creatures.Where(x => x.CampaignId == campaignId);
        var shadowed = campaignRows.Where(x => x.BaseCreatureId != null).Select(x => x.BaseCreatureId!.Value);
        var compendiumRows = db.Creatures.Where(x => x.CampaignId == null && !x.IsRemovedFromCompendium && !shadowed.Contains(x.Id));

        var rows = query.Origin switch
        {
            CreatureOrigin.Compendium => compendiumRows,
            CreatureOrigin.Edited => campaignRows.Where(x => x.BaseCreatureId != null),
            CreatureOrigin.Custom => campaignRows.Where(x => x.BaseCreatureId == null),
            _ => campaignRows.Concat(compendiumRows)
        };
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = Creature.Normalize(query.Q.Trim());
            rows = rows.Where(x => x.SearchKey.Contains(term));
        }
        if (query.Size is { } size) rows = rows.Where(x => x.Size == size);
        if (!string.IsNullOrWhiteSpace(query.Type)) rows = rows.Where(x => x.Type == query.Type);
        if (query.CrMin is { } min) rows = rows.Where(x => x.ChallengeValue >= min);
        if (query.CrMax is { } max) rows = rows.Where(x => x.ChallengeValue <= max);

        var total = rows.Count();
        var items = rows.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.Id, x.Name, x.NameEn, x.Size, x.Type, x.ChallengeRating, x.ArmorClass, x.HitPoints,
                x.CampaignId, x.BaseCreatureId, x.ImageRevision, x.ImageSourceUrl
            })
            .AsEnumerable()
            .Select(x => new CreatureSummaryDto(x.Id, x.Name, x.NameEn, x.Size, x.Type, x.ChallengeRating, x.ArmorClass,
                x.HitPoints, OriginOf(x.CampaignId, x.BaseCreatureId),
                ImageUrl(campaignId, x.Id, x.ImageRevision, x.ImageSourceUrl)))
            .ToArray();
        return new CreaturePageDto(items, total, page, pageSize);
    }

    public IReadOnlyList<string> Types(Guid campaignId, string token)
    {
        authorization.Require(campaignId, token, masterOnly: true);
        return db.Creatures
            .Where(x => (x.CampaignId == null && !x.IsRemovedFromCompendium) || x.CampaignId == campaignId)
            .Select(x => x.Type).Distinct().OrderBy(x => x).ToArray();
    }

    public CreatureDto Get(Guid campaignId, string token, Guid creatureId)
    {
        authorization.Require(campaignId, token, masterOnly: true);
        return ToDto(campaignId, ResolveForCampaign(campaignId, creatureId));
    }

    public async Task<CreatureDto> CreateAsync(Guid campaignId, string token, SaveCreatureRequest request, CancellationToken cancellationToken)
    {
        authorization.Require(campaignId, token, masterOnly: true);
        var now = timeProvider.GetUtcNow();
        Creature creature;
        try
        {
            creature = Creature.ForCampaign(Guid.NewGuid(), campaignId, ToStats(request), now);
            creature.SetNotes(request.Notes, now);
        }
        catch (ArgumentException exception) { throw new AppException(AppErrorKind.Validation, exception.Message); }
        db.Add(creature);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(campaignId, creature);
    }

    public async Task<CreatureDto> SaveAsync(Guid campaignId, string token, Guid creatureId, SaveCreatureRequest request, CancellationToken cancellationToken)
    {
        authorization.Require(campaignId, token, masterOnly: true);
        var now = timeProvider.GetUtcNow();
        var target = EditableTarget(campaignId, creatureId, request.ExpectedRevision, now);
        try
        {
            target.Update(ToStats(request), now);
            target.SetNotes(request.Notes, now);
        }
        catch (ArgumentException exception) { throw new AppException(AppErrorKind.Validation, exception.Message); }
        await SaveConcurrentAsync(cancellationToken);
        return ToDto(campaignId, target);
    }

    public async Task<CreatureDto> SaveNotesAsync(Guid campaignId, string token, Guid creatureId, SaveCreatureNotesRequest request, CancellationToken cancellationToken)
    {
        authorization.Require(campaignId, token, masterOnly: true);
        var now = timeProvider.GetUtcNow();
        var target = EditableTarget(campaignId, creatureId, request.ExpectedRevision, now);
        try { target.SetNotes(request.Notes, now); }
        catch (ArgumentException exception) { throw new AppException(AppErrorKind.Validation, exception.Message); }
        await SaveConcurrentAsync(cancellationToken);
        return ToDto(campaignId, target);
    }

    /// <summary>Custom creature: deleted. Edited copy: removed, which restores the compendium original.</summary>
    public async Task<CreatureDto?> DeleteAsync(Guid campaignId, string token, Guid creatureId, CancellationToken cancellationToken)
    {
        authorization.Require(campaignId, token, masterOnly: true);
        var creature = ResolveForCampaign(campaignId, creatureId);
        if (creature.IsCompendium)
            throw new AppException(AppErrorKind.Validation, "Criaturas do compêndio não podem ser excluídas; edite-as para criar a versão da mesa.");

        // Combatants keep fighting; an edited copy's combatants fall back to the original sheet.
        foreach (var combatant in db.Combatants.Where(x => x.CreatureId == creature.Id).ToArray())
        {
            if (creature.BaseCreatureId is { } baseId) combatant.LinkCreature(baseId);
            else combatant.UnlinkCreature();
        }
        var image = db.CreatureImages.SingleOrDefault(x => x.CreatureId == creature.Id);
        if (image is not null) db.Remove(image);
        db.Remove(creature);
        await db.SaveChangesAsync(cancellationToken);

        if (creature.BaseCreatureId is not { } originalId) return null;
        var original = db.Creatures.Single(x => x.Id == originalId);
        return ToDto(campaignId, original);
    }

    public async Task<CreatureDto> SetImageAsync(Guid campaignId, string token, Guid creatureId, byte[] content, string mediaType,
        CancellationToken cancellationToken)
    {
        authorization.Require(campaignId, token, masterOnly: true);
        if (content.Length is < 1 or > CreatureImage.MaxBytes)
            throw new AppException(AppErrorKind.Validation, "A imagem deve ter no máximo 1 MB.");
        if (!ImageFormat.IsValid(content, mediaType))
            throw new AppException(AppErrorKind.Validation, "A imagem deve ser PNG, JPEG ou WebP válido.");
        var now = timeProvider.GetUtcNow();
        var target = EditableTarget(campaignId, creatureId, null, now);
        var image = db.CreatureImages.SingleOrDefault(x => x.CreatureId == target.Id);
        if (image is null)
        {
            image = new CreatureImage(target.Id, content, mediaType, fromSource: false);
            db.Add(image);
        }
        else image.Replace(content, mediaType, fromSource: false);
        target.SetImage(image.Revision, now);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(campaignId, target);
    }

    public async Task<CreatureDto> RemoveImageAsync(Guid campaignId, string token, Guid creatureId, CancellationToken cancellationToken)
    {
        authorization.Require(campaignId, token, masterOnly: true);
        var now = timeProvider.GetUtcNow();
        var target = EditableTarget(campaignId, creatureId, null, now);
        var image = db.CreatureImages.SingleOrDefault(x => x.CreatureId == target.Id);
        if (image is not null) db.Remove(image);
        target.SetImage(null, now);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(campaignId, target);
    }

    /// <summary>
    /// Image for any participant of the campaign (tokens need it). Compendium images are
    /// fetched from their source on first use and stored; failures back off for 24 h.
    /// </summary>
    public async Task<CreatureImageResult> GetImageAsync(Guid campaignId, string token, Guid creatureId, CancellationToken cancellationToken)
    {
        authorization.Require(campaignId, token);
        var creature = db.Creatures.SingleOrDefault(x => x.Id == creatureId && (x.CampaignId == null || x.CampaignId == campaignId))
            ?? throw new AppException(AppErrorKind.NotFound, "Criatura não encontrada.");

        var owner = ImageOwner(creature);
        var stored = db.CreatureImages.SingleOrDefault(x => x.CreatureId == owner.Id);
        if (stored is not null) return new CreatureImageResult(stored.Content, stored.MediaType, stored.Revision);

        var now = timeProvider.GetUtcNow();
        if (owner.ImageSourceUrl is null || (owner.ImageFetchFailedAt is { } failed && now - failed < ImageRetryAfter))
            throw new AppException(AppErrorKind.NotFound, "A criatura não tem imagem.");

        var fetched = await imageSource.FetchAsync(owner.ImageSourceUrl, cancellationToken);
        if (fetched is not { } image || image.Content.Length > CreatureImage.MaxBytes || !ImageFormat.IsValid(image.Content, image.MediaType))
        {
            owner.MarkImageFetchFailed(now);
            await db.SaveChangesAsync(cancellationToken);
            throw new AppException(AppErrorKind.NotFound, "A imagem da criatura não está disponível agora.");
        }
        var row = new CreatureImage(owner.Id, image.Content, image.MediaType, fromSource: true);
        db.Add(row);
        owner.MarkImageFetched(row.Revision);
        if (!ReferenceEquals(owner, creature)) creature.MarkImageFetched(row.Revision);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (Exception) when (db.CreatureImages.Any(x => x.CreatureId == owner.Id))
        {
            // Two viewers fetched concurrently and the other one stored it first: serve theirs.
            var existing = db.CreatureImages.Single(x => x.CreatureId == owner.Id);
            return new CreatureImageResult(existing.Content, existing.MediaType, existing.Revision);
        }
        return new CreatureImageResult(row.Content, row.MediaType, row.Revision);
    }

    /// <summary>The creature as seen by this campaign: its own copy if the compendium entry was edited.</summary>
    internal Creature ResolveForCampaign(Guid campaignId, Guid creatureId)
    {
        var creature = db.Creatures.SingleOrDefault(x => x.Id == creatureId && (x.CampaignId == null || x.CampaignId == campaignId))
            ?? throw new AppException(AppErrorKind.NotFound, "Criatura não encontrada.");
        if (!creature.IsCompendium) return creature;
        return db.Creatures.SingleOrDefault(x => x.CampaignId == campaignId && x.BaseCreatureId == creature.Id) ?? creature;
    }

    public static string? ImageUrl(Guid campaignId, Guid creatureId, Guid? revision, string? sourceUrl) =>
        revision is null && sourceUrl is null
            ? null
            : $"/api/campaigns/{campaignId}/creatures/{creatureId}/image?rev={(revision?.ToString("N") ?? "src")}";

    private Creature ImageOwner(Creature creature)
    {
        // An edited copy that never got its own picture shows (and caches on) the original.
        if (creature.BaseCreatureId is not { } baseId || db.CreatureImages.Any(x => x.CreatureId == creature.Id)) return creature;
        var original = db.Creatures.SingleOrDefault(x => x.Id == baseId);
        return original is not null && original.ImageSourceUrl == creature.ImageSourceUrl ? original : creature;
    }

    private Creature EditableTarget(Guid campaignId, Guid creatureId, string? expectedRevision, DateTimeOffset now)
    {
        var creature = ResolveForCampaign(campaignId, creatureId);
        if (!creature.IsCompendium)
        {
            if (!string.IsNullOrWhiteSpace(expectedRevision) && expectedRevision != DtoMapper.Revision(creature.Version))
                throw new AppException(AppErrorKind.Conflict, "Esta criatura foi alterada em outra aba. Recarregue a ficha.");
            return creature;
        }
        var copy = creature.CreateCampaignCopy(Guid.NewGuid(), campaignId, now);
        db.Add(copy);
        // Combatants of this campaign created from the original now follow the edited sheet.
        foreach (var combatant in db.Combatants.Where(x => x.CreatureId == creature.Id &&
                     db.Encounters.Any(e => e.Id == x.EncounterId && e.CampaignId == campaignId)).ToArray())
            combatant.LinkCreature(copy.Id);
        return copy;
    }

    private async Task SaveConcurrentAsync(CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (Exception exception) when (exception.GetType().Name == "DbUpdateConcurrencyException")
        {
            throw new AppException(AppErrorKind.Conflict, "Esta criatura foi alterada em outra aba. Recarregue a ficha.");
        }
    }

    private static CreatureOrigin OriginOf(Guid? campaignId, Guid? baseCreatureId) =>
        campaignId is null ? CreatureOrigin.Compendium : baseCreatureId is null ? CreatureOrigin.Custom : CreatureOrigin.Edited;

    internal static CreatureStats ToStats(SaveCreatureRequest request) => new(
        request.Name, request.NameEn, request.Size, request.Type, request.Alignment, request.ArmorClass, request.ArmorClassNote,
        request.HitPoints, request.HitDice, request.Speed, request.Initiative,
        request.Strength, request.Dexterity, request.Constitution, request.Intelligence, request.Wisdom, request.Charisma,
        request.SavingThrows, request.Skills, request.DamageVulnerabilities, request.DamageResistances, request.DamageImmunities,
        request.ConditionImmunities, request.Senses, request.Languages, request.ChallengeRating, request.ExperiencePoints,
        request.ProficiencyBonus, Map(request.Traits), Map(request.Actions), Map(request.BonusActions), Map(request.Reactions),
        Map(request.LegendaryActions), request.LegendaryActionsIntro);

    private static List<CreatureAction> Map(IReadOnlyList<CreatureActionDto>? items) =>
        (items ?? []).Select(x => new CreatureAction(x.Name ?? string.Empty, x.Description ?? string.Empty)).ToList();

    private static IReadOnlyList<CreatureActionDto> Map(IReadOnlyList<CreatureAction> items) =>
        items.Select(x => new CreatureActionDto(x.Name, x.Description)).ToArray();

    internal static CreatureDto ToDto(Guid campaignId, Creature x) => new(
        x.Id, x.BaseCreatureId, OriginOf(x.CampaignId, x.BaseCreatureId), x.Name, x.NameEn, x.SourceBook, x.SourceUrl,
        x.Size, x.Type, x.Alignment, x.ArmorClass, x.ArmorClassNote, x.HitPoints, x.HitDice, x.Speed, x.Initiative,
        x.Strength, x.Dexterity, x.Constitution, x.Intelligence, x.Wisdom, x.Charisma,
        x.SavingThrows, x.Skills, x.DamageVulnerabilities, x.DamageResistances, x.DamageImmunities, x.ConditionImmunities,
        x.Senses, x.Languages, x.ChallengeRating, x.ExperiencePoints, x.ProficiencyBonus,
        Map(x.Traits), Map(x.Actions), Map(x.BonusActions), Map(x.Reactions), Map(x.LegendaryActions), x.LegendaryActionsIntro,
        x.Notes, ImageUrl(campaignId, x.Id, x.ImageRevision, x.ImageSourceUrl), x.ImageRevision is not null || x.ImageSourceUrl is not null,
        x.FootprintSize, x.InitiativeModifier, DtoMapper.Revision(x.Version));
}
