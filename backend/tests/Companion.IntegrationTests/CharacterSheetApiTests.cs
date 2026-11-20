using System.Net;
using Companion.Application.Characters;
using Companion.Domain.Characters;
using Microsoft.Extensions.DependencyInjection;

namespace Companion.IntegrationTests;

/// <summary>Spec 011, US1 and US3: sheet and inventory through the real API and database.</summary>
public sealed class CharacterSheetApiTests(CompanionApiFactory factory) : CharacterTestBase(factory)
{
    private CompanionApiFactory Factory { get; } = factory;

    [Fact]
    public async Task First_open_creates_a_default_sheet_once_even_when_two_requests_race()
    {
        var (_, player) = await Table();
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Get(player)));

        Assert.All(responses, x => Assert.Equal(1, x.Sheet.Level));
        Assert.Equal(10, responses[0].Stats.Abilities["str"].Final);
        Assert.Equal(10, responses[0].Stats.ArmorClass.Value);
        Assert.Equal(responses[0].Version, responses[1].Version);
    }

    [Fact]
    public async Task First_open_keeps_the_hp_the_player_gave_the_table_and_syncs_the_table_at_once()
    {
        var (master, player) = await Table();
        var manual = await Send(HttpMethod.Put, $"/api/campaigns/{player.CampaignId}/participants/me/combat-profile", player,
            new Application.Contracts.UpdateCombatProfileRequest(4, 25, 30));
        manual.EnsureSuccessStatusCode();

        var sheet = await Get(player);

        Assert.Equal(30, sheet.Sheet.BaseMaxHitPoints);
        Assert.Equal(25, sheet.Sheet.CurrentHitPoints);
        var participant = (await Snapshot(master)).Participants.Single(x => x.Id == player.ParticipantId);
        Assert.Equal(sheet.Stats.Initiative.Value, participant.InitiativeModifier);
        Assert.Equal(25, participant.CurrentHitPoints);
        Assert.Equal(30, participant.MaxHitPoints);
    }

    [Fact]
    public async Task Saving_base_values_returns_recalculated_stats_and_survives_a_reload()
    {
        var (_, player) = await Table();
        var sheet = await Get(player);

        var saved = await SaveSheet(player, sheet, x => x with { Dexterity = 16, Level = 5, CharacterName = "  Loren  " });

        Assert.Equal(3, saved.Stats.Initiative.Value);
        Assert.Equal(3, saved.Stats.ProficiencyBonus);
        Assert.Equal(13, saved.Stats.ArmorClass.Value);
        Assert.Equal("Loren", saved.Sheet.CharacterName);
        var reloaded = await Get(player);
        Assert.Equal(16, reloaded.Sheet.Dexterity);
        Assert.Equal(saved.Version, reloaded.Version);
    }

    [Fact]
    public async Task Saving_with_an_old_version_is_a_conflict_and_changes_nothing()
    {
        var (_, player) = await Table();
        var first = await Get(player);
        await SaveSheet(player, first, x => x with { Strength = 14 });

        var stale = await Send(HttpMethod.Put, Base(player), player, new SaveSheetRequest(first.Sheet with { Strength = 8 }, first.Version));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(14, (await Get(player)).Sheet.Strength);
    }

    [Fact]
    public async Task Invalid_values_are_rejected_with_a_message()
    {
        var (_, player) = await Table();
        var sheet = await Get(player);
        var response = await Send(HttpMethod.Put, Base(player), player, new SaveSheetRequest(sheet.Sheet with { Level = 21 }, sheet.Version));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("nível", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Sheet_initiative_and_hit_points_feed_the_table()
    {
        var (master, player) = await Table();
        var sheet = await Get(player);

        await SaveSheet(player, sheet, x => x with { Dexterity = 14, BaseMaxHitPoints = 30, CurrentHitPoints = 25 });

        var snapshot = await Snapshot(master);
        var participant = snapshot.Participants.Single(x => x.Id == player.ParticipantId);
        Assert.Equal(2, participant.InitiativeModifier);
        Assert.Equal(25, participant.CurrentHitPoints);
        Assert.Equal(30, participant.MaxHitPoints);
        var events = Factory.Notifications.For(player.CampaignId).Select(x => x.Type).ToArray();
        Assert.Contains("ParticipantUpdated", events);
        Assert.Contains(CharacterService.ChangedEvent, events);
    }

    [Fact]
    public async Task US3_armor_sequence_keeps_base_values_and_limits_one_armor_and_one_shield()
    {
        var (_, player) = await Table();
        var character = await SaveSheet(player, await Get(player), x => x with { Dexterity = 14 });
        Assert.Equal(12, character.Stats.ArmorClass.Value);

        character = await AddItem(player, Armor("Cota de Malha", 16, ArmorType.Heavy));
        var chain = character.Items.Single().Id;
        character = await AddItem(player, Shield("Escudo +1", magicBonus: 1));
        var shield = character.Items.Single(x => x.Data.Name == "Escudo +1").Id;
        character = await AddItem(player, Gear("Anel de Proteção", new ItemEffect(EffectType.ArmorClass, null, EffectOp.Add, 1)));
        var ring = character.Items.Single(x => x.Data.Name == "Anel de Proteção").Id;
        character = await AddItem(player, Armor("Placas", 18, ArmorType.Heavy));
        var plate = character.Items.Single(x => x.Data.Name == "Placas").Id;

        Assert.Equal(16, (await Equip(player, chain)).Stats.ArmorClass.Value);
        Assert.Equal(19, (await Equip(player, shield)).Stats.ArmorClass.Value);
        Assert.Equal(20, (await Equip(player, ring)).Stats.ArmorClass.Value);

        var swapped = await Equip(player, plate);
        Assert.Equal(22, swapped.Stats.ArmorClass.Value);
        Assert.Contains("Cota de Malha foi desequipado.", swapped.Notices);
        Assert.False(swapped.Items.Single(x => x.Id == chain).Data.Equipped);
        Assert.Equal(4, swapped.Items.Count);

        var withoutShield = await Equip(player, shield, equipped: false);
        Assert.Equal(19, withoutShield.Stats.ArmorClass.Value);
        Assert.Equal(14, withoutShield.Sheet.Dexterity);
        Assert.Equal(14, withoutShield.Stats.Abilities["dex"].Final);
    }

    [Fact]
    public async Task Duplicate_effect_is_suppressed_and_comes_back_when_the_kept_copy_is_unequipped()
    {
        var (_, player) = await Table();
        var ringEffect = new ItemEffect(EffectType.ArmorClass, null, EffectOp.Add, 1);
        await AddItem(player, Gear("Anel de Proteção", ringEffect));
        var character = await AddItem(player, Gear("Anel de Proteção", ringEffect));
        var first = character.Items[0].Id;
        var second = character.Items[1].Id;

        await Equip(player, first);
        var both = await Equip(player, second);
        Assert.Equal(11, both.Stats.ArmorClass.Value);
        Assert.Single(both.Stats.SuppressedEffects);

        var onlySecond = await Equip(player, first, equipped: false);
        Assert.Equal(11, onlySecond.Stats.ArmorClass.Value);
        Assert.Empty(onlySecond.Stats.SuppressedEffects);
    }

    [Fact]
    public async Task Unequipping_a_hit_point_item_lowers_current_hit_points_to_the_new_maximum()
    {
        var (master, player) = await Table();
        var character = await AddItem(player, Gear("Amuleto", new ItemEffect(EffectType.MaxHp, null, EffectOp.Add, 10)));
        var amulet = character.Items.Single().Id;
        character = await Equip(player, amulet);
        character = await SaveSheet(player, character, x => x with { CurrentHitPoints = 20 });
        Assert.Equal(20, character.Stats.MaxHitPoints.Value);

        var after = await Equip(player, amulet, equipped: false);

        Assert.Equal(10, after.Sheet.CurrentHitPoints);
        Assert.Equal(10, (await Snapshot(master)).Participants.Single(x => x.Id == player.ParticipantId).CurrentHitPoints);
    }

    [Fact]
    public async Task Weapon_proficiency_starts_from_the_general_proficiency_when_added_from_the_catalog()
    {
        Assert.True(CharacterService.DefaultProficiency(
            new GeneralProficiencies([ArmorType.Light], false, true, false, null), Weapon("Clava") with
            {
                Weapon = Weapon("Clava").Weapon! with { Class = WeaponClass.Simple }
            }));
        Assert.False(CharacterService.DefaultProficiency(
            new GeneralProficiencies([], false, true, false, null), Weapon("Espada Longa")));
        await Task.CompletedTask;
    }

    private async Task<Application.Contracts.CampaignSnapshot> Snapshot(Application.Contracts.SessionResponse session)
    {
        var response = await Send(HttpMethod.Get, $"/api/campaigns/{session.CampaignId}/snapshot", session);
        response.EnsureSuccessStatusCode();
        return (await System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<Application.Contracts.CampaignSnapshot>(response.Content, JsonOptions))!;
    }
}
