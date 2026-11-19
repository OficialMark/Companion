using Companion.Domain.Models;

namespace Companion.UnitTests;

public sealed class BattlemapFogTests
{
    private static Battlemap Map(int columns, int rows) =>
        new(Guid.NewGuid(), Guid.NewGuid(), [1, 2, 3], "image/png", "map.png", columns, rows, true, DateTimeOffset.UtcNow);

    private static byte[] MaskWith(int columns, int rows, params (int Column, int Row)[] cells)
    {
        var mask = new byte[Battlemap.FogMaskLengthFor(columns, rows)];
        foreach (var (column, row) in cells)
        {
            var index = row * columns + column;
            mask[index / 8] |= (byte)(1 << (index % 8));
        }
        return mask;
    }

    [Fact]
    public void New_battlemap_is_fully_revealed()
    {
        var map = Map(10, 10);
        Assert.Null(map.FogMask);
        Assert.False(map.IsCellFogged(0, 0));
    }

    [Fact]
    public void SetFog_marks_exact_cells_row_major()
    {
        var map = Map(10, 3);
        map.SetFog(MaskWith(10, 3, (9, 0), (0, 1), (4, 2)));

        Assert.True(map.IsCellFogged(9, 0));
        Assert.True(map.IsCellFogged(0, 1));
        Assert.True(map.IsCellFogged(4, 2));
        Assert.False(map.IsCellFogged(8, 0));
        Assert.False(map.IsCellFogged(1, 1));
        Assert.False(map.IsCellFogged(-1, 0));
        Assert.False(map.IsCellFogged(10, 0));
    }

    [Fact]
    public void SetFog_rejects_mask_of_wrong_length()
    {
        var map = Map(10, 10);
        Assert.Throws<ArgumentException>(() => map.SetFog(new byte[12]));
        Assert.Throws<ArgumentException>(() => map.SetFog(new byte[14]));
    }

    [Fact]
    public void SetFog_normalizes_empty_mask_and_padding_bits()
    {
        var map = Map(3, 3); // 9 cells -> 2 bytes, 7 padding bits
        map.SetFog([0, 0]);
        Assert.Null(map.FogMask);

        map.SetFog([0, 0b1111_1110]); // only padding bits set
        Assert.Null(map.FogMask);

        map.SetFog([0, 0b1111_1111]); // cell 8 plus padding
        Assert.Equal(new byte[] { 0, 1 }, map.FogMask);
        Assert.True(map.IsCellFogged(2, 2));
    }

    [Fact]
    public void IsAreaFogged_requires_every_in_grid_cell_of_the_footprint()
    {
        var map = Map(5, 5);
        map.SetFog(MaskWith(5, 5, (1, 1), (2, 1), (1, 2)));

        Assert.True(map.IsAreaFogged(1, 1, 1));
        Assert.False(map.IsAreaFogged(1, 1, 2)); // (2,2) is revealed
        map.SetFog(MaskWith(5, 5, (1, 1), (2, 1), (1, 2), (2, 2)));
        Assert.True(map.IsAreaFogged(1, 1, 2));
    }

    [Fact]
    public void IsAreaFogged_clips_footprint_to_the_grid()
    {
        var map = Map(5, 5);
        map.SetFog(MaskWith(5, 5, (4, 4)));
        Assert.True(map.IsAreaFogged(4, 4, 3)); // only (4,4) is inside the grid
    }

    [Fact]
    public void Replace_with_another_grid_discards_fog_but_same_grid_keeps_it()
    {
        var map = Map(5, 5);
        map.SetFog(MaskWith(5, 5, (0, 0)));

        map.Replace([4, 5, 6], "image/png", "new.png", 5, 5, true, DateTimeOffset.UtcNow);
        Assert.True(map.IsCellFogged(0, 0));

        map.Replace([4, 5, 6], "image/png", "new.png", 6, 5, true, DateTimeOffset.UtcNow);
        Assert.Null(map.FogMask);
    }

    [Fact]
    public void Max_mask_fits_the_largest_grid()
    {
        Assert.Equal(1250, Battlemap.MaxFogMaskBytes);
        var map = Map(100, 100);
        map.SetFog(Enumerable.Repeat((byte)0xFF, 1250).ToArray());
        Assert.True(map.IsCellFogged(99, 99));
    }
}
