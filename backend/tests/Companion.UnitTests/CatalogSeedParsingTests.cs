using System.Text.Json;
using Companion.Application.Catalog;
using Companion.Domain.Catalog;
using Companion.Domain.Characters;

namespace Companion.UnitTests;

/// <summary>Spec 011, FR-019/FR-020: seed rows become valid catalog entries; the real spell seed loads whole.</summary>
public sealed class CatalogSeedParsingTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Html_description_becomes_plain_text_with_paragraphs()
    {
        var text = CatalogSeeder.HtmlToText("<p><strong>Material</strong>: guano &amp; enxofre.</p><p>Um veio <em>brilhante</em>.</p><ul><li>um</li><li>dois</li></ul>");
        Assert.Equal("Material: guano & enxofre.\n\nUm veio brilhante.\n\n• um\n• dois", text);
    }

    [Fact]
    public void Weapon_row_maps_class_damage_type_and_versatile()
    {
        var row = JsonSerializer.Deserialize<CatalogSeeder.SeedItem>("""
            {"slug":"longsword","name":"Espada Longa","nameEn":"Longsword","category":"Weapon","subcategory":"Arma marcial corpo a corpo",
             "rarity":null,"requiresAttunement":false,"weight":1.5,"cost":"15 PO",
             "weapon":{"damageDice":"1d8","damageType":"slashing","versatileDice":"1d10","kind":"melee","properties":["versatile"],"mastery":"sap"},
             "magicBonus":0,"effects":[]}
            """, Json)!;
        var item = new CatalogItem(Guid.NewGuid(), row.Slug!, CatalogSeeder.ToFields(row));

        Assert.Equal(WeaponClass.Martial, item.Template.Weapon!.Class);
        Assert.Equal("cortante", item.Template.Weapon.DamageType);
        Assert.Equal("1d10", item.Template.Weapon.VersatileDice);
        Assert.False(item.RequiresBaseItem);
        Assert.Equal("espada longa longsword", item.SearchKey);
    }

    [Fact]
    public void Generic_magic_weapon_requires_a_base_item_and_maps_effects()
    {
        var generic = JsonSerializer.Deserialize<CatalogSeeder.SeedItem>("""
            {"slug":"weapon-1","name":"Arma +1","category":"Weapon","subcategory":"Arma mágica","rarity":"uncommon","magicBonus":1}
            """, Json)!;
        var ring = JsonSerializer.Deserialize<CatalogSeeder.SeedItem>("""
            {"slug":"ring-of-protection","name":"Anel de Proteção","category":"Gear","rarity":"rare","requiresAttunement":true,
             "effects":[{"type":"armorClass","target":null,"op":"add","value":1},{"type":"saveBonus","target":"all","op":"add","value":1}]}
            """, Json)!;

        var weapon = new CatalogItem(Guid.NewGuid(), "weapon-1", CatalogSeeder.ToFields(generic));
        var protection = new CatalogItem(Guid.NewGuid(), "ring-of-protection", CatalogSeeder.ToFields(ring));

        Assert.True(weapon.RequiresBaseItem);
        Assert.Null(weapon.Template.Weapon);
        Assert.Equal(1, weapon.Template.MagicBonus);
        Assert.Equal("Incomum", weapon.Rarity);
        Assert.Equal([EffectType.ArmorClass, EffectType.SaveBonus], protection.Template.Effects.Select(x => x.Type));
        Assert.Equal("Raro", protection.Rarity);
    }

    [Fact]
    public void The_real_spell_seed_loads_all_361_spells_with_valid_templates()
    {
        var path = Path.Combine(RepoRoot(), "backend", "src", "Companion.Infrastructure", "Persistence", "Seed", "spells.magias5e.json");
        var seed = JsonSerializer.Deserialize<CatalogSeeder.SpellSeedFile>(File.ReadAllBytes(path), Json)!;
        Assert.Null(seed.Attribution);

        var spells = seed.Spells!.Select(x => new CatalogSpell(Guid.NewGuid(), x.Slug!, CatalogSeeder.ToFields(x))).ToArray();

        Assert.Equal(361, spells.Length);
        Assert.Equal([27, 63, 59, 49, 35, 42, 32, 20, 18, 16], Enumerable.Range(0, 10).Select(level => spells.Count(x => x.Level == level)));
        var fireball = spells.Single(x => x.Slug == "bola de fogo");
        Assert.Equal("8d6", fireball.Template.DamageDice);
        Assert.Equal("fogo", fireball.Template.DamageType);
        Assert.Equal(SpellAttackKind.Save, fireball.Template.AttackKind);
        Assert.Equal(AbilityCode.Dex, fireball.Template.SaveAbility);
        Assert.DoesNotContain("<", fireball.Template.Description);
        Assert.Contains("Em Níveis Superiores.", fireball.Template.Description);
        Assert.Contains("mago", fireball.Classes);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docker-compose.yml"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
