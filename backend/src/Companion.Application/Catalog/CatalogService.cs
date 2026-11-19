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
    IReadOnlyList<string> Classes);

public sealed record CatalogSpellDto(Guid Id, string Name, int Level, IReadOnlyList<string> Classes, string? HigherLevels,
    CharacterSpellData Template);

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

    public CatalogPageDto<CatalogItemSummaryDto> ListItems(Guid campaignId, string token, CatalogItemQuery query)
    {
        authorization.Require(campaignId, token);
        var (page, size) = Paging(query.Page, query.PageSize);
        var items = db.CatalogItems.Where(x => !x.IsRemovedFromCatalog);
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var key = Creature.Normalize(query.Q.Trim());
            items = items.Where(x => x.SearchKey.Contains(key));
        }

        if (query.Category is { } category) items = items.Where(x => x.Category == category);
        if (!string.IsNullOrWhiteSpace(query.Rarity))
        {
            var rarity = query.Rarity.Trim();
            items = rarity == "comum-ou-sem" ? items.Where(x => x.Rarity == null) : items.Where(x => x.Rarity == rarity);
        }

        if (query.Mundane) items = items.Where(x => x.Rarity == null && !x.RequiresBaseItem);

        var total = items.Count();
        var rows = items.OrderBy(x => x.Name).ThenBy(x => x.Slug).Skip((page - 1) * size).Take(size).AsEnumerable()
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
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var key = Creature.Normalize(query.Q.Trim());
            spells = spells.Where(x => x.SearchKey.Contains(key));
        }

        if (query.Level is { } level) spells = spells.Where(x => x.Level == level);
        if (!string.IsNullOrWhiteSpace(query.School))
        {
            var school = query.School.Trim();
            spells = spells.Where(x => x.School == school);
        }

        if (query.Ritual is { } ritual) spells = spells.Where(x => x.Ritual == ritual);
        if (query.Concentration is { } concentration) spells = spells.Where(x => x.Concentration == concentration);

        // The class list is a small JSON column: filter it in memory after the SQL filters.
        var rows = spells.OrderBy(x => x.Level).ThenBy(x => x.Name).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query.Class))
        {
            var spellClass = query.Class.Trim().ToLowerInvariant();
            rows = rows.Where(x => x.Classes.Contains(spellClass));
        }

        var list = rows.ToArray();
        var pageRows = list.Skip((page - 1) * size).Take(size)
            .Select(x => new CatalogSpellSummaryDto(x.Id, x.Name, x.Level, x.School, x.Ritual, x.Concentration, x.Classes))
            .ToArray();
        return new CatalogPageDto<CatalogSpellSummaryDto>(pageRows, list.Length, page, size, null);
    }

    public CatalogSpellDto GetSpell(Guid campaignId, string token, Guid id)
    {
        authorization.Require(campaignId, token);
        var spell = db.CatalogSpells.FirstOrDefault(x => x.Id == id && !x.IsRemovedFromCatalog)
            ?? throw new AppException(AppErrorKind.NotFound, "Magia não encontrada no catálogo.");
        return new CatalogSpellDto(spell.Id, spell.Name, spell.Level, spell.Classes, spell.HigherLevels, spell.Template);
    }

    private static (int Page, int Size) Paging(int page, int size) => (Math.Max(1, page), Math.Clamp(size, 1, 50));
}
