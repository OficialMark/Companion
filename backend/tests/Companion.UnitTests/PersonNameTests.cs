using Companion.Domain.Characters;
using Companion.Domain.Models;

namespace Companion.UnitTests;

public sealed class PersonNameTests
{
    [Theory]
    [InlineData("loren b. vonn neuman", "Loren B. Vonn Neuman")]
    [InlineData("LOREN B. VONN NEUMAN", "Loren B. Vonn Neuman")]
    [InlineData("loren b. VONN neuman", "Loren B. Vonn Neuman")]
    [InlineData("  loren  ", "Loren")]
    [InlineData("loren    b.\t vonn\nneuman", "Loren B. Vonn Neuman")]
    [InlineData("jean-luc", "Jean-Luc")]
    [InlineData("JEAN-LUC picard", "Jean-Luc Picard")]
    [InlineData("d'artagnan", "D'artagnan")]
    [InlineData("D'ARTAGNAN", "D'artagnan")]
    [InlineData("joão da silva", "João Da Silva")]
    [InlineData("ÉLIS ÂNGELA", "Élis Ângela")]
    [InlineData("çeci", "Çeci")]
    [InlineData("3rd", "3rd")]
    [InlineData("(loren)", "(Loren)")]
    [InlineData("a", "A")]
    public void Normalize_applies_the_capitalization_rule(string input, string expected) =>
        Assert.Equal(expected, PersonName.Normalize(input));

    [Fact]
    public void Normalize_composes_combining_accents_so_both_spellings_match()
    {
        var decomposed = "joão";
        Assert.Equal("João", PersonName.Normalize(decomposed));
        Assert.Equal(PersonName.Key("João"), PersonName.Key(decomposed));
    }

    [Theory]
    [InlineData("loren b. vonn neuman")]
    [InlineData("  JEAN - luc  ")]
    [InlineData("d'artagnan")]
    public void Normalize_is_idempotent(string input)
    {
        var once = PersonName.Normalize(input);
        Assert.Equal(once, PersonName.Normalize(once));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Normalize_rejects_empty_names(string? input)
    {
        var exception = Assert.Throws<ArgumentException>(() => PersonName.Normalize(input));
        Assert.Equal("O nome não pode ficar vazio.", exception.Message);
    }

    [Fact]
    public void Normalize_rejects_names_longer_than_40_characters_after_normalization()
    {
        var exception = Assert.Throws<ArgumentException>(() => PersonName.Normalize(new string('a', 41)));
        Assert.Equal("O nome deve ter no máximo 40 caracteres.", exception.Message);
    }

    [Fact]
    public void Limit_applies_after_collapsing_spaces()
    {
        // 44 caracteres digitados, 40 depois de remover as pontas e os espaços repetidos.
        var input = "  " + new string('a', 19) + "   " + new string('b', 20);
        var normalized = PersonName.Normalize(input);
        Assert.Equal(40, normalized.Length);
    }

    [Fact]
    public void Key_ignores_case_but_not_accents()
    {
        Assert.Equal("LOREN B. VONN NEUMAN", PersonName.Key("loren b. vonn neuman"));
        Assert.Equal(PersonName.Key("loren"), PersonName.Key("LOREN"));
        Assert.Equal(PersonName.Key("  loren  "), PersonName.Key("Loren"));
        Assert.NotEqual(PersonName.Key("Joao"), PersonName.Key("João"));
    }

    [Fact]
    public void New_participant_stores_the_normalized_name_and_owns_its_key()
    {
        var participant = new Participant(Guid.NewGuid(), Guid.NewGuid(), "  loren b. VONN neuman ",
            ParticipantRole.Player, new byte[32], DateTimeOffset.UtcNow);
        Assert.Equal("Loren B. Vonn Neuman", participant.DisplayName);
        Assert.Equal("LOREN B. VONN NEUMAN", participant.NameKey);
    }

    [Fact]
    public void Rename_without_ownership_clears_the_key()
    {
        var participant = new Participant(Guid.NewGuid(), Guid.NewGuid(), "loren", ParticipantRole.Player, new byte[32], DateTimeOffset.UtcNow);
        participant.Rename("LOREN", ownsName: false);
        Assert.Equal("Loren", participant.DisplayName);
        Assert.Null(participant.NameKey);
    }

    [Fact]
    public void Session_requires_a_32_byte_hash_and_tracks_staleness_hourly()
    {
        Assert.Throws<ArgumentException>(() => new ParticipantSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new byte[31], DateTimeOffset.UtcNow));
        var created = new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
        var session = new ParticipantSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new byte[32], created);
        Assert.Equal(created, session.LastSeenAt);
        Assert.False(session.IsLastSeenStale(created.AddMinutes(59)));
        Assert.True(session.IsLastSeenStale(created.AddHours(1)));
    }
}
