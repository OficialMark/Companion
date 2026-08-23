using System.Net;
using System.Net.Http.Json;
using Companion.Application.Contracts;

namespace Companion.IntegrationTests;

public sealed class EncounterApiTests(CompanionApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Master_can_run_round_and_player_cannot_advance()
    {
        var master = await CreateCampaign(Guid.NewGuid().ToString("N")[..6]);
        var join = await Client.PostAsJsonAsync("/api/campaigns/join", new JoinCampaignRequest(master.CampaignCode, $"Jogador {Guid.NewGuid():N}"[..20]));
        var player = (await join.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions))!;

        var create = await Client.SendAsync(Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/encounters", master));
        var encounter = (await create.Content.ReadFromJsonAsync<EncounterDto>(JsonOptions))!;
        for (var index = 0; index < 2; index++)
        {
            var add = Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/combatants", master,
                new AddCombatantRequest($"Combatente {index}", index));
            encounter = (await (await Client.SendAsync(add)).Content.ReadFromJsonAsync<EncounterDto>(JsonOptions))!;
        }

        encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/initiative", master, new RollInitiativeRequest(null)));
        encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/start", master));
        Assert.Equal(1, encounter.Round);

        var currentId = encounter.Combatants.Single(x => x.IsCurrent).Id;
        encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/initiative", master, new RollInitiativeRequest(null)));
        Assert.Equal(currentId, encounter.Combatants.Single(x => x.IsCurrent).Id);

        var forbidden = await Client.SendAsync(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/next-turn", player));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    private async Task<T> Send<T>(HttpRequestMessage request)
    {
        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }
}
