using System.Net;
using System.Net.Http.Json;
using Companion.Application.Contracts;
using Companion.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Companion.IntegrationTests;

public sealed class AdminApiTests(CompanionApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Admin_requires_secret_lists_both_codes_and_deletes_aggregate()
    {
        var campaign = await CreateCampaign(Guid.NewGuid().ToString("N")[..6]);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Client.GetAsync("/api/admin/campaigns")).StatusCode);

        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/campaigns");
        listRequest.Headers.Add("X-Admin-Key", "integration-admin-secret");
        var listResponse = await Client.SendAsync(listRequest);
        listResponse.EnsureSuccessStatusCode();
        var campaigns = (await listResponse.Content.ReadFromJsonAsync<AdminCampaignDto[]>(JsonOptions))!;
        var listed = Assert.Single(campaigns, item => item.Id == campaign.CampaignId);
        Assert.Equal(campaign.PlayerCode, listed.PlayerCode);
        Assert.Equal(campaign.MasterCode, listed.MasterCode);

        async Task<T> Send<T>(HttpRequestMessage request)
        {
            var response = await Client.SendAsync(request); response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
        }
        var encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post, $"/api/campaigns/{campaign.CampaignId}/encounters", campaign));
        encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post, $"/api/campaigns/{campaign.CampaignId}/encounters/{encounter.Id}/combatants", campaign, new AddCombatantRequest("Alvo", 0, 10)));
        encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post, $"/api/campaigns/{campaign.CampaignId}/encounters/{encounter.Id}/initiative", campaign, new RollInitiativeRequest(null)));
        encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post, $"/api/campaigns/{campaign.CampaignId}/encounters/{encounter.Id}/start", campaign));
        var rollRequest = Authenticated(HttpMethod.Post, $"/api/campaigns/{campaign.CampaignId}/dice-rolls", campaign, new RollDiceRequest("1d4"));
        rollRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var roll = await Send<DiceRollDto>(rollRequest);
        var attackRequest = Authenticated(HttpMethod.Post, $"/api/campaigns/{campaign.CampaignId}/encounters/{encounter.Id}/attacks", campaign, new ResolveAttackRequest(roll.Id, [encounter.Combatants[0].Id]));
        attackRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var attack = await Send<AttackResultDto>(attackRequest);
        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/campaigns/{campaign.CampaignId}");
        deleteRequest.Headers.Add("X-Admin-Key", "integration-admin-secret");
        Assert.Equal(HttpStatusCode.NoContent, (await Client.SendAsync(deleteRequest)).StatusCode);

        var oldSession = Authenticated(HttpMethod.Get, $"/api/campaigns/{campaign.CampaignId}/snapshot", campaign);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.SendAsync(oldSession)).StatusCode);
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        Assert.False(await db.CombatAttacks.AnyAsync(x => x.Id == attack.Attack.Id));
        Assert.False(await db.CombatAttackTargets.AnyAsync(x => x.AttackId == attack.Attack.Id));
        var oldCode = await Client.PostAsJsonAsync("/api/campaigns/join", new JoinCampaignRequest(campaign.PlayerCode, "Novo"));
        Assert.Equal(HttpStatusCode.NotFound, oldCode.StatusCode);
    }
}
