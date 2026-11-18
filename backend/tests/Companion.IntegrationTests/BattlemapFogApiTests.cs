using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Companion.Application.Contracts;

namespace Companion.IntegrationTests;

public sealed class BattlemapFogApiTests(CompanionApiFactory factory) : ApiTestBase(factory)
{
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public async Task Master_sets_fog_player_is_forbidden_and_invalid_masks_are_rejected()
    {
        var (master, player, encounter, _) = await Arrange("fog-auth");

        var forbidden = await SetFog(player, encounter.Id, Mask(4, 4, (0, 0)));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var wrongSize = await SetFog(master, encounter.Id, Convert.ToBase64String(new byte[5]));
        Assert.Equal(HttpStatusCode.BadRequest, wrongSize.StatusCode);

        var notBase64 = await SetFog(master, encounter.Id, "%%%");
        Assert.Equal(HttpStatusCode.BadRequest, notBase64.StatusCode);

        var ok = await SetFog(master, encounter.Id, Mask(4, 4, (0, 0), (3, 3)));
        ok.EnsureSuccessStatusCode();
        var snapshot = (await ok.Content.ReadFromJsonAsync<BattlemapSnapshotDto>(JsonOptions))!;
        Assert.Equal(Mask(4, 4, (0, 0), (3, 3)), snapshot.FogMask);

        var playerView = await GetBattlemap(player, encounter.Id);
        Assert.Equal(snapshot.FogMask, playerView.FogMask);

        var reveal = await SetFog(master, encounter.Id, null);
        reveal.EnsureSuccessStatusCode();
        Assert.Null((await GetBattlemap(player, encounter.Id)).FogMask);
    }

    [Fact]
    public async Task Fogged_creature_and_master_marker_are_not_sent_to_players()
    {
        var (master, player, encounter, creatureId) = await Arrange("fog-hide");
        (await Move(master, encounter.Id, creatureId, 1, 1)).EnsureSuccessStatusCode();
        (await Move(player, encounter.Id, player.ParticipantId, 2, 2)).EnsureSuccessStatusCode();
        var markerResponse = await Client.SendAsync(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/battlemap/markers", master,
            new CreateMarkerRequest("Armadilha")));
        markerResponse.EnsureSuccessStatusCode();
        var marker = Assert.Single((await markerResponse.Content.ReadFromJsonAsync<BattlemapSnapshotDto>(JsonOptions))!.Tokens, x => x.IsMarker);
        (await Move(master, encounter.Id, marker.ParticipantId, 3, 3)).EnsureSuccessStatusCode();

        // Cover every cell holding a token, including the player's own.
        (await SetFog(master, encounter.Id, Mask(4, 4, (1, 1), (2, 2), (3, 3)))).EnsureSuccessStatusCode();

        var playerView = await GetBattlemap(player, encounter.Id);
        Assert.DoesNotContain(playerView.Tokens, x => x.ParticipantId == creatureId);
        Assert.DoesNotContain(playerView.Tokens, x => x.ParticipantId == marker.ParticipantId);
        Assert.Contains(playerView.Tokens, x => x.ParticipantId == player.ParticipantId);

        var masterView = await GetBattlemap(master, encounter.Id);
        Assert.Contains(masterView.Tokens, x => x.ParticipantId == creatureId);
        Assert.Contains(masterView.Tokens, x => x.ParticipantId == marker.ParticipantId);

