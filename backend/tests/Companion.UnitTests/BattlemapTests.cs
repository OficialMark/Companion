using Companion.Domain.Models;

namespace Companion.UnitTests;

public sealed class BattlemapTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47];

    [Fact]
    public void Create_rejects_grid_outside_supported_range()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Battlemap(Guid.NewGuid(), Guid.NewGuid(), Png, "image/png", "map.png", 0, 10, true, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Battlemap(Guid.NewGuid(), Guid.NewGuid(), Png, "image/png", "map.png", 10, 101, true, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Replace_changes_image_revision_and_dimensions()
    {
        var battlemap = new Battlemap(
            Guid.NewGuid(), Guid.NewGuid(), Png, "image/png", "map.png", 20, 15, true, DateTimeOffset.UtcNow);
        var previous = battlemap.ImageRevision;

        battlemap.Replace([0xFF, 0xD8, 0xFF], "image/jpeg", "next.jpg", 10, 8, false, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.NotEqual(previous, battlemap.ImageRevision);
        Assert.Equal(10, battlemap.Columns);
        Assert.Equal(8, battlemap.Rows);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(19, 14)]
    public void Validate_coordinate_accepts_cells_inside_grid(int column, int row)
    {
        var battlemap = new Battlemap(
            Guid.NewGuid(), Guid.NewGuid(), Png, "image/png", "map.png", 20, 15, true, DateTimeOffset.UtcNow);
        battlemap.ValidateCoordinate(column, row);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(20, 0)]
    [InlineData(0, 15)]
    public void Validate_coordinate_rejects_cells_outside_grid(int column, int row)
    {
        var battlemap = new Battlemap(
            Guid.NewGuid(), Guid.NewGuid(), Png, "image/png", "map.png", 20, 15, true, DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentOutOfRangeException>(() => battlemap.ValidateCoordinate(column, row));
    }

    [Fact]
    public void Token_position_moves_between_valid_cells()
    {
        var position = new BattlemapTokenPosition(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 2, DateTimeOffset.UtcNow);
        position.Move(4, 5, DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.Equal(4, position.Column);
        Assert.Equal(5, position.Row);
    }
}
