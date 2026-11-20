using System.Text.Json;
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

    public const int MaxSlugLength = 160;
    public const int MaxNameEnLength = 120;
    public const int MaxSubcategoryLength = 80;
    public const int MaxRarityLength = 30;
    public const int MaxCostLength = 40;

    public CatalogItem(Guid id, string slug, CatalogItemFields fields)
    {
        Id = id;
        Slug = CatalogText.Slug(slug, MaxSlugLength);
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
    /// <summary>Normalized name, English name and description: what the broad search (spec 012) matches.</summary>
    public string SearchText { get; private set; } = string.Empty;
    public bool IsRemovedFromCatalog { get; private set; }

    /// <summary>
    /// Generic magic weapons and armor ("Arma +1", "Armadura de Resistência") have no stat block of
    /// their own: adding one asks for the base item, whose block is copied (see CharacterService).
    /// </summary>
    public bool RequiresBaseItem { get; private set; }

    public void Apply(CatalogItemFields fields)
    {
        var raw = fields.Template;
        var requiresBaseItem = (raw.Category == ItemCategory.Weapon && raw.Weapon is null) ||
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
        // Every value is checked against its column (CharacterConfigurations) before anything is
        // assigned, so a bad seed row throws here and is skipped, instead of failing the whole SaveChanges.
        var nameEn = CatalogText.Optional(fields.NameEn, MaxNameEnLength, "nameEn");
        var subcategory = CatalogText.Optional(fields.Subcategory, MaxSubcategoryLength, "subcategory");
        var rarity = CatalogText.Optional(fields.Rarity, MaxRarityLength, "rarity");
        var cost = CatalogText.Optional(fields.Cost, MaxCostLength, "cost");
        RequiresBaseItem = requiresBaseItem;
        Name = template.Name;
        NameEn = nameEn;
        Category = template.Category;
        Subcategory = subcategory;
        Rarity = rarity;
        RequiresAttunement = fields.RequiresAttunement;
        Cost = cost;
        Template = template;
        SearchKey = Creature.BuildSearchKey(Name, NameEn);
        SearchText = CatalogText.SearchText(Name, NameEn, template.Description);
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

    public const int MaxSlugLength = 120;
    public const int MaxHigherLevelsLength = 4000;
    public const int MaxClassesJsonLength = 400;

    public CatalogSpell(Guid id, string slug, CatalogSpellFields fields)
    {
        Id = id;
        Slug = CatalogText.Slug(slug, MaxSlugLength);
        Apply(fields);
    }

    public Guid Id { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    /// <summary>Official English name (spec 012), from the fixed PT→EN table of the importer.</summary>
    public string? NameEn { get; private set; }
    public int Level { get; private set; }
    public string? School { get; private set; }
    public bool Ritual { get; private set; }
    public bool Concentration { get; private set; }
    public IReadOnlyList<string> Classes { get; private set; } = [];
    public string? HigherLevels { get; private set; }
    public CharacterSpellData Template { get; private set; } = null!;
    public string SearchKey { get; private set; } = string.Empty;
    /// <summary>Normalized name, English name and description: what the broad search (spec 012) matches.</summary>
    public string SearchText { get; private set; } = string.Empty;
    public bool IsRemovedFromCatalog { get; private set; }

    public void Apply(CatalogSpellFields fields)
    {
        var template = fields.Template.Validated();
        string[] classes = (fields.Classes ?? []).Select(x => (x ?? string.Empty).Trim().ToLowerInvariant())
            .Where(x => x.Length > 0).Distinct().Order().ToArray();
        // Same serializer settings as the column (default encoder): the stored JSON has this length.
        if (JsonSerializer.Serialize(classes).Length > MaxClassesJsonLength)
            throw new ArgumentException($"classes: a lista passa de {MaxClassesJsonLength} caracteres.");
        var higherLevels = CatalogText.Optional(fields.HigherLevels, MaxHigherLevelsLength, "higherLevels");
        var nameEn = CatalogText.Optional(fields.NameEn, CatalogItem.MaxNameEnLength, "nameEn");
        Name = template.Name;
        NameEn = nameEn;
        Level = template.Level;
        School = template.School;
        Ritual = template.Ritual;
        Concentration = template.Concentration;
        Classes = classes;
        HigherLevels = higherLevels;
        Template = template;
        SearchKey = Creature.BuildSearchKey(Name, NameEn);
        SearchText = CatalogText.SearchText(Name, NameEn, template.Description);
        IsRemovedFromCatalog = false;
    }

    public void MarkRemoved() => IsRemovedFromCatalog = true;
}

public sealed record CatalogSpellFields(CharacterSpellData Template, IReadOnlyList<string> Classes, string? HigherLevels, string? NameEn = null);

/// <summary>Length checks of the catalog columns; <see cref="ArgumentException"/> makes the seeder skip the row.</summary>
public static partial class CatalogText
{
    /// <summary>
    /// The text the broad search matches (spec 012): name, English name and description without HTML,
    /// lowercase and accent-free like <see cref="Creature.Normalize"/>, whitespace collapsed.
    /// </summary>
    public static string SearchText(string name, string? nameEn, string? description)
    {
        var plain = Tags().Replace(description ?? string.Empty, " ");
        var joined = string.Join(" | ", new[] { name, nameEn, System.Net.WebUtility.HtmlDecode(plain) }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return Spaces().Replace(Creature.Normalize(joined), " ").Trim();
    }

    [System.Text.RegularExpressions.GeneratedRegex("<[^>]*>")]
    private static partial System.Text.RegularExpressions.Regex Tags();

    [System.Text.RegularExpressions.GeneratedRegex(@"\s+")]
    private static partial System.Text.RegularExpressions.Regex Spaces();

    /// <summary>Slugs are compared trimmed and in lowercase, like the unique index (case-insensitive collation).</summary>
    public static string NormalizeSlug(string? slug) => (slug ?? string.Empty).Trim().ToLowerInvariant();

    public static string Slug(string? slug, int max)
    {
        var normalized = NormalizeSlug(slug);
        if (normalized.Length is 0 || normalized.Length > max)
            throw new ArgumentException($"slug: deve ter entre 1 e {max} caracteres.");
        return normalized;
    }

    public static string? Optional(string? value, int max, string field)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (trimmed.Length > max) throw new ArgumentException($"{field}: passa de {max} caracteres.");
        return trimmed;
    }
}
