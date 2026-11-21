using Companion.Domain.Characters;

namespace Companion.UnitTests;

/// <summary>Spec 013, US2: validation, counter limits and which rest restores what.</summary>
public sealed class CharacterFeatureTests
{
    private static CharacterFeatureData Data(int? max, int current = 0, FeatureRecovery recovery = FeatureRecovery.LongRest) =>
        new("Sentido Divino", " Paladino 5 ", "  ", max, current, recovery);

    private static CharacterFeature New(int? max, FeatureRecovery recovery = FeatureRecovery.LongRest) =>
        new(Guid.NewGuid(), Guid.NewGuid(), 0, Data(max, 0, recovery), DateTimeOffset.UnixEpoch);

    [Fact]
    public void Validated_trims_text_and_drops_recovery_without_a_counter()
    {
        var data = Data(null, 3).Validated();
        Assert.Equal("Paladino 5", data.Source);
        Assert.Null(data.Description);
        Assert.Equal(0, data.CurrentUses);
        Assert.Equal(FeatureRecovery.None, data.Recovery);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(-1)]
    public void Max_uses_outside_one_to_999_is_rejected(int max) =>
        Assert.Throws<ArgumentException>(() => Data(max).Validated());

    [Fact]
    public void Name_is_required_and_limited()
    {
        Assert.Throws<ArgumentException>(() => (Data(1) with { Name = "  " }).Validated());
        Assert.Throws<ArgumentException>(() => (Data(1) with { Name = new string('x', 81) }).Validated());
        Assert.Throws<ArgumentException>(() => (Data(1) with { Source = new string('x', 61) }).Validated());
        Assert.Throws<ArgumentException>(() => (Data(1) with { Recovery = (FeatureRecovery)9 }).Validated());
    }

    [Fact]
    public void Effects_are_validated_and_legacy_null_becomes_empty()
    {
        Assert.Empty(Data(null).Validated().Effects!);
        var dueling = Data(null) with
        {
            Name = "Dueling",
            Effects = [new ItemEffect(EffectType.DamageBonus, " MELEE ", EffectOp.Add, 2)]
        };
        var effect = Assert.Single(dueling.Validated().Effects!);
        Assert.Equal("melee", effect.Target);
        Assert.Equal(2, effect.Value);

        Assert.Throws<ArgumentException>(() => (dueling with
        {
            Effects = Enumerable.Repeat(effect, ItemEffect.MaxEffectsPerItem + 1).ToArray()
        }).Validated());
    }

    [Fact]
    public void New_feature_starts_full_and_spending_is_clamped()
    {
        var feature = New(5);
        Assert.Equal(5, feature.Data.CurrentUses);
        feature.Spend(-7);
        Assert.Equal(0, feature.Data.CurrentUses);
        feature.Spend(2);
        Assert.Equal(2, feature.Data.CurrentUses);
        feature.Spend(50);
        Assert.Equal(5, feature.Data.CurrentUses);
    }

    [Theory]
    [InlineData(FeatureRecovery.ShortRest, RestKind.Short, true)]
    [InlineData(FeatureRecovery.ShortRest, RestKind.Long, true)]
    [InlineData(FeatureRecovery.LongRest, RestKind.Short, false)]
    [InlineData(FeatureRecovery.LongRest, RestKind.Long, true)]
    [InlineData(FeatureRecovery.None, RestKind.Long, false)]
    public void Rest_restores_only_what_it_covers(FeatureRecovery recovery, RestKind rest, bool restores)
    {
        var feature = New(3, recovery);
        feature.Spend(-3);
        Assert.Equal(restores, feature.Rest(rest));
        Assert.Equal(restores ? 3 : 0, feature.Data.CurrentUses);
    }

    [Fact]
    public void Rest_on_a_full_feature_reports_no_change()
    {
        var feature = New(3, FeatureRecovery.ShortRest);
        Assert.False(feature.Rest(RestKind.Long));
    }

    [Fact]
    public void Update_ignores_the_clients_current_value_so_an_edit_never_undoes_a_spend()
    {
        var feature = New(25);
        feature.Spend(-5);
        feature.Update(feature.Data with { Description = "Reserva de 5 × nível.", CurrentUses = 25 });
        Assert.Equal(20, feature.Data.CurrentUses);
        Assert.Equal("Reserva de 5 × nível.", feature.Data.Description);
    }

    [Fact]
    public void Update_caps_current_by_a_lower_max_and_a_new_counter_starts_full()
    {
        var feature = New(25);
        feature.Spend(-5);
        feature.Update(feature.Data with { MaxUses = 10 });
        Assert.Equal(10, feature.Data.CurrentUses);

        var plain = New(null);
        plain.Update(plain.Data with { MaxUses = 2, CurrentUses = 0, Recovery = FeatureRecovery.ShortRest });
        Assert.Equal(2, plain.Data.CurrentUses);
        Assert.Equal(FeatureRecovery.ShortRest, plain.Data.Recovery);

        plain.Update(plain.Data with { MaxUses = null });
        Assert.Equal(0, plain.Data.CurrentUses);
        Assert.Equal(FeatureRecovery.None, plain.Data.Recovery);
    }
}
