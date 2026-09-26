using Companion.Domain.Models;

namespace Companion.UnitTests;

public sealed class AttackAreaTests
{
    [Theory]
    [InlineData(AttackAreaShape.Circle)]
    [InlineData(AttackAreaShape.Square)]
    public void Circle_and_square_accept_radius_without_width_or_direction(AttackAreaShape shape)
    {
        var area = AttackArea.ForParticipant(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), shape, radius: 3, width: null, directionDegrees: null, DateTimeOffset.UtcNow);

        Assert.Equal(3, area.Radius);
        Assert.Null(area.Width);
        Assert.Null(area.DirectionDegrees);
    }

    [Theory]
    [InlineData(AttackAreaShape.Circle)]
    [InlineData(AttackAreaShape.Square)]
    public void Circle_and_square_reject_width_or_direction(AttackAreaShape shape)
    {
        Assert.Throws<ArgumentException>(() => AttackArea.ForParticipant(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), shape, radius: 3, width: 2, directionDegrees: null, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => AttackArea.ForParticipant(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), shape, radius: 3, width: null, directionDegrees: 90, DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(AttackAreaShape.Cone)]
    [InlineData(AttackAreaShape.Cylinder)]
    public void Cone_and_cylinder_require_width_and_direction(AttackAreaShape shape)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AttackArea.ForParticipant(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), shape, radius: 4, width: null, directionDegrees: 90, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => AttackArea.ForParticipant(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), shape, radius: 4, width: 2, directionDegrees: null, DateTimeOffset.UtcNow));

        var area = AttackArea.ForParticipant(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), shape, radius: 4, width: 2, directionDegrees: 90, DateTimeOffset.UtcNow);
        Assert.Equal(2, area.Width);
        Assert.Equal(90, area.DirectionDegrees);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Radius_outside_supported_range_is_rejected(int radius)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AttackArea.ForParticipant(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), AttackAreaShape.Circle, radius, width: null, directionDegrees: null, DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(360)]
    public void Direction_outside_0_to_359_is_rejected(int direction)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AttackArea.ForParticipant(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), AttackAreaShape.Cone, radius: 4, width: 2, directionDegrees: direction, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Update_changes_shape_and_geometry()
    {
        var area = AttackArea.ForCombatant(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), AttackAreaShape.Circle, radius: 2, width: null, directionDegrees: null, DateTimeOffset.UtcNow);

        area.Update(AttackAreaShape.Cone, radius: 5, width: 3, directionDegrees: 180, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(AttackAreaShape.Cone, area.Shape);
        Assert.Equal(5, area.Radius);
        Assert.Equal(3, area.Width);
        Assert.Equal(180, area.DirectionDegrees);
    }

    [Fact]
    public void ForParticipant_and_ForCombatant_set_mutually_exclusive_owner()
    {
        var participantArea = AttackArea.ForParticipant(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), AttackAreaShape.Square, radius: 1, width: null, directionDegrees: null, DateTimeOffset.UtcNow);
        Assert.NotNull(participantArea.ParticipantId);
        Assert.Null(participantArea.CombatantId);

        var combatantArea = AttackArea.ForCombatant(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), AttackAreaShape.Square, radius: 1, width: null, directionDegrees: null, DateTimeOffset.UtcNow);
        Assert.Null(combatantArea.ParticipantId);
        Assert.NotNull(combatantArea.CombatantId);
    }
}
