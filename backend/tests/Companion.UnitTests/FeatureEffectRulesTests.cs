using System.Text.Json;
using System.Text.Json.Serialization;
using Companion.Domain.Characters;

namespace Companion.UnitTests;

/// <summary>
/// Spec 014: feature effects count like an equipped item's, with the feature as the visible source; the
/// "melee-one-handed" target (Duelismo) reaches one-handed melee damage only, never two-handed weapons,
/// ranged weapons nor the versatile (two-handed) damage.
/// </summary>
public sealed class FeatureEffectRulesTests
{
    // FOR 17 (+3), like Vashkar.
    private static readonly CharacterSheetData Sheet = CharacterSheetData.Default with { Strength = 17, Level = 5 };

    private static ItemEntry Weapon(string name, string dice, string? versatile = null, WeaponKind kind = WeaponKind.Melee, params string[] properties) =>
        new(Guid.NewGuid(), 0, new InventoryItemData(name, ItemCategory.Weapon, 1, null, null, null, true, true, 0,
            new WeaponData(dice, "contundente", versatile, kind, WeaponClass.Martial, properties, null, null, null, null), null, null, []));

    private static FeatureEntry Feature(string name, params ItemEffect[] effects) =>
        new(Guid.NewGuid(), 0, new CharacterFeatureData(name, "Paladino 2", null, null, 0, FeatureRecovery.None, effects).Validated());

    private static readonly ItemEffect Dueling = new(EffectType.DamageBonus, ItemEffect.MeleeOneHanded, EffectOp.Add, 2);

    private static WeaponRoll Roll(CharacterStats stats, string name) => stats.WeaponRolls.Single(x => x.Name == name);
    private static int Damage(DamageRoll roll) => roll.Parts.Sum(x => x.Value);

    [Fact]
    public void Dueling_adds_two_to_one_handed_melee_damage_with_the_feature_as_source()
    {
        var hammer = Weapon("Martelo de Guerra", "1d8", "1d10", properties: "versatile");
        var duel = Feature("Estilo de Luta: Duelismo", Dueling);

        var stats = CharacterRules.Compute(Sheet, [hammer], [], [duel]);

        var roll = Roll(stats, "Martelo de Guerra");
        Assert.Equal(5, Damage(roll.Damage));
        var part = Assert.Single(roll.Damage.Parts, x => x.Source == "Estilo de Luta: Duelismo");
        Assert.Equal(2, part.Value);
        Assert.Equal(duel.Id, part.ItemId);
        // The versatile damage is the two-handed grip: no Dueling there.
        Assert.Equal(3, Damage(roll.Versatile!));
        // Dueling is damage only: the attack does not change.
        Assert.DoesNotContain(roll.Attack, x => x.Source == "Estilo de Luta: Duelismo");
    }

    [Fact]
    public void Dueling_never_reaches_two_handed_or_ranged_weapons()
    {
        var maul = Weapon("Malho", "2d6", null, WeaponKind.Melee, "heavy", "two-handed");
        var crossbow = Weapon("Besta Leve", "1d8", null, WeaponKind.Ranged, "ammunition");
        var stats = CharacterRules.Compute(Sheet, [maul, crossbow], [], [Feature("Duelismo", Dueling)]);

        Assert.Equal(3, Damage(Roll(stats, "Malho").Damage));
        Assert.DoesNotContain(Roll(stats, "Besta Leve").Damage.Parts, x => x.Source == "Duelismo");
    }

    [Fact]
    public void Without_the_feature_nothing_changes()
    {
        var hammer = Weapon("Martelo de Guerra", "1d8", "1d10", properties: "versatile");
        Assert.Equal(3, Damage(Roll(CharacterRules.Compute(Sheet, [hammer], []), "Martelo de Guerra").Damage));
        Assert.Equal(3, Damage(Roll(CharacterRules.Compute(Sheet, [hammer], [], [Feature("Sem efeito")]), "Martelo de Guerra").Damage));
    }

    [Fact]
    public void One_handed_attack_bonus_and_all_targets_are_not_counted_twice()
    {
        var hammer = Weapon("Martelo", "1d8");
        var stats = CharacterRules.Compute(Sheet, [hammer], [], [
            Feature("Precisão", new ItemEffect(EffectType.AttackBonus, ItemEffect.MeleeOneHanded, EffectOp.Add, 1)),
            Feature("Bênção Constante", new ItemEffect(EffectType.AttackBonus, "all", EffectOp.Add, 1))
        ]);

        var attack = Roll(stats, "Martelo").Attack;
        Assert.Single(attack, x => x.Source == "Precisão");
        Assert.Single(attack, x => x.Source == "Bênção Constante");
    }

    [Fact]
    public void One_handed_attack_bonus_skips_two_handed_and_ranged_weapons()
    {
        var precision = Feature("Precisão", new ItemEffect(EffectType.AttackBonus, ItemEffect.MeleeOneHanded, EffectOp.Add, 1));
        var stats = CharacterRules.Compute(Sheet, [
            Weapon("Malho", "2d6", null, WeaponKind.Melee, "heavy", "two-handed"),
            Weapon("Besta Leve", "1d8", null, WeaponKind.Ranged, "ammunition")
        ], [], [precision]);

        Assert.DoesNotContain(Roll(stats, "Malho").Attack, x => x.Source == "Precisão");
        Assert.DoesNotContain(Roll(stats, "Besta Leve").Attack, x => x.Source == "Precisão");
    }

