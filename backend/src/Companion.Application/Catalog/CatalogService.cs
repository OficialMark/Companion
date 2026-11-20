using Companion.Application.Campaigns;
using Companion.Application.Contracts;
using Companion.Domain.Catalog;
using Companion.Domain.Characters;
using Companion.Domain.Models;

namespace Companion.Application.Catalog;

public sealed record CatalogItemSummaryDto(Guid Id, string Name, string? NameEn, ItemCategory Category, string? Subcategory,
    string? Rarity, bool RequiresAttunement, int MagicBonus, bool RequiresBaseItem, CatalogOrigin Origin = CatalogOrigin.Compendium);

public sealed record CatalogItemDto(Guid Id, string Name, string? NameEn, ItemCategory Category, string? Subcategory,
    string? Rarity, bool RequiresAttunement, string? Cost, bool RequiresBaseItem, InventoryItemData Template,
    CatalogOrigin Origin = CatalogOrigin.Compendium, Guid? BaseId = null, bool BaseRemoved = false);

public sealed record CatalogSpellSummaryDto(Guid Id, string Name, int Level, string? School, bool Ritual, bool Concentration,
    IReadOnlyList<string> Classes, string? NameEn = null, CatalogOrigin Origin = CatalogOrigin.Compendium);

public sealed record CatalogSpellDto(Guid Id, string Name, int Level, IReadOnlyList<string> Classes, string? HigherLevels,
    CharacterSpellData Template, string? NameEn = null, CatalogOrigin Origin = CatalogOrigin.Compendium, Guid? BaseId = null,
    bool BaseRemoved = false);

public sealed record CatalogPageDto<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize, string? Attribution);

/// <summary><see cref="Mundane"/> lists only plain items (no rarity, no bonus): the base choices for "Arma +1".</summary>
public sealed record CatalogItemQuery(string? Q, ItemCategory? Category, string? Rarity, int Page = 1, int PageSize = 24, bool Mundane = false,
    CatalogOrigin? Origin = null);

public sealed record CatalogSpellQuery(string? Q, int? Level, string? School, string? Class, bool? Ritual, bool? Concentration,
    int Page = 1, int PageSize = 24, CatalogOrigin? Origin = null);

/// <summary>
/// The master's form for a campaign item (spec 013, FR-007). A Weapon without <c>weapon</c> (or an Armor without
/// <c>armor</c>) is a generic magic item that asks for a base item when added, like "Arma +1".
/// </summary>
public sealed record SaveCatalogItemRequest(InventoryItemData? Template, string? NameEn, string? Subcategory, string? Rarity,
    bool RequiresAttunement, string? Cost);

public sealed record SaveCatalogSpellRequest(CharacterSpellData? Template, IReadOnlyList<string>? Classes, string? HigherLevels, string? NameEn);

/// <summary>
/// Catalogs of items (SRD 5.2.1) and spells (magias5e). Any participant reads what the campaign sees
/// (spec 011, US7; spec 013, FR-003); only the master writes, and only campaign rows (spec 013, US1).
/// </summary>
public sealed class CatalogService(ICompanionDbContext db, ParticipantAuthorization authorization)
{
    /// <summary>The attribution the CC-BY-4.0 license of the SRD 5.2.1 requires (shown in the item catalog only).</summary>
    public const string ItemAttribution =
        "This work includes material from the System Reference Document 5.2.1 (\"SRD 5.2.1\") by Wizards of the Coast LLC, " +
        "available at https://www.dndbeyond.com/srd. The SRD 5.2.1 is licensed under the Creative Commons Attribution 4.0 " +
        "International License, available at https://creativecommons.org/licenses/by/4.0/legalcode.";

    /// <summary>Below this length a term only matches names: one or two letters are in every description.</summary>
    public const int MinDescriptionTermLength = 3;

    public const string GlobalEntryNotDeletable =
        "Entradas do compêndio não podem ser excluídas; edite-as para criar a versão da mesa.";

