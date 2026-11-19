using Companion.Domain.Characters;
using Companion.Domain.Models;

namespace Companion.Domain.Catalog;

/// <summary>
/// An item of the global catalog (spec 011, FR-020), seeded from the SRD 5.2.1 import and never
/// edited in place. <see cref="Template"/> is what a player gets as a copy when adding it.
/// </summary>
public sealed class CatalogItem
{
    private CatalogItem() { }

    public CatalogItem(Guid id, string slug, CatalogItemFields fields)
    {
        Id = id;
        Slug = slug;
        Apply(fields);
    }

    public Guid Id { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? NameEn { get; private set; }
    public ItemCategory Category { get; private set; }
    public string? Subcategory { get; private set; }
    public string? Rarity { get; private set; }
    public bool RequiresAttunement { get; private set; }
    public string? Cost { get; private set; }
    public InventoryItemData Template { get; private set; } = null!;
    public string SearchKey { get; private set; } = string.Empty;
    public bool IsRemovedFromCatalog { get; private set; }

    /// <summary>
    /// Generic magic weapons and armor ("Arma +1", "Armadura de Resistência") have no stat block of
    /// their own: adding one asks for the base item, whose block is copied (see CharacterService).
    /// </summary>
    public bool RequiresBaseItem { get; private set; }

    public void Apply(CatalogItemFields fields)
    {
        var raw = fields.Template;
        RequiresBaseItem = (raw.Category == ItemCategory.Weapon && raw.Weapon is null) ||
                           (raw.Category == ItemCategory.Armor && raw.Armor is null);
        // Validate everything else with a stand-in block, then keep the block empty.
        var probe = raw.Category switch
        {
            ItemCategory.Weapon when raw.Weapon is null => raw with { Weapon = new WeaponData("1d4", null, null, WeaponKind.Melee, WeaponClass.Simple, [], null, null, null, null) },
            ItemCategory.Armor when raw.Armor is null => raw with { Armor = new ArmorData(10, ArmorType.Light, null, false) },
            _ => raw
        };
        var template = probe.Validated() with
        {
            Equipped = false,
            Proficient = false,
            Quantity = Math.Max(1, raw.Quantity),
            Weapon = raw.Category == ItemCategory.Weapon && raw.Weapon is null ? null : probe.Validated().Weapon,
            Armor = raw.Category == ItemCategory.Armor && raw.Armor is null ? null : probe.Validated().Armor
        };
        Name = template.Name;
        NameEn = string.IsNullOrWhiteSpace(fields.NameEn) ? null : fields.NameEn.Trim();
        Category = template.Category;
        Subcategory = string.IsNullOrWhiteSpace(fields.Subcategory) ? null : fields.Subcategory.Trim();
        Rarity = string.IsNullOrWhiteSpace(fields.Rarity) ? null : fields.Rarity.Trim();
        RequiresAttunement = fields.RequiresAttunement;
        Cost = string.IsNullOrWhiteSpace(fields.Cost) ? null : fields.Cost.Trim();
        Template = template;
        SearchKey = Creature.BuildSearchKey(Name, NameEn);
        IsRemovedFromCatalog = false;
    }

    public void MarkRemoved() => IsRemovedFromCatalog = true;
}

public sealed record CatalogItemFields(
    InventoryItemData Template,
    string? NameEn,
    string? Subcategory,
    string? Rarity,
    bool RequiresAttunement,
    string? Cost);

/// <summary>A spell of the global catalog (spec 011, FR-019), seeded from avribacki/magias5e.</summary>
public sealed class CatalogSpell
{
    private CatalogSpell() { }

    public CatalogSpell(Guid id, string slug, CatalogSpellFields fields)
    {
        Id = id;
        Slug = slug;
        Apply(fields);
    }

    public Guid Id { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public int Level { get; private set; }
    public string? School { get; private set; }
    public bool Ritual { get; private set; }
    public bool Concentration { get; private set; }
    public IReadOnlyList<string> Classes { get; private set; } = [];
    public string? HigherLevels { get; private set; }
    public CharacterSpellData Template { get; private set; } = null!;
    public string SearchKey { get; private set; } = string.Empty;
    public bool IsRemovedFromCatalog { get; private set; }

    public void Apply(CatalogSpellFields fields)
    {
        var template = fields.Template.Validated();
        Name = template.Name;
        Level = template.Level;
        School = template.School;
        Ritual = template.Ritual;
        Concentration = template.Concentration;
        Classes = (fields.Classes ?? []).Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).Distinct().Order().ToArray();
        HigherLevels = string.IsNullOrWhiteSpace(fields.HigherLevels) ? null : fields.HigherLevels.Trim();
        Template = template;
        SearchKey = Creature.Normalize(Name);
        IsRemovedFromCatalog = false;
    }

    public void MarkRemoved() => IsRemovedFromCatalog = true;
}

public sealed record CatalogSpellFields(CharacterSpellData Template, IReadOnlyList<string> Classes, string? HigherLevels);
