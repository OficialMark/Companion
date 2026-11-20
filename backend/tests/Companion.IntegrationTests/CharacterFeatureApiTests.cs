using System.Net;
using Companion.Application.Characters;
using Companion.Application.Contracts;
using Companion.Domain.Characters;

namespace Companion.IntegrationTests;

/// <summary>Spec 013, US2: class features with a use counter, rests and order, through the API.</summary>
public sealed class CharacterFeatureApiTests(CompanionApiFactory factory) : CharacterTestBase(factory)
{
    private static CharacterFeatureData Feature(string name, int? max = null, FeatureRecovery recovery = FeatureRecovery.None, int current = 0) =>
        new(name, "Paladino 5", $"Descrição de {name}.", max, current, recovery);

    private Task<CharacterDto> Add(SessionResponse session, CharacterFeatureData data) =>
        Ok(HttpMethod.Post, $"{Base(session)}/features", session, new SaveFeatureRequest(data));

    private static CharacterFeatureDto Named(CharacterDto character, string name) => character.Features.Single(x => x.Data.Name == name);

    [Fact]
    public async Task New_feature_starts_full_and_spending_stays_between_zero_and_max()
    {
        var (_, player) = await Table();

        var sheet = await Add(player, Feature("Sentido Divino", 5, FeatureRecovery.LongRest, current: 0));
        var sense = Named(sheet, "Sentido Divino");
        Assert.Equal(5, sense.Data.CurrentUses);
        Assert.Equal("Paladino 5", sense.Data.Source);

        sheet = await Ok(HttpMethod.Post, $"{Base(player)}/features/{sense.Id}/uses", player, new FeatureUsesRequest(-2));
        Assert.Equal(3, Named(sheet, "Sentido Divino").Data.CurrentUses);
        sheet = await Ok(HttpMethod.Post, $"{Base(player)}/features/{sense.Id}/uses", player, new FeatureUsesRequest(-9));
        Assert.Equal(0, Named(sheet, "Sentido Divino").Data.CurrentUses);
        sheet = await Ok(HttpMethod.Post, $"{Base(player)}/features/{sense.Id}/uses", player, new FeatureUsesRequest(99));
        Assert.Equal(5, Named(sheet, "Sentido Divino").Data.CurrentUses);

        // Persisted: a fresh read shows the stored value.
        await Ok(HttpMethod.Post, $"{Base(player)}/features/{sense.Id}/uses", player, new FeatureUsesRequest(-1));
        Assert.Equal(4, Named(await Get(player), "Sentido Divino").Data.CurrentUses);
    }