    /// <summary>Lowercase, accent-free, inner whitespace collapsed (like the stored search columns); null when blank.</summary>
    private static string? SearchTerm(string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return null;
        return string.Join(' ', Creature.Normalize(q).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    public CatalogPageDto<CatalogItemSummaryDto> ListItems(Guid campaignId, string token, CatalogItemQuery query)
    {
        authorization.Require(campaignId, token);
        var (page, size) = Paging(query.Page, query.PageSize);
        var items = CatalogScope.WithOrigin(CatalogScope.Items(db, campaignId), query.Origin);
        var key = SearchTerm(query.Q);
        var broad = key is { Length: >= MinDescriptionTermLength };
        if (key is not null)
            items = broad ? items.Where(x => x.SearchKey.Contains(key) || x.SearchText.Contains(key)) : items.Where(x => x.SearchKey.Contains(key));

        if (query.Category is { } category) items = items.Where(x => x.Category == category);
        if (!string.IsNullOrWhiteSpace(query.Rarity))
        {
            var rarity = query.Rarity.Trim();
            items = rarity == "comum-ou-sem" ? items.Where(x => x.Rarity == null) : items.Where(x => x.Rarity == rarity);
        }

        if (query.Mundane) items = items.Where(x => x.Rarity == null && !x.RequiresBaseItem);

        // Name matches first, then the rows that matched only by description (spec 012, FR-003).
        var ordered = (key is null ? items.OrderBy(x => 0) : items.OrderBy(x => x.SearchKey.Contains(key) ? 0 : 1))
            .ThenBy(x => x.Name).ThenBy(x => x.Slug);
        int total;
        IEnumerable<CatalogItem> pageRows;
        if (query.Mundane)
        {
            // A base item must carry no bonus (CharacterService.WithBaseItem). The seed always gives magic items a
            // rarity, but a master's entry may have a bonus without one (spec 013); the bonus lives in the JSON
            // template, so this short list is filtered in memory.
            var mundane = ordered.AsEnumerable().Where(x => x.Template.MagicBonus == 0).ToList();
            total = mundane.Count;
            pageRows = mundane.Skip((page - 1) * size).Take(size);
        }
        else
        {
            total = items.Count();
            pageRows = ordered.Skip((page - 1) * size).Take(size).AsEnumerable();
        }

        var rows = pageRows
            .Select(x => new CatalogItemSummaryDto(x.Id, x.Name, x.NameEn, x.Category, x.Subcategory, x.Rarity,
                x.RequiresAttunement, x.Template.MagicBonus, x.RequiresBaseItem, x.Origin))
            .ToArray();
        return new CatalogPageDto<CatalogItemSummaryDto>(rows, total, page, size, ItemAttribution);
    }

    public CatalogItemDto GetItem(Guid campaignId, string token, Guid id)
    {
        authorization.Require(campaignId, token);
        return ToDto(CatalogScope.ResolveItem(db, campaignId, id));
    }

    public CatalogPageDto<CatalogSpellSummaryDto> ListSpells(Guid campaignId, string token, CatalogSpellQuery query)
    {
        authorization.Require(campaignId, token);
        var (page, size) = Paging(query.Page, query.PageSize);
        var spells = CatalogScope.WithOrigin(CatalogScope.Spells(db, campaignId), query.Origin);
        var key = SearchTerm(query.Q);
        var broad = key is { Length: >= MinDescriptionTermLength };
        if (key is not null)
            spells = broad ? spells.Where(x => x.SearchKey.Contains(key) || x.SearchText.Contains(key)) : spells.Where(x => x.SearchKey.Contains(key));

        if (query.Level is { } level) spells = spells.Where(x => x.Level == level);
        if (!string.IsNullOrWhiteSpace(query.School))
        {
            var school = query.School.Trim();
            spells = spells.Where(x => x.School == school);
        }

        if (query.Ritual is { } ritual) spells = spells.Where(x => x.Ritual == ritual);
        if (query.Concentration is { } concentration) spells = spells.Where(x => x.Concentration == concentration);

        // The class list is a small JSON column: filter it in memory after the SQL filters.
        var ordered = key is null ? spells.OrderBy(x => 0) : spells.OrderBy(x => x.SearchKey.Contains(key) ? 0 : 1);
        var rows = ordered.ThenBy(x => x.Level).ThenBy(x => x.Name).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query.Class))
        {
            var spellClass = query.Class.Trim().ToLowerInvariant();
            rows = rows.Where(x => x.Classes.Contains(spellClass));
        }

        var list = rows.ToArray();
        var pageRows = list.Skip((page - 1) * size).Take(size)
            .Select(x => new CatalogSpellSummaryDto(x.Id, x.Name, x.Level, x.School, x.Ritual, x.Concentration, x.Classes, x.NameEn, x.Origin))
            .ToArray();
        return new CatalogPageDto<CatalogSpellSummaryDto>(pageRows, list.Length, page, size, null);
    }

