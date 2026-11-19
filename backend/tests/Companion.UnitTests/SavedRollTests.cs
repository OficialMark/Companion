using Companion.Domain.Characters;

namespace Companion.UnitTests;

/// <summary>Spec 011, US6 and FR-033..FR-035: saved roll validation.</summary>
public sealed class SavedRollTests
{
    private static SavedRollData Valid() => new("Ataque com espada", "lucide:sword", RollKind.Attack,
        new Dictionary<string, int> { ["d20"] = 1 }, AbilityCode.Str, null, null, Guid.NewGuid(), null, [], AdvantageMode.None, 1);

    [Fact]
    public void Valid_roll_is_normalized()
    {
        var roll = (Valid() with
        {
            Name = "  Ataque  ",
            Dice = new Dictionary<string, int> { ["D20"] = 1, ["d6"] = 0 },
            ExtraModifiers = [new RollModifier("Bênção (1d4)", 1)]
        }).Validated();
        Assert.Equal("Ataque", roll.Name);
        Assert.Equal(new Dictionary<string, int> { ["d20"] = 1 }, roll.Dice);
        Assert.Equal("Bênção 1d4", roll.ExtraModifiers[0].Label);
    }

    [Fact]
    public void Icon_outside_the_list_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => (Valid() with { Icon = "mdi:sword" }).Validated());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Repetitions_must_be_between_1_and_20(int repetitions)
    {
        Assert.Throws<ArgumentException>(() => (Valid() with { Repetitions = repetitions }).Validated());
    }

    [Fact]
    public void Unknown_die_and_too_many_dice_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => (Valid() with { Dice = new Dictionary<string, int> { ["d7"] = 1 } }).Validated());
        Assert.Throws<ArgumentException>(() => (Valid() with { Dice = new Dictionary<string, int> { ["d6"] = 21 } }).Validated());
    }

    [Fact]
    public void Skill_and_save_are_exclusive_and_only_for_checks()
    {
        var check = Valid() with { Kind = RollKind.Check, InventoryItemId = null };
        Assert.Equal("furtividade", (check with { Skill = "Furtividade" }).Validated().Skill);
        Assert.Throws<ArgumentException>(() => (check with { Skill = "furtividade", SavingThrow = AbilityCode.Dex }).Validated());
        Assert.Throws<ArgumentException>(() => (Valid() with { Skill = "furtividade" }).Validated());
    }

    [Fact]
    public void Weapon_and_spell_are_exclusive()
    {
        Assert.Throws<ArgumentException>(() => (Valid() with { CharacterSpellId = Guid.NewGuid() }).Validated());
    }

    [Fact]
    public void Icon_list_has_sixteen_unique_lucide_icons()
    {
        Assert.Equal(16, SavedRollIcons.All.Distinct().Count());
        Assert.All(SavedRollIcons.All, icon => Assert.StartsWith("lucide:", icon));
    }
}
