using System.Net;
using System.Net.Http.Json;
using Companion.Application.Contracts;

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

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/campaigns/{campaign.CampaignId}");
        deleteRequest.Headers.Add("X-Admin-Key", "integration-admin-secret");
        Assert.Equal(HttpStatusCode.NoContent, (await Client.SendAsync(deleteRequest)).StatusCode);

        var oldSession = Authenticated(HttpMethod.Get, $"/api/campaigns/{campaign.CampaignId}/snapshot", campaign);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.SendAsync(oldSession)).StatusCode);
        var oldCode = await Client.PostAsJsonAsync("/api/campaigns/join", new JoinCampaignRequest(campaign.PlayerCode, "Novo"));
        Assert.Equal(HttpStatusCode.NotFound, oldCode.StatusCode);
    }
}
