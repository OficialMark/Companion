using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Companion.Application.Contracts;
using Companion.Application.Creatures;
using Companion.Domain.Catalog;
using Companion.Domain.Characters;
using Companion.Domain.Models;

namespace Companion.Application.Catalog;

public enum CatalogKind { Items, Spells }

/// <summary>A catalog seed written by scripts/import-catalogs. <see cref="Read"/> returns null when the file is not shipped.</summary>
public interface ICatalogSeedSource
{
    string Key { get; }
    CatalogKind Kind { get; }
    byte[]? Read();
}

/// <summary>
/// Loads the item (SRD 5.2.1) and spell (magias5e) catalogs at startup (spec 011, FR-021), the same
/// way the creature compendium is loaded: a no-op when the seed hash did not change, upsert by slug
/// otherwise, entries gone from the seed marked removed (copies in sheets never change), bad rows
/// skipped and reported. The app never reads the original sites.
/// </summary>
public sealed partial class CatalogSeeder(ICompanionDbContext db, IEnumerable<ICatalogSeedSource> sources, TimeProvider timeProvider)
{
    /// <summary>Bump when <see cref="CatalogItem.Apply"/> or <see cref="CatalogSpell.Apply"/> derive new columns.</summary>
    public const string FormatVersion = "012-search-text";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly Dictionary<string, string> DamageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["acid"] = "ácido", ["bludgeoning"] = "contundente", ["cold"] = "gélido", ["fire"] = "fogo", ["force"] = "força",
        ["lightning"] = "elétrico", ["necrotic"] = "necrótico", ["piercing"] = "perfurante", ["poison"] = "veneno",
        ["psychic"] = "psíquico", ["radiant"] = "radiante", ["slashing"] = "cortante", ["thunder"] = "trovejante"
    };

    private static readonly Dictionary<string, string> Rarities = new(StringComparer.OrdinalIgnoreCase)
    {
        ["common"] = "Comum", ["uncommon"] = "Incomum", ["rare"] = "Raro", ["very-rare"] = "Muito Raro",
        ["legendary"] = "Lendário", ["artifact"] = "Artefato", ["varies"] = "Varia"
    };

    public async Task<IReadOnlyList<SeedResult>> SeedAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<SeedResult>();
        foreach (var source in sources)
        {
            var bytes = source.Read();
            if (bytes is null)
            {
                results.Add(new SeedResult(false, 0, [$"{source.Key}: seed ausente"]));
                continue;
            }

            // The format version is part of the hash: a change in what Apply derives (spec 012 added the
            // search text) reapplies an unchanged seed file once.
            var hash = Convert.ToHexString(SHA256.HashData([.. bytes, .. System.Text.Encoding.UTF8.GetBytes(FormatVersion)]));
            var version = db.SeedVersions.SingleOrDefault(x => x.Key == source.Key);
            if (version?.Hash == hash)
            {
                results.Add(new SeedResult(false, 0, []));
                continue;
            }

            var result = source.Kind == CatalogKind.Items ? SeedItems(bytes) : SeedSpells(bytes);
            var now = timeProvider.GetUtcNow();
            if (version is null) db.Add(new SeedVersion(source.Key, hash, now));
            else version.Update(hash, now);
            await db.SaveChangesAsync(cancellationToken);
            results.Add(result);
        }

        return results;
    }

    private SeedResult SeedItems(byte[] bytes)
    {
        var seed = JsonSerializer.Deserialize<ItemSeedFile>(bytes, Json) ?? throw new InvalidOperationException("Seed de itens vazio.");
        // Slugs compared trimmed and lowercase, like the unique index (case-insensitive collation):
        // "Foo" and "foo " are the same row, so the second is skipped instead of failing SaveChanges.
        // Only global rows: campaign entries (spec 013, FR-009) belong to their master, never to the seed.
        var existing = db.CatalogItems.Where(x => x.CampaignId == null)
            .ToDictionary(x => CatalogText.NormalizeSlug(x.Slug), StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var skipped = new List<string>();
        var changed = 0;
        foreach (var entry in seed.Items ?? [])
        {
            var slug = CatalogText.NormalizeSlug(entry.Slug);
            if (slug.Length == 0) continue;
            if (!seen.Add(slug))
            {
                skipped.Add($"{slug}: slug repetido");
                continue;
            }

            try
            {
                var fields = ToFields(entry);
                if (existing.TryGetValue(slug, out var item)) item.Apply(fields);
                else db.Add(new CatalogItem(Guid.NewGuid(), slug, fields));
                changed++;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NullReferenceException)
            {
                skipped.Add($"{slug}: {exception.Message}");
            }
        }

        foreach (var (slug, item) in existing)
            if (!seen.Contains(slug) && !item.IsRemovedFromCatalog) item.MarkRemoved();
        return new SeedResult(true, changed, skipped);
    }

    private SeedResult SeedSpells(byte[] bytes)
    {
        var seed = JsonSerializer.Deserialize<SpellSeedFile>(bytes, Json) ?? throw new InvalidOperationException("Seed de magias vazio.");
        // Slugs compared trimmed and lowercase, like the unique index (case-insensitive collation):
        // "Foo" and "foo " are the same row, so the second is skipped instead of failing SaveChanges.
        // Only global rows: campaign entries (spec 013, FR-009) belong to their master, never to the seed.
        var existing = db.CatalogSpells.Where(x => x.CampaignId == null)
            .ToDictionary(x => CatalogText.NormalizeSlug(x.Slug), StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var skipped = new List<string>();
        var changed = 0;
        foreach (var entry in seed.Spells ?? [])
        {
            var slug = CatalogText.NormalizeSlug(entry.Slug);
            if (slug.Length == 0) continue;
            if (!seen.Add(slug))
            {
                skipped.Add($"{slug}: slug repetido");
                continue;
            }

            try
            {
                var fields = ToFields(entry);
                if (existing.TryGetValue(slug, out var spell)) spell.Apply(fields);
                else db.Add(new CatalogSpell(Guid.NewGuid(), slug, fields));
                changed++;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NullReferenceException)
            {
                skipped.Add($"{slug}: {exception.Message}");
            }
        }

        foreach (var (slug, spell) in existing)
            if (!seen.Contains(slug) && !spell.IsRemovedFromCatalog) spell.MarkRemoved();
        return new SeedResult(true, changed, skipped);
    }

    public static CatalogItemFields ToFields(SeedItem x)
    {
        var category = Enum.Parse<ItemCategory>(x.Category ?? throw new ArgumentException("Categoria ausente."), ignoreCase: true);
        var marcial = x.Subcategory?.Contains("marcial", StringComparison.OrdinalIgnoreCase) == true;
        WeaponData? weapon = x.Weapon is null ? null : new WeaponData(
            x.Weapon.DamageDice ?? throw new ArgumentException("Dano da arma ausente."),
            DamageType(x.Weapon.DamageType),
            x.Weapon.VersatileDice,
            string.Equals(x.Weapon.Kind, "ranged", StringComparison.OrdinalIgnoreCase) ? WeaponKind.Ranged : WeaponKind.Melee,
            marcial ? WeaponClass.Martial : WeaponClass.Simple,
            x.Weapon.Properties ?? [],
            x.Weapon.RangeNormal,
            x.Weapon.RangeLong,
            null,
            x.Weapon.Mastery);
        ArmorData? armor = x.Armor is null ? null : new ArmorData(x.Armor.BaseAc,
            Enum.Parse<ArmorType>(x.Armor.Type ?? "light", ignoreCase: true), x.Armor.StrengthMin, x.Armor.StealthDisadvantage);
        ShieldData? shield = category == ItemCategory.Shield ? new ShieldData(x.Shield?.AcBonus ?? 2) : null;
        var effects = (x.Effects ?? []).Select(e => new ItemEffect(
            Enum.Parse<EffectType>(e.Type ?? throw new ArgumentException("Efeito sem tipo."), ignoreCase: true),
            e.Target,
            Enum.Parse<EffectOp>(e.Op ?? "add", ignoreCase: true),
            e.Value)).ToArray();
        var template = new InventoryItemData(x.Name ?? throw new ArgumentException("Nome ausente."), category, 1, x.Weight,
            x.Description, null, false, false, Math.Clamp(x.MagicBonus, 0, 3), weapon, armor, shield, effects);
        var rarity = x.Rarity is null ? null : Rarities.GetValueOrDefault(x.Rarity, x.Rarity);
        return new CatalogItemFields(template, x.NameEn, x.Subcategory, rarity, x.RequiresAttunement, x.Cost);
    }

    public static CatalogSpellFields ToFields(SeedSpell x)
    {
        var attackKind = x.AttackKind?.ToLowerInvariant() switch
        {
            "attack" => SpellAttackKind.Attack,
            "save" => SpellAttackKind.Save,
            _ => SpellAttackKind.None
        };
        AbilityCode? save = Abilities.TryParse(x.SaveAbility, out var parsed) ? parsed : null;
        if (attackKind == SpellAttackKind.Save && save is null) attackKind = SpellAttackKind.None;

        string? damageDice = null;
        if (!string.IsNullOrWhiteSpace(x.SuggestedDamageDice))
        {
            try { damageDice = DiceNotation.Sum(x.SuggestedDamageDice, "O dano"); }
            catch (ArgumentException) { damageDice = null; } // e.g. 1d3: not a pad die, the player types it
        }

        var description = HtmlToText(x.DescriptionHtml);
        if (!string.IsNullOrWhiteSpace(x.HigherLevels))
            description = $"{description}\n\nEm Níveis Superiores. {x.HigherLevels.Trim()}".Trim();
        var template = new CharacterSpellData(x.Name ?? throw new ArgumentException("Nome ausente."), x.Level, x.School,
            x.CastingTime, x.Range, x.Components, x.Duration, x.Ritual, x.Concentration,
            description.Length == 0 ? null : description, attackKind, save, damageDice,
            damageDice is null ? null : DamageType(x.SuggestedDamageType), false);
        return new CatalogSpellFields(template, x.Classes ?? [], x.HigherLevels, x.NameEn);
    }

    private static string? DamageType(string? key) =>
        string.IsNullOrWhiteSpace(key) ? null : DamageTypes.GetValueOrDefault(key.Trim(), key.Trim());

    /// <summary>Plain text with paragraph breaks: the app never renders catalog HTML.</summary>
    public static string HtmlToText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var text = ParagraphEnd().Replace(html, "\n\n");
        text = LineBreak().Replace(text, "\n");
        text = ListItem().Replace(text, "\n• ");
        text = AnyTag().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text);
        text = SpacesPerLine().Replace(text, " ");
        text = ManyBreaks().Replace(text, "\n\n");
        return text.Trim();
    }

    [GeneratedRegex("</p\\s*>", RegexOptions.IgnoreCase)] private static partial Regex ParagraphEnd();
    [GeneratedRegex("<br\\s*/?>", RegexOptions.IgnoreCase)] private static partial Regex LineBreak();
    [GeneratedRegex("<li[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex ListItem();
    [GeneratedRegex("<[^>]+>")] private static partial Regex AnyTag();
    [GeneratedRegex("[ \\t]+")] private static partial Regex SpacesPerLine();
    [GeneratedRegex("\\s*\\n\\s*\\n\\s*")] private static partial Regex ManyBreaks();

    public sealed record ItemSeedFile(string? Source, string? Attribution, IReadOnlyList<SeedItem>? Items);
    public sealed record SeedItem(string? Slug, string? Name, string? NameEn, string? Category, string? Subcategory, string? Rarity,
        bool RequiresAttunement, decimal? Weight, string? Cost, string? Description, SeedWeapon? Weapon, SeedArmor? Armor,
        SeedShield? Shield, int MagicBonus, IReadOnlyList<SeedEffect>? Effects);
    public sealed record SeedWeapon(string? DamageDice, string? DamageType, string? VersatileDice, string? Kind,
        IReadOnlyList<string>? Properties, decimal? RangeNormal, decimal? RangeLong, string? Mastery);
    public sealed record SeedArmor(int BaseAc, string? Type, int? StrengthMin, bool StealthDisadvantage);
    public sealed record SeedShield(int AcBonus);
    public sealed record SeedEffect(string? Type, string? Target, string? Op, int? Value);

    public sealed record SpellSeedFile(string? Source, string? Attribution, IReadOnlyList<SeedSpell>? Spells);
    public sealed record SeedSpell(string? Slug, string? Name, int Level, string? School, bool Ritual, string? CastingTime,
        string? Range, string? Components, string? Duration, bool Concentration, string? DescriptionHtml, string? HigherLevels,
        IReadOnlyList<string>? Classes, string? SuggestedDamageDice, string? SuggestedDamageType, string? AttackKind,
        string? SaveAbility, string? NameEn = null);
}
