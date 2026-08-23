using System.Net;
using System.Net.Http.Json;
using Companion.Application.Contracts;

namespace Companion.IntegrationTests;

public sealed class CampaignApiTests(CompanionApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Create_join_and_snapshot_are_isolated()
    {
        var master = await CreateCampaign(Guid.NewGuid().ToString("N")[..6]);
        var joinResponse = await Client.PostAsJsonAsync("/api/campaigns/join", new JoinCampaignRequest(master.CampaignCode, "Jogador"));
        joinResponse.EnsureSuccessStatusCode();
        var player = (await joinResponse.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions))!;

        var snapshotRequest = Authenticated(HttpMethod.Get, $"/api/campaigns/{master.CampaignId}/snapshot", player);
        var snapshotResponse = await Client.SendAsync(snapshotRequest);
        snapshotResponse.EnsureSuccessStatusCode();
        var snapshot = (await snapshotResponse.Content.ReadFromJsonAsync<CampaignSnapshot>(JsonOptions))!;
        Assert.Equal(2, snapshot.Participants.Count);

        var other = await CreateCampaign(Guid.NewGuid().ToString("N")[..6]);
        var crossRequest = Authenticated(HttpMethod.Get, $"/api/campaigns/{other.CampaignId}/snapshot", player);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.SendAsync(crossRequest)).StatusCode);
    }

    [Fact]
    public async Task Invalid_code_returns_not_found()
    {
        var response = await Client.PostAsJsonAsync("/api/campaigns/join", new JoinCampaignRequest("ZZZZZZ", "Jogador"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