    public CatalogSpellDto GetSpell(Guid campaignId, string token, Guid id)
    {
        authorization.Require(campaignId, token);
        return ToDto(CatalogScope.ResolveSpell(db, campaignId, id));
    }

    // ---- Master writes (spec 013, US1) ----

    public async Task<CatalogItemDto> CreateItemAsync(Guid campaignId, string token, SaveCatalogItemRequest request, CancellationToken cancellationToken)
    {
        authorization.Require(campaignId, token, masterOnly: true);
        var item = Validate(() => CatalogItem.ForCampaign(Guid.NewGuid(), campaignId, null, ItemFields(request)));
        db.Add(item);
        await SaveAsync(cancellationToken);
        return ToDto(item);
    }

    /// <summary>A campaign row is updated in place; a global row gets (or updates) the campaign's copy (FR-002).</summary>
    public async Task<CatalogItemDto> SaveItemAsync(Guid campaignId, string token, Guid id, SaveCatalogItemRequest request, CancellationToken cancellationToken)
    {
        authorization.Require(campaignId, token, masterOnly: true);
        var row = CatalogScope.ResolveItem(db, campaignId, id);
        var fields = ItemFields(request);
        if (row.CampaignId is null)
        {
            row = Validate(() => CatalogItem.ForCampaign(Guid.NewGuid(), campaignId, row.Id, fields));
            db.Add(row);
        }
        else
        {
            var target = row;
            Validate(() => { target.Apply(fields); return target; });
        }

        await SaveAsync(cancellationToken);
        return ToDto(row);
    }

    /// <summary>Custom entry: deleted (null). Edited copy: deleted, and the global original it replaced comes back.</summary>
    public async Task<CatalogItemDto?> DeleteItemAsync(Guid campaignId, string token, Guid id, CancellationToken cancellationToken)
    {
        authorization.Require(campaignId, token, masterOnly: true);
        var row = CatalogScope.ResolveItem(db, campaignId, id);
        if (row.CampaignId is null) throw new AppException(AppErrorKind.Validation, GlobalEntryNotDeletable);
        db.Remove(row);
        await SaveAsync(cancellationToken);
        if (row.BaseId is not { } originalId) return null;
        var original = db.CatalogItems.FirstOrDefault(x => x.Id == originalId && !x.IsRemovedFromCatalog);
        return original is null ? null : ToDto(original);
    }

    public async Task<CatalogSpellDto> CreateSpellAsync(Guid campaignId, string token, SaveCatalogSpellRequest request, CancellationToken cancellationToken)
    {
        authorization.Require(campaignId, token, masterOnly: true);
        var spell = Validate(() => CatalogSpell.ForCampaign(Guid.NewGuid(), campaignId, null, SpellFields(request)));
        db.Add(spell);
        await SaveAsync(cancellationToken);
        return ToDto(spell);
    }

    public async Task<CatalogSpellDto> SaveSpellAsync(Guid campaignId, string token, Guid id, SaveCatalogSpellRequest request, CancellationToken cancellationToken)
    {
        authorization.Require(campaignId, token, masterOnly: true);
        var row = CatalogScope.ResolveSpell(db, campaignId, id);
        var fields = SpellFields(request);
        if (row.CampaignId is null)
        {
            row = Validate(() => CatalogSpell.ForCampaign(Guid.NewGuid(), campaignId, row.Id, fields));
            db.Add(row);
        }
        else
        {
            var target = row;
            Validate(() => { target.Apply(fields); return target; });
        }

        await SaveAsync(cancellationToken);
        return ToDto(row);
    }

