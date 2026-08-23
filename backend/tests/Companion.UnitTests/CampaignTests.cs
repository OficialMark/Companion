using Companion.Domain.Models;

namespace Companion.UnitTests;

public sealed class CampaignTests
{
    [Fact]
    public void Campaign_trims_valid_name()
    {
        var campaign = new Campaign(Guid.NewGuid(), "ABC234", "  Minas Perdidas  ", DateTimeOffset.UtcNow);
        Assert.Equal("Minas Perdidas", campaign.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Campaign_rejects_blank_name(string name)
    {
        Assert.Throws<ArgumentException>(() =>
            new Campaign(Guid.NewGuid(), "ABC234", name, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Participant_rejects_name_longer_than_limit()
    {
        Assert.Throws<ArgumentException>(() => new Participant(
            Guid.NewGuid(), Guid.NewGuid(), new string('a', 41), ParticipantRole.Player, new byte[32], DateTimeOffset.UtcNow));
    }
}
