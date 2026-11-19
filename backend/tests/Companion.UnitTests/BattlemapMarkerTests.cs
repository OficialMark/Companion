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
}
