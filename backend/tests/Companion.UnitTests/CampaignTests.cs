using Companion.Domain.Models;

namespace Companion.UnitTests;

public sealed class CampaignTests
{
    [Fact]
    public void Campaign_trims_valid_name()
    {
        var campaign = new Campaign(Guid.NewGuid(), "  Minas Perdidas  ", DateTimeOffset.UtcNow);
        Assert.Equal("Minas Perdidas", campaign.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Campaign_rejects_blank_name(string name)
    {
        Assert.Throws<ArgumentException>(() =>
            new Campaign(Guid.NewGuid(), name, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Participant_rejects_name_longer_than_limit()
    {
        Assert.Throws<ArgumentException>(() => new Participant(
            Guid.NewGuid(), Guid.NewGuid(), new string('a', 41), ParticipantRole.Player, new byte[32], DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData("0000")]
    [InlineData("9876")]
    public void Campaign_access_code_accepts_exactly_four_digits(string code)
    {
        var access = new CampaignAccessCode(code, Guid.NewGuid(), ParticipantRole.Player);
        Assert.Equal(code, access.Code);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("12345")]
    [InlineData("12A4")]
    public void Campaign_access_code_rejects_invalid_format(string code) =>
        Assert.Throws<ArgumentException>(() =>
            new CampaignAccessCode(code, Guid.NewGuid(), ParticipantRole.Player));
}