    [Fact]
    public void Feature_effects_reach_initiative_hit_points_speed_ability_and_text()
    {
        var stats = CharacterRules.Compute(Sheet, [], [], [
            Feature("Alerta", new ItemEffect(EffectType.Initiative, null, EffectOp.Add, 5)),
            Feature("Vigoroso", new ItemEffect(EffectType.MaxHp, null, EffectOp.Add, 10)),
            Feature("Mobilidade", new ItemEffect(EffectType.Speed, "walk", EffectOp.Add, 3)),
            Feature("Resiliente", new ItemEffect(EffectType.Ability, "con", EffectOp.Add, 2)),
            Feature("Ancestralidade", new ItemEffect(EffectType.Resistance, "fogo", EffectOp.Add, null))
        ]);

        Assert.Equal(5, stats.Initiative.Value);
        Assert.Contains(stats.Initiative.Parts, x => x.Source == "Alerta");
        // Base 10 HP, +10 from Vigoroso, +1 × level 5 from the CON modifier change (10 → 12).
        Assert.Equal(10 + 10 + 5, stats.MaxHitPoints.Value);
        Assert.Contains(stats.MaxHitPoints.Parts, x => x.Source == "Constituição alterada por item ou característica");
        Assert.Equal(12m, stats.Speed["walk"].Value);
        Assert.Equal(12, stats.Abilities["con"].Final);
        Assert.Contains("fogo", stats.Resistances);
    }

    [Fact]
    public void Editing_without_an_effects_list_keeps_the_stored_effects_and_an_empty_list_clears_them()
    {
        // A tab opened before spec 014 sends no "effects": the stored Dueling must survive a text edit.
        var feature = new CharacterFeature(Guid.NewGuid(), Guid.NewGuid(), 0,
            new CharacterFeatureData("Duelismo", null, null, null, 0, FeatureRecovery.None, [Dueling]), DateTimeOffset.UnixEpoch);

        feature.Update(feature.Data with { Description = "Texto novo.", Effects = null });
        Assert.Equal("Texto novo.", feature.Data.Description);
        Assert.Single(feature.Data.EffectList());

        feature.Update(feature.Data with { Effects = [] });
        Assert.Empty(feature.Data.EffectList());
    }

    [Fact]
    public void Feature_effects_reach_armor_class_saves_and_skills()
    {
        var stats = CharacterRules.Compute(Sheet, [], [], [
            Feature("Estilo de Luta: Defesa", new ItemEffect(EffectType.ArmorClass, null, EffectOp.Add, 1)),
            Feature("Aura de Proteção", new ItemEffect(EffectType.SaveBonus, "all", EffectOp.Add, 4)),
            Feature("Treino", new ItemEffect(EffectType.SkillBonus, "atletismo", EffectOp.Add, 2))
        ]);

        Assert.Equal(11, stats.ArmorClass.Value);
        Assert.Contains(stats.ArmorClass.Parts, x => x.Source == "Estilo de Luta: Defesa");
        Assert.Equal(3 + 4, stats.SavingThrows["str"].Bonus);
        Assert.Equal(3 + 2, stats.Skills["atletismo"].Bonus);
    }

    [Fact]
    public void A_repeated_effect_from_an_item_and_a_feature_of_the_same_name_counts_once()
    {
        var ring = new ItemEntry(Guid.NewGuid(), 0, new InventoryItemData("Proteção", ItemCategory.Gear, 1, null, null, null, true, false, 0,
            null, null, null, [new ItemEffect(EffectType.ArmorClass, null, EffectOp.Add, 1)]));
        var feature = Feature("Proteção", new ItemEffect(EffectType.ArmorClass, null, EffectOp.Add, 1));

        var stats = CharacterRules.Compute(Sheet, [ring], [], [feature]);

        Assert.Equal(11, stats.ArmorClass.Value);
        var suppressed = Assert.Single(stats.SuppressedEffects);
        Assert.Equal(feature.Id, suppressed.ItemId);
        Assert.Equal(ring.Id, suppressed.KeptItemId);
    }

    [Fact]
    public void A_feature_saved_before_spec_014_reads_as_having_no_effects()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
        var old = JsonSerializer.Deserialize<CharacterFeatureData>(
            """{"name":"Sentido Divino","source":"Paladino 1","description":null,"maxUses":5,"currentUses":5,"recovery":"LongRest","hasCounter":true}""",
            options)!;

        Assert.Empty(old.EffectList());
        Assert.Empty(old.Validated().EffectList());
        var stats = CharacterRules.Compute(Sheet, [], [], [new FeatureEntry(Guid.NewGuid(), 0, old)]);
        Assert.Equal(10, stats.ArmorClass.Value);
    }

    [Fact]
    public void Feature_effects_are_validated_and_limited_to_twenty()
    {
        var bad = new CharacterFeatureData("X", null, null, null, 0, FeatureRecovery.None,
            [new ItemEffect(EffectType.DamageBonus, "duas maos", EffectOp.Add, 2)]);
        Assert.Throws<ArgumentException>(() => bad.Validated());

        var many = new CharacterFeatureData("X", null, null, null, 0, FeatureRecovery.None,
            Enumerable.Range(0, 21).Select(i => new ItemEffect(EffectType.Text, $"nota {i}", EffectOp.Add, null)).ToArray());
        Assert.Contains("até 20 efeitos", Assert.Throws<ArgumentException>(() => many.Validated()).Message);

        // The stored JSON keeps the list and never gains computed fields.
        var json = JsonSerializer.Serialize(new CharacterFeatureData("Duelismo", null, null, null, 0, FeatureRecovery.None, [Dueling]).Validated(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } });
        Assert.Contains("\"effects\":[", json);
        Assert.DoesNotContain("effectList", json, StringComparison.OrdinalIgnoreCase);
    }
}
