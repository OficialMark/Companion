using Companion.Domain.Characters;

namespace Companion.UnitTests;

/// <summary>Spec 011, US3 and FR-011..FR-017: equip limits and item validation.</summary>
public sealed class InventoryRulesTests
{
    private static ItemEntry Item(string name, ItemCategory category, bool equipped, int order = 0)
    {
        var data = new InventoryItemData(name, category, 1, null, null, null, equipped, false, 0,
            category == ItemCategory.Weapon ? new WeaponData("1d8", null, null, WeaponKind.Melee, WeaponClass.Martial, [], null, null, null, null) : null,
            category == ItemCategory.Armor ? new ArmorData(16, ArmorType.Heavy, null, true) : null,
            category == ItemCategory.Shield ? new ShieldData(2) : null,
            []).Validated();
        return new ItemEntry(Guid.NewGuid(), order, data);
    }

    [Fact]
    public void Equipping_a_second_armor_unequips_the_first_and_says_so()
    {
        var chain = Item("Cota de Malha", ItemCategory.Armor, true);
        var plate = Item("Placas", ItemCategory.Armor, false, 1);
        var result = EquipmentRules.SetEquipped([chain, plate], plate.Id, true);

        Assert.Contains(new EquipChange(plate.Id, true), result.Changes);
        Assert.Contains(new EquipChange(chain.Id, false), result.Changes);
        Assert.Equal(["Cota de Malha foi desequipado."], result.Notices);
    }

    [Fact]
    public void Equipping_a_second_shield_unequips_the_first()
    {
        var a = Item("Escudo", ItemCategory.Shield, true);
        var b = Item("Escudo +1", ItemCategory.Shield, false, 1);
        var result = EquipmentRules.SetEquipped([a, b], b.Id, true);
        Assert.Contains(new EquipChange(a.Id, false), result.Changes);
    }

    [Fact]
    public void Armor_and_shield_can_be_equipped_together()
    {
        var armor = Item("Cota de Malha", ItemCategory.Armor, true);
        var shield = Item("Escudo", ItemCategory.Shield, false, 1);
        var result = EquipmentRules.SetEquipped([armor, shield], shield.Id, true);
        Assert.Equal([new EquipChange(shield.Id, true)], result.Changes);
        Assert.Empty(result.Notices);
    }

    [Fact]
    public void Several_weapons_can_be_equipped()
    {
        var sword = Item("Espada", ItemCategory.Weapon, true);
        var dagger = Item("Adaga", ItemCategory.Weapon, true, 1);
        var axe = Item("Machado", ItemCategory.Weapon, false, 2);
        var result = EquipmentRules.SetEquipped([sword, dagger, axe], axe.Id, true);
        Assert.Equal([new EquipChange(axe.Id, true)], result.Changes);
    }

    [Fact]
    public void Potions_and_misc_cannot_be_equipped()
    {
        var potion = Item("Poção de Cura", ItemCategory.Potion, false);
        Assert.Throws<ArgumentException>(() => EquipmentRules.SetEquipped([potion], potion.Id, true));
        Assert.False(potion.Data.Equipped);
    }

    [Fact]
    public void Repair_keeps_only_the_first_armor_by_order()
    {
        var first = Item("Cota", ItemCategory.Armor, true, 0);
        var second = Item("Placas", ItemCategory.Armor, true, 1);
        Assert.Equal([new EquipChange(second.Id, false)], EquipmentRules.Repair([second, first]));
    }

    [Fact]
    public void Weapon_requires_valid_damage_dice()
    {
        var bad = new InventoryItemData("Espada", ItemCategory.Weapon, 1, null, null, null, false, false, 0,
            new WeaponData("1d7", null, null, WeaponKind.Melee, WeaponClass.Martial, [], null, null, null, null), null, null, []);
        Assert.Throws<ArgumentException>(() => bad.Validated());
        var missing = bad with { Weapon = null };
        Assert.Throws<ArgumentException>(() => missing.Validated());
    }

    [Theory]
    [InlineData(EffectType.Ability, "xyz", EffectOp.Add, 1)]
    [InlineData(EffectType.ArmorClass, null, EffectOp.Add, 31)]
    [InlineData(EffectType.AttackBonus, "melee", EffectOp.Min, 3)]
    [InlineData(EffectType.Vision, "radar", EffectOp.Min, 18)]
    [InlineData(EffectType.Resistance, null, EffectOp.Add, null)]
    public void Invalid_effects_are_rejected(EffectType type, string? target, EffectOp op, int? value)
    {
        Assert.Throws<ArgumentException>(() => new ItemEffect(type, target, op, value).Validated());
    }

    [Fact]
    public void Effect_targets_are_normalized()
    {
        Assert.Equal("str", new ItemEffect(EffectType.Ability, " FOR ", EffectOp.Add, 1).Validated().Target);
        Assert.Equal("walk", new ItemEffect(EffectType.Speed, null, EffectOp.Add, 3).Validated().Target);
        Assert.Equal("furtividade", new ItemEffect(EffectType.SkillBonus, "Furtividade", EffectOp.Add, 1).Validated().Target);
    }

    [Fact]
    public void Item_validation_clears_flags_that_do_not_apply()
    {
        var potion = new InventoryItemData(" Poção ", ItemCategory.Potion, 3, null, null, null, true, true, 2, null, null, null, []).Validated();
        Assert.Equal("Poção", potion.Name);
        Assert.False(potion.Equipped);
        Assert.False(potion.Proficient);
        Assert.Equal(0, potion.MagicBonus);
    }
}