        // Revealing the creature's cell brings it back.
        (await SetFog(master, encounter.Id, Mask(4, 4, (2, 2), (3, 3)))).EnsureSuccessStatusCode();
        Assert.Contains((await GetBattlemap(player, encounter.Id)).Tokens, x => x.ParticipantId == creatureId);
    }

    [Fact]
    public async Task Partly_fogged_large_creature_stays_visible_and_player_marker_ignores_fog()
    {
        var (master, player, encounter, creatureId) = await Arrange("fog-partial");
        (await Move(master, encounter.Id, creatureId, 0, 0)).EnsureSuccessStatusCode();
        (await Client.SendAsync(Authenticated(HttpMethod.Put,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/battlemap/tokens/{creatureId}/appearance", master,
            new SetTokenAppearanceRequest(2, [])))).EnsureSuccessStatusCode();
        var markerResponse = await Client.SendAsync(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{player.CampaignId}/encounters/{encounter.Id}/battlemap/markers", player,
            new CreateMarkerRequest("Familiar")));
        var marker = Assert.Single((await markerResponse.Content.ReadFromJsonAsync<BattlemapSnapshotDto>(JsonOptions))!.Tokens, x => x.IsMarker);
        (await Move(player, encounter.Id, marker.ParticipantId, 3, 3)).EnsureSuccessStatusCode();

        // 2x2 creature at (0,0) covers (0,0),(1,0),(0,1),(1,1); leave (1,1) revealed.
        (await SetFog(master, encounter.Id, Mask(4, 4, (0, 0), (1, 0), (0, 1), (3, 3)))).EnsureSuccessStatusCode();

        var playerView = await GetBattlemap(player, encounter.Id);
        Assert.Contains(playerView.Tokens, x => x.ParticipantId == creatureId);
        Assert.Contains(playerView.Tokens, x => x.ParticipantId == marker.ParticipantId);

        (await SetFog(master, encounter.Id, Mask(4, 4, (0, 0), (1, 0), (0, 1), (1, 1)))).EnsureSuccessStatusCode();
        Assert.DoesNotContain((await GetBattlemap(player, encounter.Id)).Tokens, x => x.ParticipantId == creatureId);
    }

    [Fact]
    public async Task Replacing_map_with_another_grid_discards_fog()
    {
        var (master, _, encounter, _) = await Arrange("fog-replace");
        var fogged = await SetFog(master, encounter.Id, Mask(4, 4, (0, 0)));
        var revision = (await fogged.Content.ReadFromJsonAsync<BattlemapSnapshotDto>(JsonOptions))!.Revision;

        var replaced = await Configure(master, encounter.Id, 5, 4, revision);
        replaced.EnsureSuccessStatusCode();
        var result = (await replaced.Content.ReadFromJsonAsync<ReplaceBattlemapResultDto>(JsonOptions))!;
        Assert.Null(result.Battlemap.FogMask);
    }

    [Fact]
    public async Task Publishing_a_new_map_with_fog_never_exposes_it_uncovered()
    {
        var master = await CreateCampaign("fog-publish");
        var join = await Client.PostAsJsonAsync("/api/campaigns/join", new JoinCampaignRequest(master.PlayerCode, "Ayla"));
        var player = (await join.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions))!;
        var created = await Client.SendAsync(Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/encounters", master));
        var encounter = (await created.Content.ReadFromJsonAsync<EncounterDto>(JsonOptions))!;

        var allCovered = Convert.ToBase64String(Enumerable.Repeat((byte)0xFF, 2).ToArray()); // 4x4 = 16 cells
        var published = await Configure(master, encounter.Id, 4, 4, fogMask: allCovered);
        Assert.Equal(HttpStatusCode.Created, published.StatusCode);
        var result = (await published.Content.ReadFromJsonAsync<ReplaceBattlemapResultDto>(JsonOptions))!;
        Assert.Equal(allCovered, result.Battlemap.FogMask);
        // The very first state a player can read is the fogged one.
        Assert.Equal(allCovered, (await GetBattlemap(player, encounter.Id)).FogMask);
    }

    [Fact]
    public async Task Replacing_the_image_applies_the_preview_fog_and_rejects_a_mask_for_another_grid()
    {
        var (master, player, encounter, _) = await Arrange("fog-replace-preview");
        var current = await GetBattlemap(master, encounter.Id);

        var wrongGrid = await Configure(master, encounter.Id, 4, 4, current.Revision, fogMask: Convert.ToBase64String(new byte[9]));
        Assert.Equal(HttpStatusCode.BadRequest, wrongGrid.StatusCode);
        Assert.Null((await GetBattlemap(player, encounter.Id)).FogMask); // nothing was saved

        var replaced = await Configure(master, encounter.Id, 4, 4, current.Revision, fogMask: Mask(4, 4, (1, 1)));
        replaced.EnsureSuccessStatusCode();
        Assert.Equal(Mask(4, 4, (1, 1)), (await GetBattlemap(player, encounter.Id)).FogMask);

        // An empty fogMask field means "publish fully revealed".
        var revision = (await GetBattlemap(master, encounter.Id)).Revision;
        (await Configure(master, encounter.Id, 4, 4, revision, fogMask: "")).EnsureSuccessStatusCode();
        Assert.Null((await GetBattlemap(player, encounter.Id)).FogMask);
    }

    [Fact]
    public async Task Legacy_upload_without_fog_field_keeps_the_current_fog_on_the_same_grid()
    {
        var (master, _, encounter, _) = await Arrange("fog-legacy");
        var fogged = await SetFog(master, encounter.Id, Mask(4, 4, (2, 2)));
        var revision = (await fogged.Content.ReadFromJsonAsync<BattlemapSnapshotDto>(JsonOptions))!.Revision;
        (await Configure(master, encounter.Id, 4, 4, revision)).EnsureSuccessStatusCode();
        Assert.Equal(Mask(4, 4, (2, 2)), (await GetBattlemap(master, encounter.Id)).FogMask);
    }

    private static string Mask(int columns, int rows, params (int Column, int Row)[] cells)
    {
        var mask = new byte[(columns * rows + 7) / 8];
        foreach (var (column, row) in cells)
        {
            var index = row * columns + column;
            mask[index / 8] |= (byte)(1 << (index % 8));
        }
        return Convert.ToBase64String(mask);
    }

    private async Task<(SessionResponse Master, SessionResponse Player, EncounterDto Encounter, Guid CreatureId)> Arrange(string suffix)
    {
        var master = await CreateCampaign(suffix);
        var join = await Client.PostAsJsonAsync("/api/campaigns/join", new JoinCampaignRequest(master.PlayerCode, "Ayla"));
        join.EnsureSuccessStatusCode();
        var player = (await join.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions))!;
        var created = await Client.SendAsync(Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/encounters", master));
        created.EnsureSuccessStatusCode();
        var encounter = (await created.Content.ReadFromJsonAsync<EncounterDto>(JsonOptions))!;
        var added = await Client.SendAsync(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/combatants", master,
            new AddCombatantRequest("Goblin", 2, 7, 7)));
        added.EnsureSuccessStatusCode();
        var creature = Assert.Single((await added.Content.ReadFromJsonAsync<EncounterDto>(JsonOptions))!.Combatants, x => x.ParticipantId is null);
        (await Configure(master, encounter.Id, 4, 4)).EnsureSuccessStatusCode();
        return (master, player, encounter, creature.Id);
    }

    private Task<HttpResponseMessage> SetFog(SessionResponse session, Guid encounterId, string? mask) =>
        Client.SendAsync(Authenticated(HttpMethod.Put,
            $"/api/campaigns/{session.CampaignId}/encounters/{encounterId}/battlemap/fog", session,
            new SetFogRequest(mask)));

    private async Task<BattlemapSnapshotDto> GetBattlemap(SessionResponse session, Guid encounterId)
    {
        var response = await Client.SendAsync(Authenticated(HttpMethod.Get,
            $"/api/campaigns/{session.CampaignId}/encounters/{encounterId}/battlemap", session));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BattlemapSnapshotDto>(JsonOptions))!;
    }

    private Task<HttpResponseMessage> Move(SessionResponse session, Guid encounterId, Guid tokenId, int column, int row) =>
        Client.SendAsync(Authenticated(HttpMethod.Put,
            $"/api/campaigns/{session.CampaignId}/encounters/{encounterId}/battlemap/tokens/{tokenId}/position", session,
            new MoveBattlemapTokenRequest(column, row, null)));

    private Task<HttpResponseMessage> Configure(SessionResponse session, Guid encounterId, int columns, int rows, string? expectedRevision = null, string? fogMask = null)
    {
        var content = new MultipartFormDataContent();
        var image = new ByteArrayContent(TinyPng);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(image, "image", "map.png");
        content.Add(new StringContent(columns.ToString()), "columns");
        content.Add(new StringContent(rows.ToString()), "rows");
        content.Add(new StringContent("true"), "isGridVisible");
        if (expectedRevision is not null) content.Add(new StringContent(expectedRevision), "expectedRevision");
        if (fogMask is not null) content.Add(new StringContent(fogMask), "fogMask");
        var request = new HttpRequestMessage(HttpMethod.Put,
            $"/api/campaigns/{session.CampaignId}/encounters/{encounterId}/battlemap") { Content = content };
        request.Headers.Add("X-Participant-Token", session.ParticipantToken);
        return Client.SendAsync(request);
    }
}
