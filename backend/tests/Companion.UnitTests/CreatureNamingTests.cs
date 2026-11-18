using Companion.Application.Creatures;

namespace Companion.UnitTests;

public sealed class CreatureNamingTests
{
    [Fact]
    public void Single_creature_without_namesake_keeps_the_plain_name() =>
        Assert.Equal(["Goblin"], CreatureNaming.Next("Goblin", ["Ayla", "Orc"], 1, 60));

    [Fact]
    public void Several_creatures_are_numbered_from_one() =>
        Assert.Equal(["Goblin 1", "Goblin 2", "Goblin 3"], CreatureNaming.Next("Goblin", [], 3, 60));

    [Fact]
    public void Numbers_fill_gaps_and_never_repeat() =>
        Assert.Equal(["Goblin 2", "Goblin 4"], CreatureNaming.Next("Goblin", ["Goblin 1", "goblin 3"], 2, 60));

    [Fact]
    public void A_plain_namesake_counts_as_number_one() =>
        Assert.Equal(["Goblin 2"], CreatureNaming.Next("Goblin", ["Goblin"], 1, 60));

    [Fact]
    public void Unrelated_names_sharing_a_prefix_are_ignored() =>
        Assert.Equal(["Goblin"], CreatureNaming.Next("Goblin", ["Goblin Chefe", "Goblinoide 2"], 1, 60));

    [Fact]
    public void Long_names_are_trimmed_to_fit_with_the_number()
    {
        var names = CreatureNaming.Next(new string('a', 70), [], 2, 60);
        Assert.All(names, name => Assert.True(name.Length <= 60));
        Assert.EndsWith(" 2", names[1]);
    }
}
