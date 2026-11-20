using Companion.Application.Contracts;
using Companion.Domain.Catalog;

namespace Companion.Application.Catalog;

/// <summary>
/// What a campaign sees of the catalog (spec 013, FR-003): its own rows plus the global rows none of its
/// rows replace. One definition, used by the catalog lists and by "add to sheet", so both always agree.
/// </summary>
public static class CatalogScope
{
    public const string ItemNotFound = "Item não encontrado no catálogo.";
    public const string SpellNotFound = "Magia não encontrada no catálogo.";

    public static IQueryable<CatalogItem> Items(ICompanionDbContext db, Guid campaignId)
    {
        var replaced = db.CatalogItems.Where(x => x.CampaignId == campaignId && x.BaseId != null).Select(x => x.BaseId!.Value);
        return db.CatalogItems.Where(x => x.CampaignId == campaignId ||
                                          (x.CampaignId == null && !x.IsRemovedFromCatalog && !replaced.Contains(x.Id)));
    }

    public static IQueryable<CatalogSpell> Spells(ICompanionDbContext db, Guid campaignId)
    {
        var replaced = db.CatalogSpells.Where(x => x.CampaignId == campaignId && x.BaseId != null).Select(x => x.BaseId!.Value);
        return db.CatalogSpells.Where(x => x.CampaignId == campaignId ||
                                           (x.CampaignId == null && !x.IsRemovedFromCatalog && !replaced.Contains(x.Id)));
    }

    public static IQueryable<CatalogItem> WithOrigin(IQueryable<CatalogItem> rows, CatalogOrigin? origin) => origin switch
    {
        CatalogOrigin.Compendium => rows.Where(x => x.CampaignId == null),
        CatalogOrigin.Edited => rows.Where(x => x.CampaignId != null && x.BaseId != null),
        CatalogOrigin.Custom => rows.Where(x => x.CampaignId != null && x.BaseId == null),
        _ => rows
    };

    public static IQueryable<CatalogSpell> WithOrigin(IQueryable<CatalogSpell> rows, CatalogOrigin? origin) => origin switch
    {
        CatalogOrigin.Compendium => rows.Where(x => x.CampaignId == null),
        CatalogOrigin.Edited => rows.Where(x => x.CampaignId != null && x.BaseId != null),
        CatalogOrigin.Custom => rows.Where(x => x.CampaignId != null && x.BaseId == null),
        _ => rows
    };

    /// <summary>
    /// The row a campaign means by <paramref name="id"/> (research R4): a global id the campaign replaced
    /// resolves to the campaign copy, so a list opened before the edit still adds the table's version.
    /// Another campaign's row, or a removed global one, is not found.
    /// </summary>
    public static CatalogItem ResolveItem(ICompanionDbContext db, Guid campaignId, Guid id)
    {
        var row = db.CatalogItems.FirstOrDefault(x => x.Id == id) ?? throw new AppException(AppErrorKind.NotFound, ItemNotFound);
        if (row.CampaignId is { } owner)
            return owner == campaignId ? row : throw new AppException(AppErrorKind.NotFound, ItemNotFound);
        var copy = db.CatalogItems.FirstOrDefault(x => x.CampaignId == campaignId && x.BaseId == id);
        if (copy is not null) return copy;
        return row.IsRemovedFromCatalog ? throw new AppException(AppErrorKind.NotFound, ItemNotFound) : row;
    }

    public static CatalogSpell ResolveSpell(ICompanionDbContext db, Guid campaignId, Guid id)
    {
        var row = db.CatalogSpells.FirstOrDefault(x => x.Id == id) ?? throw new AppException(AppErrorKind.NotFound, SpellNotFound);
        if (row.CampaignId is { } owner)
            return owner == campaignId ? row : throw new AppException(AppErrorKind.NotFound, SpellNotFound);
        var copy = db.CatalogSpells.FirstOrDefault(x => x.CampaignId == campaignId && x.BaseId == id);
        if (copy is not null) return copy;
        return row.IsRemovedFromCatalog ? throw new AppException(AppErrorKind.NotFound, SpellNotFound) : row;
    }
}
