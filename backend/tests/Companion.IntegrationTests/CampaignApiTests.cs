using System.Net;
using System.Net.Http.Json;
using Companion.Application.Contracts;
using Companion.Domain.Models;

namespace Companion.IntegrationTests;

public sealed class CampaignApiTests(CompanionApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Create_join_and_snapshot_are_isolated()
    {
        var master = await CreateCampaign(Guid.NewGuid().ToString("N")[..6]);
        Assert.Matches("^[0-9]{4}$", master.PlayerCode);
        Assert.Matches("^[0-9]{4}$", master.MasterCode!);
        Assert.NotEqual(master.PlayerCode, master.MasterCode);
        var joinResponse = await Client.PostAsJsonAsync("/api/campaigns/join", new JoinCampaignRequest(master.PlayerCode, "Jogador"));
        joinResponse.EnsureSuccessStatusCode();
        var player = (await joinResponse.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions))!;
        Assert.Equal(ParticipantRole.Player, player.Role);
        Assert.Null(player.MasterCode);

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
        var response = await Client.PostAsJsonAsync("/api/campaigns/join", new JoinCampaignRequest("ZZZZ", "Jogador"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Master_code_recovers_master_role_without_being_echoed()
    {
        var creator = await CreateCampaign(Guid.NewGuid().ToString("N")[..6]);
        var response = await Client.PostAsJsonAsync("/api/campaigns/join",
            new JoinCampaignRequest(creator.MasterCode!, "Segundo mestre"));
        response.EnsureSuccessStatusCode();
        var recovered = (await response.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions))!;

        Assert.Equal(ParticipantRole.Master, recovered.Role);
        Assert.Null(recovered.MasterCode);
        Assert.Equal(creator.PlayerCode, recovered.PlayerCode);
    }

    [Fact]
    public async Task Stable_identity_allows_players_with_the_same_display_name()
    {
        var campaign = await CreateCampaign(Guid.NewGuid().ToString("N")[..6]);
        var first = await Client.PostAsJsonAsync("/api/campaigns/join", new JoinCampaignRequest(campaign.PlayerCode, "Alex"));
        var second = await Client.PostAsJsonAsync("/api/campaigns/join", new JoinCampaignRequest(campaign.PlayerCode, "Alex"));

        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();
        var firstSession = (await first.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions))!;
        var secondSession = (await second.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions))!;
        Assert.NotEqual(firstSession.ParticipantId, secondSession.ParticipantId);
    }
}