    public async Task<CatalogSpellDto?> DeleteSpellAsync(Guid campaignId, string token, Guid id, CancellationToken cancellationToken)
    {
        authorization.Require(campaignId, token, masterOnly: true);
        var row = CatalogScope.ResolveSpell(db, campaignId, id);
        if (row.CampaignId is null) throw new AppException(AppErrorKind.Validation, GlobalEntryNotDeletable);
        db.Remove(row);
        await SaveAsync(cancellationToken);
        if (row.BaseId is not { } originalId) return null;
        var original = db.CatalogSpells.FirstOrDefault(x => x.Id == originalId && !x.IsRemovedFromCatalog);
        return original is null ? null : ToDto(original);
    }

    private static CatalogItemFields ItemFields(SaveCatalogItemRequest? request)
    {
        var template = request?.Template ?? throw new AppException(AppErrorKind.Validation, "Envie os dados do item.");
        return new CatalogItemFields(template with { Effects = template.Effects ?? [] }, request.NameEn, request.Subcategory,
            request.Rarity, request.RequiresAttunement, request.Cost);
    }

    private static CatalogSpellFields SpellFields(SaveCatalogSpellRequest? request)
    {
        var template = request?.Template ?? throw new AppException(AppErrorKind.Validation, "Envie os dados da magia.");
        return new CatalogSpellFields(template, request.Classes ?? [], request.HigherLevels, request.NameEn);
    }

    private static bool IsDbUpdate(Exception exception)
    {
        for (var type = exception.GetType(); type is not null; type = type.BaseType)
            if (type.Name == "DbUpdateException") return true;
        return false;
    }

    private static T Validate<T>(Func<T> action)
    {
        try { return action(); }
        catch (ArgumentException exception) { throw new AppException(AppErrorKind.Validation, exception.Message); }
    }

    /// <summary>
    /// The unique (campaign, base) index turns a double click on "Salvar" of a global entry into a conflict, not a
    /// second copy; a save or delete that lost a race with a delete (0 rows affected) is a conflict too, never a 500.
    /// The Application layer does not reference EF, so the type is matched by name, base types included
    /// (DbUpdateConcurrencyException derives from DbUpdateException).
    /// </summary>
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (Exception exception) when (IsDbUpdate(exception))
        {
            throw new AppException(AppErrorKind.Conflict, "Esta entrada acabou de ser editada em outra tela. Recarregue o compêndio e tente de novo.");
        }
    }

    /// <summary>
    /// <c>BaseRemoved</c>: the global entry an edited copy replaced left the seed, so deleting the copy brings
    /// nothing back; the UI says "Excluir" instead of "Restaurar original".
    /// </summary>
    private CatalogItemDto ToDto(CatalogItem item) =>
        new(item.Id, item.Name, item.NameEn, item.Category, item.Subcategory, item.Rarity, item.RequiresAttunement, item.Cost,
            item.RequiresBaseItem, item.Template, item.Origin, item.BaseId,
            item.BaseId is { } baseId && !db.CatalogItems.Any(x => x.Id == baseId && !x.IsRemovedFromCatalog));

    private CatalogSpellDto ToDto(CatalogSpell spell) =>
        new(spell.Id, spell.Name, spell.Level, spell.Classes, spell.HigherLevels, spell.Template, spell.NameEn, spell.Origin, spell.BaseId,
            spell.BaseId is { } baseId && !db.CatalogSpells.Any(x => x.Id == baseId && !x.IsRemovedFromCatalog));

    public const int MaxPage = 100_000;

    /// <summary>
    /// Page 1..100000 and size 1..50, so the offset (page - 1) * size stays under 5 million and never
    /// overflows into a negative Skip (page=2147483647 used to be a 500). A page past the end is empty.
    /// </summary>
    public static (int Page, int Size) Paging(int page, int size) => (Math.Clamp(page, 1, MaxPage), Math.Clamp(size, 1, 50));
}
