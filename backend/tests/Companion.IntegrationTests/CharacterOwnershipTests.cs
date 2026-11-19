using System.Net;
using Companion.Application.Characters;
using Companion.Domain.Characters;

namespace Companion.IntegrationTests;

/// <summary>
/// Spec 011, US2 and SC-002: nobody reads or changes another participant's sheet, whatever the
/// request carries. Every route of contracts/character-api.md is attacked with another owner's
/// ids, a master session, no session and a session of another campaign.
/// </summary>
public sealed class CharacterOwnershipTests(CompanionApiFactory factory) : CharacterTestBase(factory)
{
    private sealed record Victim(Application.Contracts.SessionResponse Session, Guid ItemId, Guid SpellId, Guid RollId, CharacterDto Before);

    private async Task<Victim> PrepareVictim(Application.Contracts.SessionResponse session)
    {
        var character = await AddItem(session, Weapon("Espada de B"));
        var itemId = character.Items.Single().Id;
        character = await Ok(HttpMethod.Post, $"{Base(session)}/spells", session, new SaveSpellRequest(
            new CharacterSpellData("Raio de B", 0, null, null, null, null, null, false, false, null, SpellAttackKind.Attack, null, "1d10", "fogo", false)));
        var spellId = character.Spells.Single().Id;
        character = await Ok(HttpMethod.Post, $"{Base(session)}/rolls", session, new SaveRollRequest(RollFor(itemId)));
        return new Victim(session, itemId, spellId, character.SavedRolls.Single().Id, character);
    }

    private static SavedRollData RollFor(Guid itemId) => new("Ataque", "lucide:sword", RollKind.Attack,
        new Dictionary<string, int> { ["d20"] = 1 }, null, null, null, itemId, null, [], AdvantageMode.None, 1);

    /// <summary>Every write or read that names an object by id, aimed at the victim's ids.</summary>
    private static IEnumerable<(HttpMethod Method, string Path, object? Body)> RoutesById(Application.Contracts.SessionResponse attacker, Victim victim)
    {
        var path = Base(attacker);
        yield return (HttpMethod.Put, $"{path}/items/{victim.ItemId}", new SaveItemRequest(Weapon("Roubada"), null));
        yield return (HttpMethod.Post, $"{path}/items/{victim.ItemId}/equip", new EquipItemRequest(true));
        yield return (HttpMethod.Delete, $"{path}/items/{victim.ItemId}", null);
        yield return (HttpMethod.Put, $"{path}/spells/{victim.SpellId}", new SaveSpellRequest(
            new CharacterSpellData("Roubada", 0, null, null, null, null, null, false, false, null, SpellAttackKind.None, null, null, null, false)));
        yield return (HttpMethod.Delete, $"{path}/spells/{victim.SpellId}", null);
        yield return (HttpMethod.Put, $"{path}/rolls/{victim.RollId}", new SaveRollRequest(RollFor(victim.ItemId) with { Name = "Roubada" }));
        yield return (HttpMethod.Post, $"{path}/rolls/{victim.RollId}/duplicate", null);
        yield return (HttpMethod.Delete, $"{path}/rolls/{victim.RollId}", null);
    }

    /// <summary>Every route of the character API (no ids), for the master/no-session/other-campaign checks.</summary>
    private static IEnumerable<(HttpMethod Method, string Path, object? Body)> AllRoutes(Guid campaignId, Victim victim)
    {
        var path = $"/api/campaigns/{campaignId}/participants/me/character";
        yield return (HttpMethod.Get, path, null);
        yield return (HttpMethod.Put, path, new SaveSheetRequest(CharacterSheetData.Default, victim.Before.Version));
        yield return (HttpMethod.Post, $"{path}/items", new SaveItemRequest(Weapon("X"), null));
        yield return (HttpMethod.Post, $"{path}/items/from-catalog", new AddFromCatalogRequest(Guid.NewGuid(), 1));
        yield return (HttpMethod.Post, $"{path}/spells", new SaveSpellRequest(
            new CharacterSpellData("X", 0, null, null, null, null, null, false, false, null, SpellAttackKind.None, null, null, null, false)));
        yield return (HttpMethod.Post, $"{path}/spells/from-catalog", new AddSpellFromCatalogRequest(Guid.NewGuid()));
        yield return (HttpMethod.Post, $"{path}/rolls", new SaveRollRequest(RollFor(victim.ItemId)));
    }

    [Fact]
    public async Task A_player_cannot_touch_another_players_items_spells_or_rolls_by_id()
    {
        var (_, attacker) = await Table("Atacante");
        var master = await CreateCampaign(Suffix());
        var victimSession = await Join(master.PlayerCode, "Vítima");
        var sameTableAttacker = await Join(master.PlayerCode, "Atacante");
        var victim = await PrepareVictim(victimSession);

        foreach (var who in new[] { sameTableAttacker, attacker })
        {
            foreach (var (method, path, body) in RoutesById(who, victim))
            {
                var response = await Send(method, path.Replace(who.CampaignId.ToString(), who.CampaignId.ToString()), who, body);
                Assert.True(response.StatusCode is HttpStatusCode.NotFound,
                    $"{method} {path} devolveu {(int)response.StatusCode}");
            }
        }

        var after = await Get(victimSession);
        Assert.Equal("Espada de B", after.Items.Single().Data.Name);
        Assert.False(after.Items.Single().Data.Equipped);
        Assert.Equal("Raio de B", after.Spells.Single().Data.Name);
        Assert.Equal("Ataque", after.SavedRolls.Single().Data.Name);
        Assert.Single(after.SavedRolls);
    }

    [Fact]
    public async Task A_saved_roll_cannot_reference_another_players_weapon()
    {
        var master = await CreateCampaign(Suffix());
        var victim = await PrepareVictim(await Join(master.PlayerCode, "Vítima"));
        var attacker = await Join(master.PlayerCode, "Atacante");

        var response = await Send(HttpMethod.Post, $"{Base(attacker)}/rolls", attacker, new SaveRollRequest(RollFor(victim.ItemId)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Item não encontrado na sua ficha.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Master_no_session_and_other_campaign_sessions_are_refused_everywhere()
    {
        var master = await CreateCampaign(Suffix());
        var victimSession = await Join(master.PlayerCode, "Vítima");
        var victim = await PrepareVictim(victimSession);
        var outsider = (await Table("Forasteiro")).Player;
        var anonymous = victimSession with { ParticipantToken = string.Empty };

        foreach (var (method, path, body) in AllRoutes(master.CampaignId, victim))
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await Send(method, path, master, body)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Send(method, path, anonymous, body)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Send(method, path, outsider, body)).StatusCode);
        }

        var after = await Get(victimSession);
        Assert.Equal(victim.Before.Version, after.Version);
        Assert.Single(after.Items);
    }
}
