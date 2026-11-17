using System.Net;
using System.Net.Http.Json;
using Companion.Application.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Companion.IntegrationTests;

public sealed class EncounterApiTests(CompanionApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Master_can_run_round_and_player_cannot_advance()
    {
        var master = await CreateCampaign(Guid.NewGuid().ToString("N")[..6]);
        var join = await Client.PostAsJsonAsync("/api/campaigns/join", new JoinCampaignRequest(master.PlayerCode, $"Jogador {Guid.NewGuid():N}"[..20]));
        var player = (await join.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions))!;

        var create = await Client.SendAsync(Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/encounters", master));
        var encounter = (await create.Content.ReadFromJsonAsync<EncounterDto>(JsonOptions))!;
        for (var index = 0; index < 2; index++)
        {
            var add = Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/combatants", master,
                new AddCombatantRequest($"Combatente {index}", index, 20));
            encounter = (await (await Client.SendAsync(add)).Content.ReadFromJsonAsync<EncounterDto>(JsonOptions))!;
        }

        encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/initiative", master, new RollInitiativeRequest(null)));
        encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/start", master));
        Assert.Equal(1, encounter.Round);

        encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/initiative", master, new RollInitiativeRequest(null)));
        var current = encounter.Combatants.Single(x => x.IsCurrent);
        Assert.Equal(encounter.Combatants.Max(x => x.Initiative), current.Initiative);

        var forbidden = await Client.SendAsync(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/next-turn", player));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Participant_updates_own_modifier_and_master_edits_and_removes_combatant()
    {
        var master = await CreateCampaign(Guid.NewGuid().ToString("N")[..6]);
        var join = await Client.PostAsJsonAsync("/api/campaigns/join",
            new JoinCampaignRequest(master.PlayerCode, $"Jogador {Guid.NewGuid():N}"[..20]));
        join.EnsureSuccessStatusCode();
        var player = (await join.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions))!;

        var modifierRequest = Authenticated(HttpMethod.Put,
            $"/api/campaigns/{master.CampaignId}/participants/me/combat-profile", player,
            new UpdateCombatProfileRequest(7, 24));
        var updatedPlayer = await Send<ParticipantDto>(modifierRequest);
        Assert.Equal(7, updatedPlayer.InitiativeModifier);
        Assert.Equal(24, updatedPlayer.CurrentHitPoints);
        var presence = Services.GetRequiredService<IPresenceTracker>();
        presence.Connected(player.ParticipantId, $"integration-{Guid.NewGuid():N}");

        var create = await Client.SendAsync(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters", master));
        var encounter = (await create.Content.ReadFromJsonAsync<EncounterDto>(JsonOptions))!;
        encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/initiative", master,
            new RollInitiativeRequest(null)));
        var combatant = Assert.Single(encounter.Combatants);
        Assert.Equal(player.ParticipantId, combatant.ParticipantId);
        Assert.Equal(7, combatant.InitiativeModifier);
        Assert.Equal(24, combatant.CurrentHitPoints);

        encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/initiative", master,
            new RollInitiativeRequest(null)));
        Assert.Single(encounter.Combatants);

        encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Put,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/combatants/{combatant.Id}", master,
            new UpdateCombatantRequest("Jogador editado", 4, 30)));
        Assert.Equal("Jogador editado", Assert.Single(encounter.Combatants).Name);
        Assert.Equal(30, Assert.Single(encounter.Combatants).CurrentHitPoints);

        encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Delete,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/combatants/{combatant.Id}", master));
        Assert.Empty(encounter.Combatants);
    }

    [Fact]
    public async Task Attack_is_atomic_idempotent_and_filtered_for_player()
    {
        var master = await CreateCampaign(Guid.NewGuid().ToString("N")[..6]);
        var join = await Client.PostAsJsonAsync("/api/campaigns/join", new JoinCampaignRequest(master.PlayerCode, "Atacante"));
        var player = (await join.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions))!;
        await Send<ParticipantDto>(Authenticated(HttpMethod.Put,
            $"/api/campaigns/{master.CampaignId}/participants/me/combat-profile", player,
            new UpdateCombatProfileRequest(2, 20)));
        Services.GetRequiredService<IPresenceTracker>().Connected(player.ParticipantId, $"attack-{Guid.NewGuid():N}");

        var encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/encounters", master));
        encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/initiative", master, new RollInitiativeRequest(null)));
        encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/combatants", master,
            new AddCombatantRequest("Goblin", 1, 1)));
        encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/initiative", master, new RollInitiativeRequest(null)));
        encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/start", master));
        var playerTarget = encounter.Combatants.Single(x => x.ParticipantId == player.ParticipantId);
        var goblinTarget = encounter.Combatants.Single(x => x.Name == "Goblin");

        var rollRequest = Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/dice-rolls", player,
            new RollDiceRequest("1d6+2"));
        rollRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var roll = await Send<DiceRollDto>(rollRequest);

        var attackKey = Guid.NewGuid().ToString();
        HttpRequestMessage AttackRequest()
        {
            var request = Authenticated(HttpMethod.Post,
                $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/attacks", player,
                new ResolveAttackRequest(roll.Id, [playerTarget.Id, goblinTarget.Id]));
            request.Headers.Add("Idempotency-Key", attackKey);
            return request;
        }

        var first = await Send<AttackResultDto>(AttackRequest());
        var retry = await Send<AttackResultDto>(AttackRequest());
        Assert.Equal(first.Attack.Id, retry.Attack.Id);
        Assert.Equal(2, first.Attack.Targets.Count);
        Assert.NotNull(first.Attack.Targets.Single(x => x.CombatantId == playerTarget.Id).HitPointsAfter);
        Assert.Null(first.Attack.Targets.Single(x => x.CombatantId == goblinTarget.Id).HitPointsAfter);
        Assert.NotNull(first.Encounter.Combatants.Single(x => x.Id == playerTarget.Id).CurrentHitPoints);
        // Vida exata visível a qualquer participante, inclusive de um alvo que não é seu (FR-009a).
        Assert.Equal(0, first.Encounter.Combatants.Single(x => x.Id == goblinTarget.Id).CurrentHitPoints);
        Assert.True(first.Encounter.Combatants.Single(x => x.Id == goblinTarget.Id).IsDefeated);
    }
    [Fact]
    public async Task Player_updates_only_own_hit_points_directly_but_sees_everyones_value()
    {
        var master = await CreateCampaign(Guid.NewGuid().ToString("N")[..6]);
        async Task<SessionResponse> Join(string name)
        {
            var response = await Client.PostAsJsonAsync("/api/campaigns/join", new JoinCampaignRequest(master.PlayerCode, name));
            return (await response.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions))!;
        }
        var first = await Join("Primeiro"); var second = await Join("Segundo");
        await Send<ParticipantDto>(Authenticated(HttpMethod.Put, $"/api/campaigns/{master.CampaignId}/participants/me/combat-profile", first, new UpdateCombatProfileRequest(1, 20)));
        await Send<ParticipantDto>(Authenticated(HttpMethod.Put, $"/api/campaigns/{master.CampaignId}/participants/me/combat-profile", second, new UpdateCombatProfileRequest(2, 30)));
        var presence = Services.GetRequiredService<IPresenceTracker>(); presence.Connected(first.ParticipantId, $"hp-{Guid.NewGuid():N}"); presence.Connected(second.ParticipantId, $"hp-{Guid.NewGuid():N}");
        var encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/encounters", master));
        encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/initiative", master, new RollInitiativeRequest(null)));
        encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/start", master));
        var firstCombatant = encounter.Combatants.Single(x => x.ParticipantId == first.ParticipantId);
        var secondCombatant = encounter.Combatants.Single(x => x.ParticipantId == second.ParticipantId);

        var own = await Send<EncounterDto>(Authenticated(HttpMethod.Put,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/combatants/{firstCombatant.Id}/hit-points", first,
            new UpdateHitPointsRequest(17)));
        Assert.Equal(17, own.Combatants.Single(x => x.Id == firstCombatant.Id).CurrentHitPoints);
        // Vida exata de outro combatente visível mesmo a um jogador que não é dono nem mestre (FR-009a).
        Assert.Equal(30, own.Combatants.Single(x => x.Id == secondCombatant.Id).CurrentHitPoints);
        var forbidden = await Client.SendAsync(Authenticated(HttpMethod.Put,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/combatants/{firstCombatant.Id}/hit-points", second,
            new UpdateHitPointsRequest(1)));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var masterSnapshot = await Send<CampaignSnapshot>(Authenticated(HttpMethod.Get, $"/api/campaigns/{master.CampaignId}/snapshot", master));
        Assert.All(masterSnapshot.ActiveEncounter!.Combatants, combatant => Assert.NotNull(combatant.CurrentHitPoints));
    }

    [Fact]
    public async Task Max_hit_points_round_trips_through_setup_screen_and_creature_creation()
    {
        var master = await CreateCampaign(Guid.NewGuid().ToString("N")[..6]);
        var response = await Client.PostAsJsonAsync("/api/campaigns/join", new JoinCampaignRequest(master.PlayerCode, "Jogador"));
        var player = (await response.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions))!;

        var ownProfile = await Send<ParticipantDto>(Authenticated(HttpMethod.Put,
            $"/api/campaigns/{master.CampaignId}/participants/me/combat-profile", player,
            new UpdateCombatProfileRequest(1, 14, 22)));
        Assert.Equal(14, ownProfile.CurrentHitPoints);
        Assert.Equal(22, ownProfile.MaxHitPoints);

        var encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/encounters", master));
        encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/combatants", master,
            new AddCombatantRequest("Ogro", 0, 40, 55)));
        var ogre = encounter.Combatants.Single(x => x.Name == "Ogro");
        Assert.Equal(40, ogre.CurrentHitPoints);
        Assert.Equal(55, ogre.MaxHitPoints);

        var presence = Services.GetRequiredService<IPresenceTracker>();
        presence.Connected(player.ParticipantId, $"maxhp-{Guid.NewGuid():N}");
        encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/initiative", master, new RollInitiativeRequest(null)));
        var playerCombatant = encounter.Combatants.Single(x => x.ParticipantId == player.ParticipantId);
        Assert.Equal(14, playerCombatant.CurrentHitPoints);
        Assert.Equal(22, playerCombatant.MaxHitPoints);
    }
    private async Task<T> Send<T>(HttpRequestMessage request)
    {
        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }
}
