using Companion.Application.Campaigns;
using Companion.Application.Contracts;
using Companion.Domain.Catalog;
using Companion.Domain.Characters;
using Companion.Domain.Models;

namespace Companion.Application.Catalog;

public sealed record CatalogItemSummaryDto(Guid Id, string Name, string? NameEn, ItemCategory Category, string? Subcategory,
    string? Rarity, bool RequiresAttunement, int MagicBonus, bool RequiresBaseItem);

public sealed record CatalogItemDto(Guid Id, string Name, string? NameEn, ItemCategory Category, string? Subcategory,
    string? Rarity, bool RequiresAttunement, string? Cost, bool RequiresBaseItem, InventoryItemData Template);

public sealed record CatalogSpellSummaryDto(Guid Id, string Name, int Level, string? School, bool Ritual, bool Concentration,
    IReadOnlyList<string> Classes, string? NameEn = null);

public sealed record CatalogSpellDto(Guid Id, string Name, int Level, IReadOnlyList<string> Classes, string? HigherLevels,
    CharacterSpellData Template, string? NameEn = null);

public sealed record CatalogPageDto<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize, string? Attribution);

/// <summary><see cref="Mundane"/> lists only plain items (no rarity, no bonus): the base choices for "Arma +1".</summary>
public sealed record CatalogItemQuery(string? Q, ItemCategory? Category, string? Rarity, int Page = 1, int PageSize = 24, bool Mundane = false);

public sealed record CatalogSpellQuery(string? Q, int? Level, string? School, string? Class, bool? Ritual, bool? Concentration,
    int Page = 1, int PageSize = 24);

/// <summary>Read-only catalogs of items (SRD 5.2.1) and spells (magias5e) for any participant (spec 011, US7).</summary>
public sealed class CatalogService(ICompanionDbContext db, ParticipantAuthorization authorization)
{
    /// <summary>The attribution the CC-BY-4.0 license of the SRD 5.2.1 requires (shown in the item catalog only).</summary>
    public const string ItemAttribution =
        "This work includes material from the System Reference Document 5.2.1 (\"SRD 5.2.1\") by Wizards of the Coast LLC, " +
        "available at https://www.dndbeyond.com/srd. The SRD 5.2.1 is licensed under the Creative Commons Attribution 4.0 " +
        "International License, available at https://creativecommons.org/licenses/by/4.0/legalcode.";

    /// <summary>Below this length a term only matches names: one or two letters are in every description.</summary>
    public const int MinDescriptionTermLength = 3;

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
        var items = db.CatalogItems.Where(x => !x.IsRemovedFromCatalog);
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

        var total = items.Count();
        // Name matches first, then the rows that matched only by description (spec 012, FR-003).
        var ordered = key is null ? items.OrderBy(x => 0) : items.OrderBy(x => x.SearchKey.Contains(key) ? 0 : 1);
        var rows = ordered.ThenBy(x => x.Name).ThenBy(x => x.Slug).Skip((page - 1) * size).Take(size).AsEnumerable()
            .Select(x => new CatalogItemSummaryDto(x.Id, x.Name, x.NameEn, x.Category, x.Subcategory, x.Rarity,
                x.RequiresAttunement, x.Template.MagicBonus, x.RequiresBaseItem))
            .ToArray();
        return new CatalogPageDto<CatalogItemSummaryDto>(rows, total, page, size, ItemAttribution);
    }

    public CatalogItemDto GetItem(Guid campaignId, string token, Guid id)
    {
        authorization.Require(campaignId, token);
        var item = db.CatalogItems.FirstOrDefault(x => x.Id == id && !x.IsRemovedFromCatalog)
            ?? throw new AppException(AppErrorKind.NotFound, "Item não encontrado no catálogo.");
        return new CatalogItemDto(item.Id, item.Name, item.NameEn, item.Category, item.Subcategory, item.Rarity,
            item.RequiresAttunement, item.Cost, item.RequiresBaseItem, item.Template);
    }

    public CatalogPageDto<CatalogSpellSummaryDto> ListSpells(Guid campaignId, string token, CatalogSpellQuery query)
    {
        authorization.Require(campaignId, token);
        var (page, size) = Paging(query.Page, query.PageSize);
        var spells = db.CatalogSpells.Where(x => !x.IsRemovedFromCatalog);
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
            .Select(x => new CatalogSpellSummaryDto(x.Id, x.Name, x.Level, x.School, x.Ritual, x.Concentration, x.Classes, x.NameEn))
            .ToArray();
        return new CatalogPageDto<CatalogSpellSummaryDto>(pageRows, list.Length, page, size, null);
    }

    public CatalogSpellDto GetSpell(Guid campaignId, string token, Guid id)
    {
        authorization.Require(campaignId, token);
        var spell = db.CatalogSpells.FirstOrDefault(x => x.Id == id && !x.IsRemovedFromCatalog)
            ?? throw new AppException(AppErrorKind.NotFound, "Magia não encontrada no catálogo.");
        return new CatalogSpellDto(spell.Id, spell.Name, spell.Level, spell.Classes, spell.HigherLevels, spell.Template, spell.NameEn);
    }

    public const int MaxPage = 100_000;

    /// <summary>
    /// Page 1..100000 and size 1..50, so the offset (page - 1) * size stays under 5 million and never
    /// overflows into a negative Skip (page=2147483647 used to be a 500). A page past the end is empty.
    /// </summary>
    public static (int Page, int Size) Paging(int page, int size) => (Math.Clamp(page, 1, MaxPage), Math.Clamp(size, 1, 50));
}
