using Companion.Application.Creatures;
using Companion.Domain.Models;

namespace Companion.UnitTests;

public sealed class CreatureTests
{
    internal static CreatureStats Stats(string name = "Goblin", CreatureSize size = CreatureSize.Small, int dex = 14,
        int? initiative = null, string cr = "1/4") => new(
        name, "Goblin", size, "humanoide (goblinoide)", "Neutro e mau", 15, "armadura de couro", 7, "2d6", "9 m", initiative,
        8, dex, 10, 10, 8, 8, null, "Furtividade +6", null, null, null, null, "visão no escuro 18 m", "Comum, Goblin",
        cr, 50, 2, [new CreatureAction("Fuga Ágil", "Desengajar como ação bônus.")],
        [new CreatureAction("Cimitarra", "Ataque com Arma Corpo a Corpo: +4 para acertar. 5 (1d6 + 2) de dano cortante.")],
        [], [], [], null);

    [Theory]
    [InlineData(1, -5)]
    [InlineData(8, -1)]
    [InlineData(9, -1)]
    [InlineData(10, 0)]
    [InlineData(11, 0)]
    [InlineData(14, 2)]
    [InlineData(30, 10)]
    public void Modifier_rounds_down(int score, int expected) => Assert.Equal(expected, Creature.Modifier(score));

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1/8", 0.125)]
    [InlineData("1/4", 0.25)]
    [InlineData("1/2", 0.5)]
    [InlineData("17", 17)]
    public void Challenge_rating_is_parsed_for_sorting(string cr, double expected) =>
        Assert.Equal((decimal)expected, Creature.ParseChallenge(cr));

    [Theory]
    [InlineData(CreatureSize.Tiny, 1)]
    [InlineData(CreatureSize.Medium, 1)]
    [InlineData(CreatureSize.Large, 2)]
    [InlineData(CreatureSize.Huge, 3)]
    [InlineData(CreatureSize.Gargantuan, 4)]
    public void Footprint_follows_size(CreatureSize size, int footprint) =>
        Assert.Equal(footprint, Creature.ForCampaign(Guid.NewGuid(), Guid.NewGuid(), Stats(size: size), DateTimeOffset.UtcNow).FootprintSize);

    [Fact]
    public void Initiative_uses_the_stat_block_value_else_dex_modifier()
    {
        Assert.Equal(2, Creature.ForCampaign(Guid.NewGuid(), Guid.NewGuid(), Stats(dex: 14), DateTimeOffset.UtcNow).InitiativeModifier);
        Assert.Equal(7, Creature.ForCampaign(Guid.NewGuid(), Guid.NewGuid(), Stats(dex: 9, initiative: 7), DateTimeOffset.UtcNow).InitiativeModifier);
    }

    [Fact]
    public void Search_key_ignores_case_and_accents()
    {
        var creature = Creature.ForCampaign(Guid.NewGuid(), Guid.NewGuid(), Stats(name: "Águia Gigante"), DateTimeOffset.UtcNow);
        Assert.Equal("aguia gigante goblin", creature.SearchKey);
        Assert.Equal("aguia", Creature.Normalize("ÁGUIA"));
    }

