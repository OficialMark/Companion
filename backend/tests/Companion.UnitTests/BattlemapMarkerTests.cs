using Companion.Domain.Models;

namespace Companion.UnitTests;

public sealed class BattlemapMarkerTests
{
    [Fact]
    public void Creates_with_trimmed_label()
    {
        var marker = new BattlemapMarker(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "  Lobo invocado  ", DateTimeOffset.UtcNow);
        Assert.Equal("Lobo invocado", marker.Label);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_empty_label(string label)
    {
        Assert.Throws<ArgumentException>(() =>
            new BattlemapMarker(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), label, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Rejects_label_longer_than_forty_characters()
    {
        var label = new string('a', 41);
        Assert.Throws<ArgumentException>(() =>
            new BattlemapMarker(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), label, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Keeps_the_compendium_source()
    {
        var creatureId = Guid.NewGuid();
        var marker = new BattlemapMarker(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Lobo", DateTimeOffset.UtcNow, creatureId: creatureId);
        Assert.Equal(creatureId, marker.CreatureId);
        Assert.Null(marker.CatalogItemId);
    }

    [Fact]
    public void Rejects_both_sources_at_once()
    {
        Assert.Throws<ArgumentException>(() => new BattlemapMarker(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Baú",
            DateTimeOffset.UtcNow, creatureId: Guid.NewGuid(), catalogItemId: Guid.NewGuid()));
    }

    [Theory]
    [InlineData("Lobo", "Lobo")]
    [InlineData("  Corda de cânhamo (15 m)  ", "Corda de cânhamo (15 m)")]
    // 52 chars: cut at the last space inside the first 40.
    [InlineData("Poção de Resistência a Dano Necrótico Superior Extra", "Poção de Resistência a Dano Necrótico")]
    // No space in the second half: hard cut at 40.
    [InlineData("Supercalifragilisticexpialidociousmentequalquercoisa", "Supercalifragilisticexpialidociousmenteq")]
    public void Label_from_a_catalog_name_fits_forty_characters(string name, string expected)
    {
        var label = BattlemapMarker.LabelFrom(name);
        Assert.Equal(expected, label);
        Assert.True(label.Length <= BattlemapMarker.MaxLabelLength);
    }
}
