using Companion.Domain.Models;

namespace Companion.UnitTests;

public sealed class BattlemapTokenAppearanceTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Accepts_footprint_sizes_between_one_and_five(int size)
    {
        var appearance = BattlemapTokenAppearance.ForParticipant(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        appearance.SetFootprintSize(size);
        Assert.Equal(size, appearance.FootprintSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Rejects_footprint_sizes_outside_one_to_five(int size)
    {
        var appearance = BattlemapTokenAppearance.ForParticipant(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<ArgumentOutOfRangeException>(() => appearance.SetFootprintSize(size));
    }

    [Fact]
    public void Defaults_to_a_single_cell_with_no_conditions()
    {
        var appearance = BattlemapTokenAppearance.ForCombatant(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(1, appearance.FootprintSize);
        Assert.Empty(appearance.StatusConditions);
    }

    [Fact]
    public void Accepts_multiple_distinct_status_conditions()
    {
        var appearance = BattlemapTokenAppearance.ForMarker(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        appearance.SetStatusConditions([StatusCondition.Stunned, StatusCondition.Blinded, StatusCondition.Stunned]);
        Assert.Equal(2, appearance.StatusConditions.Count);
        Assert.Contains(StatusCondition.Stunned, appearance.StatusConditions);
        Assert.Contains(StatusCondition.Blinded, appearance.StatusConditions);
    }

    [Fact]
    public void Clearing_status_conditions_empties_the_list()
    {
        var appearance = BattlemapTokenAppearance.ForParticipant(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        appearance.SetStatusConditions([StatusCondition.Prone]);
        appearance.SetStatusConditions([]);
        Assert.Empty(appearance.StatusConditions);
    }
}