    [Fact]
    public async Task Short_rest_restores_short_rest_features_and_long_rest_restores_both()
    {
        var (_, player) = await Table();
        await Add(player, Feature("Canalizar Divindade", 1, FeatureRecovery.ShortRest));
        await Add(player, Feature("Cura pelas Mãos", 25, FeatureRecovery.LongRest));
        var sheet = await Add(player, Feature("Destruição Divina"));
        foreach (var (name, delta) in new[] { ("Canalizar Divindade", -1), ("Cura pelas Mãos", -10) })
            sheet = await Ok(HttpMethod.Post, $"{Base(player)}/features/{Named(sheet, name).Id}/uses", player, new FeatureUsesRequest(delta));

        sheet = await Ok(HttpMethod.Post, $"{Base(player)}/rest", player, new RestRequest(RestKind.Short));
        Assert.Equal(1, Named(sheet, "Canalizar Divindade").Data.CurrentUses);
        Assert.Equal(15, Named(sheet, "Cura pelas Mãos").Data.CurrentUses);
        Assert.Contains("Descanso curto: 1 característica recuperada.", sheet.Notices);

        sheet = await Ok(HttpMethod.Post, $"{Base(player)}/features/{Named(sheet, "Canalizar Divindade").Id}/uses", player, new FeatureUsesRequest(-1));
        sheet = await Ok(HttpMethod.Post, $"{Base(player)}/rest", player, new RestRequest(RestKind.Long));
        Assert.Equal(1, Named(sheet, "Canalizar Divindade").Data.CurrentUses);
        Assert.Equal(25, Named(sheet, "Cura pelas Mãos").Data.CurrentUses);
        Assert.Contains("Descanso longo: 2 características recuperadas.", sheet.Notices);

        // An empty body is refused instead of being read as the first enum value (a short rest).
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(HttpMethod.Post, $"{Base(player)}/rest", player, new { })).StatusCode);

        // A feature without a counter has nothing to spend.
        var smite = await Send(HttpMethod.Post, $"{Base(player)}/features/{Named(sheet, "Destruição Divina").Id}/uses", player, new FeatureUsesRequest(-1));
        Assert.Equal(HttpStatusCode.BadRequest, smite.StatusCode);
    }

    [Fact]
    public async Task Editing_keeps_spent_uses_lowering_max_caps_current_and_turning_counter_on_starts_full()
    {
        var (_, player) = await Table();
        var sheet = await Add(player, Feature("Cura pelas Mãos", 25, FeatureRecovery.LongRest));
        var lay = Named(sheet, "Cura pelas Mãos");
        sheet = await Ok(HttpMethod.Post, $"{Base(player)}/features/{lay.Id}/uses", player, new FeatureUsesRequest(-5));

        // The client sends the current value it shows; renaming keeps 20 spent-down uses.
        sheet = await Ok(HttpMethod.Put, $"{Base(player)}/features/{lay.Id}", player,
            new SaveFeatureRequest(Named(sheet, "Cura pelas Mãos").Data with { Description = "Reserva de 5 × nível." }));
        Assert.Equal(20, Named(sheet, "Cura pelas Mãos").Data.CurrentUses);

        sheet = await Ok(HttpMethod.Put, $"{Base(player)}/features/{lay.Id}", player,
            new SaveFeatureRequest(Named(sheet, "Cura pelas Mãos").Data with { MaxUses = 10 }));
        Assert.Equal(10, Named(sheet, "Cura pelas Mãos").Data.CurrentUses);

        sheet = await Add(player, Feature("Presença Conquistadora"));
        var presence = Named(sheet, "Presença Conquistadora");
        Assert.Null(presence.Data.MaxUses);
        Assert.Equal(FeatureRecovery.None, presence.Data.Recovery);
        sheet = await Ok(HttpMethod.Put, $"{Base(player)}/features/{presence.Id}", player,
            new SaveFeatureRequest(presence.Data with { MaxUses = 1, CurrentUses = 0, Recovery = FeatureRecovery.ShortRest }));
        Assert.Equal(1, Named(sheet, "Presença Conquistadora").Data.CurrentUses);
    }

    [Fact]
    public async Task Move_and_remove_persist_the_order()
    {
        var (_, player) = await Table();
        foreach (var name in new[] { "A", "B", "C" }) await Add(player, Feature(name));
        var sheet = await Get(player);

        sheet = await Ok(HttpMethod.Post, $"{Base(player)}/features/{Named(sheet, "C").Id}/move", player, new MoveFeatureRequest(-1));
        Assert.Equal(["A", "C", "B"], sheet.Features.OrderBy(x => x.SortOrder).Select(x => x.Data.Name));
        // At the top it stays put.
        sheet = await Ok(HttpMethod.Post, $"{Base(player)}/features/{Named(sheet, "A").Id}/move", player, new MoveFeatureRequest(-1));
        Assert.Equal(["A", "C", "B"], sheet.Features.OrderBy(x => x.SortOrder).Select(x => x.Data.Name));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Send(HttpMethod.Post, $"{Base(player)}/features/{Named(sheet, "A").Id}/move", player, new MoveFeatureRequest(2))).StatusCode);

        sheet = await Ok(HttpMethod.Delete, $"{Base(player)}/features/{Named(sheet, "C").Id}", player);
        Assert.Equal(["A", "B"], (await Get(player)).Features.OrderBy(x => x.SortOrder).Select(x => x.Data.Name));
    }

    [Fact]
    public async Task Invalid_features_are_rejected_and_the_limit_is_one_hundred()
    {
        var (_, player) = await Table();
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(HttpMethod.Post, $"{Base(player)}/features", player,
            new SaveFeatureRequest(Feature(" ")))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(HttpMethod.Post, $"{Base(player)}/features", player,
            new SaveFeatureRequest(Feature("Demais", 1000)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(HttpMethod.Post, $"{Base(player)}/features", player,
            new SaveFeatureRequest(Feature("Zero", 0)))).StatusCode);

        for (var i = 0; i < CharacterFeature.MaxFeaturesPerCharacter; i++) await Add(player, Feature($"F{i}"));
        var over = await Send(HttpMethod.Post, $"{Base(player)}/features", player, new SaveFeatureRequest(Feature("Cento e um")));
        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);
        Assert.Contains("100 características", await over.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_feature_effect_changes_the_weapon_damage_and_removing_it_reverts()
    {
        // Spec 014: Duelismo, +2 damage with a one-handed melee weapon.
        var (_, player) = await Table();
        var sheet = await AddItem(player, Weapon("Martelo de Guerra"));
        var before = sheet.Stats.WeaponRolls.Single().Damage.Parts.Sum(x => x.Value);

        var dueling = Feature("Estilo de Luta: Duelismo") with
        {
            Effects = [new ItemEffect(EffectType.DamageBonus, ItemEffect.MeleeOneHanded, EffectOp.Add, 2)]
        };
        sheet = await Add(player, dueling);
        var damage = sheet.Stats.WeaponRolls.Single().Damage;
        Assert.Equal(before + 2, damage.Parts.Sum(x => x.Value));
        Assert.Contains(damage.Parts, x => x.Source == "Estilo de Luta: Duelismo" && x.Value == 2);
        Assert.Equal(ItemEffect.MeleeOneHanded, Assert.Single(Named(sheet, "Estilo de Luta: Duelismo").Data.Effects!).Target);

        // A fresh read computes the same (the effect is stored, not only answered).
        Assert.Equal(before + 2, (await Get(player)).Stats.WeaponRolls.Single().Damage.Parts.Sum(x => x.Value));

        sheet = await Ok(HttpMethod.Delete, $"{Base(player)}/features/{Named(sheet, "Estilo de Luta: Duelismo").Id}", player);
        Assert.Equal(before, sheet.Stats.WeaponRolls.Single().Damage.Parts.Sum(x => x.Value));
    }

    [Fact]
    public async Task An_invalid_feature_effect_is_refused_in_portuguese()
    {
        var (_, player) = await Table();
        var response = await Send(HttpMethod.Post, $"{Base(player)}/features", player, new SaveFeatureRequest(Feature("Ruim") with
        {
            Effects = [new ItemEffect(EffectType.DamageBonus, "duas maos", EffectOp.Add, 2)]
        }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("corpo a corpo de uma mão", await response.Content.ReadAsStringAsync());
        Assert.Empty((await Get(player)).Features);
    }

    [Fact]
    public async Task The_same_player_name_in_another_campaign_sees_the_same_features()
    {
        var name = $"Vash {Suffix()}";
        var (_, here) = await Table(name);
        var (_, there) = await Table(name);
        var sheet = await Add(here, Feature("Sentido Divino", 5, FeatureRecovery.LongRest));
        await Ok(HttpMethod.Post, $"{Base(here)}/features/{Named(sheet, "Sentido Divino").Id}/uses", here, new FeatureUsesRequest(-1));

        Assert.Equal(4, Named(await Get(there), "Sentido Divino").Data.CurrentUses);
    }

    [Fact]
    public async Task Another_player_cannot_touch_my_features()
    {
        var (master, me) = await Table();
        var intruder = await Join(master.PlayerCode, $"Intruso {Suffix()}");
        var sheet = await Add(me, Feature("Sentido Divino", 5, FeatureRecovery.LongRest));
        var id = Named(sheet, "Sentido Divino").Id;

        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(HttpMethod.Post, $"{Base(intruder)}/features/{id}/uses", intruder, new FeatureUsesRequest(-1))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Send(HttpMethod.Post, $"{Base(master)}/features", master, new SaveFeatureRequest(Feature("Mestre")))).StatusCode);
        Assert.Equal(5, Named(await Get(me), "Sentido Divino").Data.CurrentUses);
    }
}
