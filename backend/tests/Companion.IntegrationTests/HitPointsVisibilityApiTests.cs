using System.Net;
using System.Net.Http.Json;
using Companion.Application.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Companion.IntegrationTests;

public sealed class HitPointsVisibilityApiTests(CompanionApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Master_hides_creature_hit_points_from_players_only()
    {
        var (master, player, encounterId, goblinId, _) = await Arrange("hp-hide");

        var hidden = await Client.SendAsync(Authenticated(HttpMethod.Put,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounterId}/combatants/{goblinId}/hit-points-visibility", master,
            new SetHitPointsVisibilityRequest(true)));
        hidden.EnsureSuccessStatusCode();
        var masterEncounter = (await hidden.Content.ReadFromJsonAsync<EncounterDto>(JsonOptions))!;
        var masterGoblin = Assert.Single(masterEncounter.Combatants, x => x.Id == goblinId);
        Assert.True(masterGoblin.IsHitPointsHidden);
        Assert.Equal(7, masterGoblin.CurrentHitPoints);

        var playerSnapshot = await Snapshot(player);
        var playerGoblin = Assert.Single(playerSnapshot.ActiveEncounter!.Combatants, x => x.Id == goblinId);
        Assert.Null(playerGoblin.CurrentHitPoints);
        Assert.Null(playerGoblin.MaxHitPoints);
        Assert.True(playerGoblin.IsHitPointsHidden);

        // The raw JSON a player receives must not carry the hidden numbers at all.
        var raw = await RawSnapshot(player);
        Assert.DoesNotContain("\"currentHitPoints\":7", raw);

        var shown = await Client.SendAsync(Authenticated(HttpMethod.Put,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounterId}/combatants/{goblinId}/hit-points-visibility", master,
            new SetHitPointsVisibilityRequest(false)));
        shown.EnsureSuccessStatusCode();
        Assert.Equal(7, Assert.Single((await Snapshot(player)).ActiveEncounter!.Combatants, x => x.Id == goblinId).CurrentHitPoints);
    }

    [Fact]
    public async Task Bulk_toggle_affects_every_creature_and_never_player_characters()
    {
        var (master, player, encounterId, goblinId, orcId) = await Arrange("hp-bulk");

        var response = await Client.SendAsync(Authenticated(HttpMethod.Put,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounterId}/hit-points-visibility", master,
            new SetHitPointsVisibilityRequest(true)));
        response.EnsureSuccessStatusCode();
        var encounter = (await response.Content.ReadFromJsonAsync<EncounterDto>(JsonOptions))!;
        Assert.True(encounter.Combatants.Single(x => x.Id == goblinId).IsHitPointsHidden);
        Assert.True(encounter.Combatants.Single(x => x.Id == orcId).IsHitPointsHidden);
        var ownCharacter = encounter.Combatants.Single(x => x.ParticipantId == player.ParticipantId);
        Assert.False(ownCharacter.IsHitPointsHidden);

        var playerView = (await Snapshot(player)).ActiveEncounter!;
        Assert.Equal(18, playerView.Combatants.Single(x => x.ParticipantId == player.ParticipantId).CurrentHitPoints);
    }

    [Fact]
    public async Task Hiding_a_player_character_is_rejected_and_players_cannot_toggle()
    {
        var (master, player, encounterId, goblinId, _) = await Arrange("hp-rules");
        var encounter = (await Snapshot(master)).ActiveEncounter!;
        var character = encounter.Combatants.Single(x => x.ParticipantId == player.ParticipantId);

        var invalid = await Client.SendAsync(Authenticated(HttpMethod.Put,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounterId}/combatants/{character.Id}/hit-points-visibility", master,
            new SetHitPointsVisibilityRequest(true)));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var forbidden = await Client.SendAsync(Authenticated(HttpMethod.Put,
            $"/api/campaigns/{player.CampaignId}/encounters/{encounterId}/combatants/{goblinId}/hit-points-visibility", player,
            new SetHitPointsVisibilityRequest(true)));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var missing = await Client.SendAsync(Authenticated(HttpMethod.Put,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounterId}/combatants/{Guid.NewGuid()}/hit-points-visibility", master,
            new SetHitPointsVisibilityRequest(true)));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Participants_list_keeps_hit_points_for_every_viewer()
    {
        var (master, player, _, _, _) = await Arrange("hp-participants");
        var masterView = await Snapshot(master);
        var ayla = masterView.Participants.Single(x => x.Id == player.ParticipantId);
        Assert.Equal(18, ayla.CurrentHitPoints);
        Assert.Equal(24, ayla.MaxHitPoints);
    }

    private async Task<(SessionResponse Master, SessionResponse Player, Guid EncounterId, Guid GoblinId, Guid OrcId)> Arrange(string suffix)
    {
        var master = await CreateCampaign(suffix);
        var join = await Client.PostAsJsonAsync("/api/campaigns/join", new JoinCampaignRequest(master.PlayerCode, "Ayla"));
        join.EnsureSuccessStatusCode();
        var player = (await join.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions))!;
        (await Client.SendAsync(Authenticated(HttpMethod.Put,
            $"/api/campaigns/{player.CampaignId}/participants/me/combat-profile", player,
            new UpdateCombatProfileRequest(2, 18, 24)))).EnsureSuccessStatusCode();

        var created = await Client.SendAsync(Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/encounters", master));
        created.EnsureSuccessStatusCode();
        var encounterId = (await created.Content.ReadFromJsonAsync<EncounterDto>(JsonOptions))!.Id;
        var goblin = await AddCreature(master, encounterId, "Goblin", 7);
        var orc = await AddCreature(master, encounterId, "Orc", 15);
        // Online players join the encounter when the master rolls initiative.
        Services.GetRequiredService<IPresenceTracker>().Connected(player.ParticipantId, $"hpvis-{Guid.NewGuid():N}");
        (await Client.SendAsync(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounterId}/initiative", master))).EnsureSuccessStatusCode();
        return (master, player, encounterId, goblin, orc);
    }

    private async Task<Guid> AddCreature(SessionResponse master, Guid encounterId, string name, int hitPoints)
    {
        var added = await Client.SendAsync(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounterId}/combatants", master,
            new AddCombatantRequest(name, 1, hitPoints, hitPoints)));
        added.EnsureSuccessStatusCode();
        var encounter = (await added.Content.ReadFromJsonAsync<EncounterDto>(JsonOptions))!;
        return encounter.Combatants.Single(x => x.Name == name).Id;
    }

    private async Task<string> RawSnapshot(SessionResponse session)
    {
        var response = await Client.SendAsync(Authenticated(HttpMethod.Get, $"/api/campaigns/{session.CampaignId}/snapshot", session));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<CampaignSnapshot> Snapshot(SessionResponse session)
    {
        var response = await Client.SendAsync(Authenticated(HttpMethod.Get, $"/api/campaigns/{session.CampaignId}/snapshot", session));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CampaignSnapshot>(JsonOptions))!;
    }
}
