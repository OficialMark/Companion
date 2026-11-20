using System.Net;
using System.Net.Http.Json;
using System.Text;
using Companion.Application.Campaigns;
using Companion.Application.Catalog;
using Companion.Application.Characters;
using Companion.Application.Contracts;
using Companion.Domain.Characters;
using Companion.Domain.Models;
using Companion.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Companion.IntegrationTests;

/// <summary>
/// Spec 011, Phase 12 (T071-T076): regressões dos achados da revisão crítica, pela API e banco reais.
/// </summary>
public sealed class CharacterReviewFixesApiTests(CompanionApiFactory factory) : CharacterTestBase(factory)
{
    private static string CombatProfile(SessionResponse session) => $"/api/campaigns/{session.CampaignId}/participants/me/combat-profile";
    private static string InitiativeModifier(SessionResponse session) => $"/api/campaigns/{session.CampaignId}/participants/me/initiative-modifier";

    // T072 -------------------------------------------------------------------------------------------

    [Fact]
    public async Task Parallel_item_adds_never_pass_the_200_item_limit()
    {
        var (_, player) = await Table();
        await Get(player);
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
            var now = DateTimeOffset.UtcNow;
            for (var index = 0; index < InventoryItem.MaxItemsPerCharacter - 1; index++)
                db.Add(new InventoryItem(Guid.NewGuid(), player.ParticipantId, index, Gear($"Pedra {index}"), null, now));
            await db.SaveChangesAsync();
        }

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(index =>
            Send(HttpMethod.Post, $"{Base(player)}/items", player, new SaveItemRequest(Gear($"Corrida {index}"), null))));

        Assert.All(responses, x => Assert.Contains(x.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict, HttpStatusCode.BadRequest }));
        Assert.Contains(responses, x => x.StatusCode == HttpStatusCode.OK);
        var character = await Get(player);
        Assert.True(character.Items.Count <= InventoryItem.MaxItemsPerCharacter, $"{character.Items.Count} itens na mochila");
        Assert.Equal(InventoryItem.MaxItemsPerCharacter, character.Items.Count);
    }

    [Fact]
    public async Task Parallel_equips_of_different_armors_leave_at_most_one_equipped()
    {
        // Two requests do not always overlap in the test server; five rounds of two make the race
        // happen reliably (verified: without the sheet version check this fails).
        var (_, player) = await Table();
        await AddItem(player, Armor("Cota de Malha", 16, ArmorType.Heavy));
        var character = await AddItem(player, Armor("Peitoral", 14, ArmorType.Medium));
        var ids = character.Items.Select(x => x.Id).ToArray();

        for (var round = 0; round < 5; round++)
        {
            foreach (var id in ids) await Equip(player, id, equipped: false);
            var responses = await Task.WhenAll(ids.Select(id =>
                Send(HttpMethod.Post, $"{Base(player)}/items/{id}/equip", player, new EquipItemRequest(true))));

            Assert.All(responses, x => Assert.Contains(x.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
            var final = await Get(player);
            Assert.True(final.Items.Count(x => x.Data.Equipped) <= 1, $"rodada {round}: duas armaduras equipadas");
        }
    }

    [Fact]
    public async Task Item_writes_without_a_version_still_work_one_after_another_and_bump_the_sheet_version()
    {
        var (_, player) = await Table();
        var first = await Get(player);

        var added = await AddItem(player, Shield("Escudo"));
        var equipped = await Equip(player, added.Items.Single().Id);

        Assert.NotEqual(first.Version, added.Version);
        Assert.NotEqual(added.Version, equipped.Version);
        // The sheet editor saves with the latest version it received: still fine.
        await SaveSheet(player, equipped, x => x with { Strength = 12 });
    }

    // T073 -------------------------------------------------------------------------------------------

    [Fact]
    public async Task If_exists_does_not_create_the_sheet_and_the_manual_profile_keeps_working()
    {
        var (_, player) = await Table();

        var missing = await Send(HttpMethod.Get, $"{Base(player)}?ifExists=true", player);

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains(CharacterService.NoSheetYet, await missing.Content.ReadAsStringAsync());
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
            Assert.False(await db.CharacterSheets.AnyAsync(x => x.ParticipantId == player.ParticipantId));
        }

        var profile = await Send(HttpMethod.Put, CombatProfile(player), player, new UpdateCombatProfileRequest(3, 20));
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
    }

    [Fact]
    public async Task If_exists_returns_the_sheet_when_it_exists()
    {
        var (_, player) = await Table();
        var created = await Get(player);
        var again = await Ok(HttpMethod.Get, $"{Base(player)}?ifExists=true", player);
        Assert.Equal(created.Version, again.Version);
    }

    [Fact]
    public async Task With_a_sheet_the_manual_combat_profile_is_a_conflict_and_changes_nothing()
    {
        var (master, player) = await Table();
        var sheet = await Get(player);
        await SaveSheet(player, sheet, x => x with { Dexterity = 14, BaseMaxHitPoints = 30, CurrentHitPoints = 30 });

        var profile = await Send(HttpMethod.Put, CombatProfile(player), player, new UpdateCombatProfileRequest(10, 999));
        var initiative = await Send(HttpMethod.Put, InitiativeModifier(player), player, new UpdateInitiativeModifierRequest(10));

        Assert.Equal(HttpStatusCode.Conflict, profile.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, initiative.StatusCode);
        Assert.Contains(CampaignService.CombatProfileFromSheet, await profile.Content.ReadAsStringAsync());
        var participant = (await Snapshot(master)).Participants.Single(x => x.Id == player.ParticipantId);
        Assert.Equal(2, participant.InitiativeModifier);
        Assert.Equal(30, participant.MaxHitPoints);
    }

    // T071 -------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("""{"data":{"name":"X","category":"Gear","quantity":1,"equipped":false,"proficient":false,"magicBonus":0,"effects":[{"type":"MaxHp","op":5,"value":2000000000}]}}""")]
    [InlineData("""{"data":{"name":"X","category":"Armor","quantity":1,"equipped":false,"proficient":false,"magicBonus":0,"armor":{"baseAc":12,"type":99},"effects":[]}}""")]
    [InlineData("""{"data":{"name":"X","category":"Weapon","quantity":1,"equipped":false,"proficient":false,"magicBonus":0,"weapon":{"damageDice":"1d8","kind":"Melee","class":"Simple","properties":["magia"]},"effects":[]}}""")]
    public async Task Item_with_garbage_is_a_400(string json)
    {
        var (_, player) = await Table();
        var response = await Raw(HttpMethod.Post, $"{Base(player)}/items", player, json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await Get(player)).Items);
    }

    [Fact]
    public async Task Sheet_with_an_ability_outside_the_list_is_a_400_not_a_500()
    {
        var (_, player) = await Table();
        var sheet = await Get(player);
        var body = System.Text.Json.JsonSerializer.SerializeToNode(new SaveSheetRequest(sheet.Sheet, sheet.Version), JsonOptions)!;
        body["data"]!["spellcastingAbility"] = 9;

        var response = await Raw(HttpMethod.Put, Base(player), player, body.ToJsonString());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("conjuração", await response.Content.ReadAsStringAsync());
    }

    // T074 -------------------------------------------------------------------------------------------

    [Fact]
    public async Task Saved_roll_keeps_versatile_and_rejects_it_outside_weapon_damage()
    {
        var (_, player) = await Table();
        var character = await AddItem(player, Weapon("Espada Longa"));
        var sword = character.Items.Single().Id;
        var damage = new SavedRollData("Dano duas mãos", "lucide:sword", RollKind.Damage, new Dictionary<string, int>(), null, null, null,
            sword, null, [], AdvantageMode.None, 1, Versatile: true);

        character = await Ok(HttpMethod.Post, $"{Base(player)}/rolls", player, new SaveRollRequest(damage));
        Assert.True(character.SavedRolls.Single().Data.Versatile);
        Assert.True((await Get(player)).SavedRolls.Single().Data.Versatile);

        var attack = await Send(HttpMethod.Post, $"{Base(player)}/rolls", player, new SaveRollRequest(damage with { Kind = RollKind.Attack }));
        Assert.Equal(HttpStatusCode.BadRequest, attack.StatusCode);
    }

    // T076 -------------------------------------------------------------------------------------------

    [Fact]
    public async Task New_accounts_store_the_first_session_hash_so_the_previous_api_still_authorizes_them()
    {
        var (master, player) = await Table();
        var tokens = Services.GetRequiredService<IParticipantTokenService>();

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        foreach (var session in new[] { master, player })
        {
            // The authorization of the API before spec 011 (ParticipantAuthorization at f44c558^1).
            var hash = tokens.Hash(session.ParticipantToken);
            var authorized = await db.Participants.AnyAsync(x => x.CampaignId == session.CampaignId && x.TokenHash == hash);
            Assert.True(authorized, $"{session.DisplayName} não autorizaria na versão anterior");
        }
    }

    [Fact]
    public async Task Catalog_page_at_int_max_is_an_empty_page_not_a_500()
    {
        var (_, player) = await Table();
        foreach (var path in new[] { "items", "spells" })
        {
            var response = await Send(HttpMethod.Get, $"/api/campaigns/{player.CampaignId}/catalog/{path}?page=2147483647", player);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var page = await response.Content.ReadFromJsonAsync<CatalogPageDto<object>>(JsonOptions);
            Assert.Empty(page!.Items);
            Assert.Equal(CatalogService.MaxPage, page.Page);
        }
    }

    private async Task<HttpResponseMessage> Raw(HttpMethod method, string path, SessionResponse session, string json)
    {
        var request = new HttpRequestMessage(method, path) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Participant-Token", session.ParticipantToken);
        return await Client.SendAsync(request);
    }

    private async Task<CampaignSnapshot> Snapshot(SessionResponse session)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/campaigns/{session.CampaignId}/snapshot");
        request.Headers.Add("X-Participant-Token", session.ParticipantToken);
        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CampaignSnapshot>(JsonOptions))!;
    }
}