    [Fact]
    public void Campaign_copy_keeps_stats_and_source_and_points_to_the_original()
    {
        var original = Creature.ForCompendium(Guid.NewGuid(), "goblin", "SRD 5.1", "https://pocketdm.com.br/monstros/goblin",
            "https://pocketdm.com.br/api/token/MM/Goblin.webp", Stats(), DateTimeOffset.UtcNow);
        var campaignId = Guid.NewGuid();
        var copy = original.CreateCampaignCopy(Guid.NewGuid(), campaignId, DateTimeOffset.UtcNow);

        Assert.Equal(original.Id, copy.BaseCreatureId);
        Assert.Equal(campaignId, copy.CampaignId);
        Assert.Null(copy.Slug);
        Assert.Equal(original.ImageSourceUrl, copy.ImageSourceUrl);
        Assert.Equal(original.ToStats().Actions, copy.ToStats().Actions);
        Assert.Throws<InvalidOperationException>(() => copy.CreateCampaignCopy(Guid.NewGuid(), campaignId, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Notes_only_exist_on_campaign_creatures()
    {
        var original = Creature.ForCompendium(Guid.NewGuid(), "goblin", "SRD 5.1", null, null, Stats(), DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => original.SetNotes("x", DateTimeOffset.UtcNow));
        var own = Creature.ForCampaign(Guid.NewGuid(), Guid.NewGuid(), Stats(), DateTimeOffset.UtcNow);
        own.SetNotes("  lidera a patrulha  ", DateTimeOffset.UtcNow);
        Assert.Equal("lidera a patrulha", own.Notes);
        Assert.Throws<ArgumentException>(() => own.SetNotes(new string('x', 4001), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Invalid_stats_are_rejected()
    {
        Assert.ThrowsAny<ArgumentException>(() => Creature.ForCampaign(Guid.NewGuid(), Guid.NewGuid(), Stats(name: " "), DateTimeOffset.UtcNow));
        Assert.ThrowsAny<ArgumentException>(() => Creature.ForCampaign(Guid.NewGuid(), Guid.NewGuid(), Stats(dex: 31), DateTimeOffset.UtcNow));
        Assert.ThrowsAny<ArgumentException>(() => Creature.ForCampaign(Guid.NewGuid(), Guid.NewGuid(), Stats(cr: "abc"), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Removing_the_image_drops_the_remote_source_so_it_is_not_fetched_back()
    {
        var copy = Creature.ForCompendium(Guid.NewGuid(), "goblin", "SRD 5.1", null, "https://pocketdm.com.br/x.webp", Stats(), DateTimeOffset.UtcNow)
            .CreateCampaignCopy(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        copy.SetImage(null, DateTimeOffset.UtcNow);
        Assert.Null(copy.ImageSourceUrl);
        Assert.Null(copy.ImageRevision);
    }

    [Theory]
    [InlineData("Abocanhador Matraqueante (Gibbering Mouther)", "Gibbering Mouther", "Abocanhador Matraqueante")]
    [InlineData("Goblin", "Goblin", "Goblin")]
    [InlineData("Aboleth", null, "Aboleth")]
    public void Seed_display_name_drops_the_english_suffix(string name, string? nameEn, string expected) =>
        Assert.Equal(expected, CompendiumSeeder.DisplayName(name, nameEn));

    [Fact]
    public void Roll_preset_is_validated()
    {
        var appearance = BattlemapTokenAppearance.ForCombatant(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        appearance.SetRollPreset(2, 6, "dex", -3);
        Assert.Equal((2, 6, "dex", -3), (appearance.LastRollCount, appearance.LastRollDie, appearance.LastRollAbility, appearance.LastRollBonus));
        appearance.SetRollPreset(1, 20, null, 0);
        Assert.Null(appearance.LastRollAbility);
        Assert.Throws<ArgumentOutOfRangeException>(() => appearance.SetRollPreset(1, 7, null, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => appearance.SetRollPreset(0, 20, null, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => appearance.SetRollPreset(1, 20, "luck", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => appearance.SetRollPreset(1, 20, null, 21));
    }

    [Fact]
    public void Dice_roll_label_is_trimmed_and_bounded()
    {
        Assert.Equal("Goblin 2", new DiceRoll(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "1d20", "[]", 0, 1, DateTimeOffset.UtcNow, null, "  Goblin 2 ").Label);
        Assert.Null(new DiceRoll(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "1d20", "[]", 0, 1, DateTimeOffset.UtcNow, null, "   ").Label);
        Assert.Throws<ArgumentException>(() => new DiceRoll(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "1d20", "[]", 0, 1, DateTimeOffset.UtcNow, null, "a\nb"));
    }

    [Theory]
    [InlineData("-1/2")]
    [InlineData("1000/1")]
    [InlineData("31")]
    public void Out_of_range_challenge_ratings_are_rejected(string cr) =>
        Assert.Throws<ArgumentException>(() => Creature.ParseChallenge(cr));

    [Fact]
    public void Compendium_rows_enforce_the_database_limits_in_the_domain()
    {
        Assert.Throws<ArgumentException>(() => Creature.ForCompendium(Guid.NewGuid(), new string('s', 121), "SRD 5.1", null, null, Stats(), DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => Creature.ForCompendium(Guid.NewGuid(), "x", new string('b', 41), null, null, Stats(), DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => Creature.ForCompendium(Guid.NewGuid(), "x", "SRD 5.1", "http://insecure.example/x", null, Stats(), DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => Creature.ForCompendium(Guid.NewGuid(), "x", "SRD 5.1", null, "https://pocketdm.com.br/" + new string('a', 400), Stats(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Refresh_reports_a_changed_image_source_and_never_half_applies_a_bad_entry()
    {
        var creature = Creature.ForCompendium(Guid.NewGuid(), "goblin", "SRD 5.1", null, "https://pocketdm.com.br/a.webp", Stats(), DateTimeOffset.UtcNow);
        Assert.False(creature.RefreshFromCompendium("SRD 5.1", null, "https://pocketdm.com.br/a.webp", Stats(), DateTimeOffset.UtcNow));
        Assert.True(creature.RefreshFromCompendium("SRD 5.2", null, "https://pocketdm.com.br/b.webp", Stats(), DateTimeOffset.UtcNow));
        Assert.ThrowsAny<ArgumentException>(() => creature.RefreshFromCompendium("SRD 9", null, "https://pocketdm.com.br/c.webp", Stats(dex: 99), DateTimeOffset.UtcNow));
        Assert.Equal("SRD 5.2", creature.SourceBook);
        Assert.Equal("https://pocketdm.com.br/b.webp", creature.ImageSourceUrl);
    }
}
