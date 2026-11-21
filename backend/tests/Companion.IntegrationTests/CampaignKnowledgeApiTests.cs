using System.Net;
using System.Net.Http.Json;
using Companion.Application.Campaigns;
using Companion.Application.Contracts;
using Companion.Domain.Campaigns;

namespace Companion.IntegrationTests;

public sealed class CampaignKnowledgeApiTests(CompanionApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Hidden_records_and_master_notes_are_absent_from_player_responses()
    {
        var master = await CreateCampaign(Guid.NewGuid().ToString("N")[..6]);
        var player = await Join(master, "Jogador");
        var created = await Client.SendAsync(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/knowledge", master,
            new SaveCampaignKnowledgeRequest(CampaignKnowledgeKind.Npc, "Aliado secreto", "Resumo", "Descrição", false, null)));
        created.EnsureSuccessStatusCode();
        var hidden = (await created.Content.ReadFromJsonAsync<CampaignKnowledgeDetail>(JsonOptions))!;

        var note = await Client.SendAsync(Authenticated(HttpMethod.Put,
            $"/api/campaigns/{master.CampaignId}/knowledge/{hidden.Id}/master-note", master,
            new SaveMasterNoteRequest("Plano reservado")));
        note.EnsureSuccessStatusCode();

        var listResponse = await Client.SendAsync(Authenticated(HttpMethod.Get,
            $"/api/campaigns/{master.CampaignId}/knowledge?kind=Npc&q=secreto", player));
        listResponse.EnsureSuccessStatusCode();
        var page = (await listResponse.Content.ReadFromJsonAsync<CampaignKnowledgePage>(JsonOptions))!;
        Assert.Equal(0, page.Total);
        Assert.Empty(page.Items);
        var direct = await Client.SendAsync(Authenticated(HttpMethod.Get,
            $"/api/campaigns/{master.CampaignId}/knowledge/{hidden.Id}", player));
        Assert.Equal(HttpStatusCode.NotFound, direct.StatusCode);

        var masterDetailResponse = await Client.SendAsync(Authenticated(HttpMethod.Get,
            $"/api/campaigns/{master.CampaignId}/knowledge/{hidden.Id}", master));
        var masterDetail = (await masterDetailResponse.Content.ReadFromJsonAsync<CampaignKnowledgeDetail>(JsonOptions))!;
        Assert.Equal("Plano reservado", masterDetail.MasterNote);
        var forged = await Client.SendAsync(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/knowledge", player,
            new SaveCampaignKnowledgeRequest(CampaignKnowledgeKind.Clue, "Oculta", null, null, false, null)));
        Assert.Equal(HttpStatusCode.Forbidden, forged.StatusCode);
    }

    [Fact]
    public async Task Players_can_add_map_points_and_keep_them_in_relative_coordinates()
    {
        var master = await CreateCampaign(Guid.NewGuid().ToString("N")[..6]);
        var player = await Join(master, "Cartógrafo");
        var created = await Client.SendAsync(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/knowledge", master,
            new SaveCampaignKnowledgeRequest(CampaignKnowledgeKind.Map, "Ruínas", null, null, true, null)));
        var map = (await created.Content.ReadFromJsonAsync<CampaignKnowledgeDetail>(JsonOptions))!;
        var added = await Client.SendAsync(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/knowledge/{map.Id}/points", player,
            new SaveCampaignMapPointRequest("Portão", "Entrada norte", .25m, .75m)));
        added.EnsureSuccessStatusCode();

        var detailResponse = await Client.SendAsync(Authenticated(HttpMethod.Get,
            $"/api/campaigns/{master.CampaignId}/knowledge/{map.Id}", master));
        var detail = (await detailResponse.Content.ReadFromJsonAsync<CampaignKnowledgeDetail>(JsonOptions))!;
        var point = Assert.Single(detail.Points);
        Assert.Equal(.25m, point.X);
        Assert.Equal(.75m, point.Y);
        Assert.Equal("Entrada norte", point.Description);
    }

    private async Task<SessionResponse> Join(SessionResponse master, string name)
    {
        var response = await Client.PostAsJsonAsync("/api/campaigns/join", new JoinCampaignRequest(master.PlayerCode, name));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions))!;
    }
}
