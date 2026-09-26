using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Companion.Application.Contracts;

namespace Companion.IntegrationTests;

public sealed class BattlemapApiTests(CompanionApiFactory factory) : ApiTestBase(factory)
{
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public async Task Master_can_publish_retrieve_and_download_battlemap()
    {
        var master = await CreateCampaign("battlemap-publish");
        var encounter = await CreateEncounter(master);

        var response = await Configure(master, encounter.Id, TinyPng, "image/png", "map.png", 20, 15);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ReplaceBattlemapResultDto>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(20, result.Battlemap.Columns);
        Assert.Equal(15, result.Battlemap.Rows);
        Assert.DoesNotContain(Convert.ToBase64String(TinyPng), await response.Content.ReadAsStringAsync());

        var snapshotRequest = Authenticated(
            HttpMethod.Get,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/battlemap",
            master);
        var snapshotResponse = await Client.SendAsync(snapshotRequest);
        snapshotResponse.EnsureSuccessStatusCode();

        var imageRequest = Authenticated(
            HttpMethod.Get,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/battlemap/image?revision={result.Battlemap.ImageRevision}",
            master);
        var imageResponse = await Client.SendAsync(imageRequest);
        Assert.Equal("image/png", imageResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(TinyPng, await imageResponse.Content.ReadAsByteArrayAsync());
        Assert.NotNull(imageResponse.Headers.ETag);
    }

    [Fact]
    public async Task Player_cannot_configure_battlemap()
    {
        var master = await CreateCampaign("battlemap-forbidden");
        var encounter = await CreateEncounter(master);
        var player = await Join(master.PlayerCode, "Jogador");

        var response = await Configure(player, encounter.Id, TinyPng, "image/png", "map.png", 10, 10);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Upload_rejects_mime_signature_mismatch()
    {
        var master = await CreateCampaign("battlemap-signature");
        var encounter = await CreateEncounter(master);

        var response = await Configure(master, encounter.Id, [1, 2, 3, 4], "image/png", "map.png", 10, 10);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Owner_and_master_can_move_token_but_other_player_cannot()
    {
        var master = await CreateCampaign("battlemap-move");
        var player = await Join(master.PlayerCode, "Ayla");
        var other = await Join(master.PlayerCode, "Borin");
        var encounter = await CreateEncounter(master);
        var configured = await Configure(master, encounter.Id, TinyPng, "image/png", "map.png", 10, 10);
        configured.EnsureSuccessStatusCode();

        var ownerMove = await Move(player, encounter.Id, player.ParticipantId, 3, 4, null);
        ownerMove.EnsureSuccessStatusCode();
        var ownerSnapshot = await ownerMove.Content.ReadFromJsonAsync<BattlemapSnapshotDto>(JsonOptions);
        var token = Assert.Single(ownerSnapshot!.Tokens, item => item.ParticipantId == player.ParticipantId);
        Assert.Equal(3, token.Column);
        Assert.Equal(4, token.Row);

        var forbidden = await Move(other, encounter.Id, player.ParticipantId, 2, 2, token.PositionVersion);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var masterMove = await Move(master, encounter.Id, player.ParticipantId, 5, 6, token.PositionVersion);
        masterMove.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Manual_creature_has_master_controlled_battlemap_token()
    {
        var master = await CreateCampaign("battlemap-creature");
        var player = await Join(master.PlayerCode, "Ayla");
        var encounter = await CreateEncounter(master);
        var added = await Client.SendAsync(Authenticated(
            HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/combatants",
            master,
            new AddCombatantRequest("Goblin", 2, 8)));
        added.EnsureSuccessStatusCode();
        var updatedEncounter = (await added.Content.ReadFromJsonAsync<EncounterDto>(JsonOptions))!;
        var creature = Assert.Single(updatedEncounter.Combatants, item => item.ParticipantId is null);
        (await Configure(master, encounter.Id, TinyPng, "image/png", "map.png", 10, 10)).EnsureSuccessStatusCode();

        var snapshotResponse = await Client.SendAsync(Authenticated(
            HttpMethod.Get,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/battlemap",
            master));
        snapshotResponse.EnsureSuccessStatusCode();
        var snapshot = (await snapshotResponse.Content.ReadFromJsonAsync<BattlemapSnapshotDto>(JsonOptions))!;
        var creatureToken = Assert.Single(snapshot.Tokens, item => item.ParticipantId == creature.Id);
        Assert.True(creatureToken.IsCreature);
        Assert.Null(creatureToken.Column);

        var forbidden = await Move(player, encounter.Id, creature.Id, 2, 3, null);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var moved = await Move(master, encounter.Id, creature.Id, 2, 3, null);
        moved.EnsureSuccessStatusCode();
        var movedSnapshot = (await moved.Content.ReadFromJsonAsync<BattlemapSnapshotDto>(JsonOptions))!;
        var movedCreature = Assert.Single(movedSnapshot.Tokens, item => item.ParticipantId == creature.Id);
        Assert.Equal(2, movedCreature.Column);
        Assert.Equal(3, movedCreature.Row);
    }

    [Fact]
    public async Task Hidden_grid_is_persisted_and_token_can_return_to_reserve()
    {
        var master = await CreateCampaign("battlemap-hidden-grid");
        var player = await Join(master.PlayerCode, "Dara");
        var encounter = await CreateEncounter(master);
        var configured = await Configure(
            master, encounter.Id, TinyPng, "image/png", "map.png", 10, 10, isGridVisible: false);
        var configuredResult = (await configured.Content.ReadFromJsonAsync<ReplaceBattlemapResultDto>(JsonOptions))!;
        Assert.False(configuredResult.Battlemap.IsGridVisible);

        var moved = await Move(player, encounter.Id, player.ParticipantId, 3, 4, null);
        var movedSnapshot = (await moved.Content.ReadFromJsonAsync<BattlemapSnapshotDto>(JsonOptions))!;
        var positioned = Assert.Single(movedSnapshot.Tokens, item => item.ParticipantId == player.ParticipantId);

        var removed = await Move(player, encounter.Id, player.ParticipantId, null, null, positioned.PositionVersion);
        removed.EnsureSuccessStatusCode();
        var reserveSnapshot = (await removed.Content.ReadFromJsonAsync<BattlemapSnapshotDto>(JsonOptions))!;
        var reserveToken = Assert.Single(reserveSnapshot.Tokens, item => item.ParticipantId == player.ParticipantId);
        Assert.Null(reserveToken.Column);
        Assert.Null(reserveToken.Row);
    }
    [Fact]
    public async Task Reducing_grid_returns_outside_tokens_to_reserve()
    {
        var master = await CreateCampaign("battlemap-resize");
        var player = await Join(master.PlayerCode, "Cora");
        var encounter = await CreateEncounter(master);
        var created = await Configure(master, encounter.Id, TinyPng, "image/png", "map.png", 20, 20);
        var first = (await created.Content.ReadFromJsonAsync<ReplaceBattlemapResultDto>(JsonOptions))!;
        var moved = await Move(player, encounter.Id, player.ParticipantId, 15, 15, null);
        moved.EnsureSuccessStatusCode();

        var replaced = await Configure(
            master, encounter.Id, TinyPng, "image/png", "smaller.png", 10, 10, first.Battlemap.Revision);

        replaced.EnsureSuccessStatusCode();
        var result = (await replaced.Content.ReadFromJsonAsync<ReplaceBattlemapResultDto>(JsonOptions))!;
        Assert.Contains(player.ParticipantId, result.ReturnedToReserve);
        Assert.Null(Assert.Single(result.Battlemap.Tokens, x => x.ParticipantId == player.ParticipantId).Column);
    }

    [Fact]
    public async Task Owner_and_master_can_set_attack_area_but_other_player_cannot()
    {
        var master = await CreateCampaign("attack-area-auth");
        var player = await Join(master.PlayerCode, "Ayla");
        var other = await Join(master.PlayerCode, "Borin");
        var encounter = await CreateEncounter(master);
        (await Configure(master, encounter.Id, TinyPng, "image/png", "map.png", 10, 10)).EnsureSuccessStatusCode();
        (await Move(player, encounter.Id, player.ParticipantId, 3, 4, null)).EnsureSuccessStatusCode();

        var ownerSet = await SetAttackArea(player, encounter.Id, player.ParticipantId, "Circle", 3, null, null);
        ownerSet.EnsureSuccessStatusCode();
        var snapshot = (await ownerSet.Content.ReadFromJsonAsync<BattlemapSnapshotDto>(JsonOptions))!;
        var token = Assert.Single(snapshot.Tokens, x => x.ParticipantId == player.ParticipantId);
        Assert.NotNull(token.AttackArea);
        Assert.Equal("Circle", token.AttackArea!.Shape);
        Assert.Equal(3, token.AttackArea.Radius);

        var forbidden = await SetAttackArea(other, encounter.Id, player.ParticipantId, "Circle", 2, null, null);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var masterSet = await SetAttackArea(master, encounter.Id, player.ParticipantId, "Cone", 5, 2, 90);
        masterSet.EnsureSuccessStatusCode();
        var masterSnapshot = (await masterSet.Content.ReadFromJsonAsync<BattlemapSnapshotDto>(JsonOptions))!;
        var updatedToken = Assert.Single(masterSnapshot.Tokens, x => x.ParticipantId == player.ParticipantId);
        Assert.Equal("Cone", updatedToken.AttackArea!.Shape);
        Assert.Equal(2, updatedToken.AttackArea.Width);
        Assert.Equal(90, updatedToken.AttackArea.DirectionDegrees);
    }

    [Fact]
    public async Task Removing_attack_area_is_idempotent_and_master_can_configure_creature_tokens()
    {
        var master = await CreateCampaign("attack-area-remove");
        var encounter = await CreateEncounter(master);
        (await Configure(master, encounter.Id, TinyPng, "image/png", "map.png", 10, 10)).EnsureSuccessStatusCode();
        var added = await Client.SendAsync(Authenticated(
            HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/combatants",
            master,
            new AddCombatantRequest("Goblin", 2, 8)));
        added.EnsureSuccessStatusCode();
        var encounterDto = (await added.Content.ReadFromJsonAsync<EncounterDto>(JsonOptions))!;
        var creature = Assert.Single(encounterDto.Combatants, x => x.ParticipantId is null);

        var set = await SetAttackArea(master, encounter.Id, creature.Id, "Square", 4, null, null);
        set.EnsureSuccessStatusCode();
        var snapshot = (await set.Content.ReadFromJsonAsync<BattlemapSnapshotDto>(JsonOptions))!;
        Assert.NotNull(Assert.Single(snapshot.Tokens, x => x.ParticipantId == creature.Id).AttackArea);

        var removed = await RemoveAttackArea(master, encounter.Id, creature.Id);
        removed.EnsureSuccessStatusCode();
        var afterRemove = (await removed.Content.ReadFromJsonAsync<BattlemapSnapshotDto>(JsonOptions))!;
        Assert.Null(Assert.Single(afterRemove.Tokens, x => x.ParticipantId == creature.Id).AttackArea);

        var removedAgain = await RemoveAttackArea(master, encounter.Id, creature.Id);
        removedAgain.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Owner_and_master_can_set_token_appearance_but_other_player_cannot()
    {
        var master = await CreateCampaign("appearance-auth");
        var player = await Join(master.PlayerCode, "Ayla");
        var other = await Join(master.PlayerCode, "Borin");
        var encounter = await CreateEncounter(master);
        (await Configure(master, encounter.Id, TinyPng, "image/png", "map.png", 10, 10)).EnsureSuccessStatusCode();
        (await Move(player, encounter.Id, player.ParticipantId, 3, 4, null)).EnsureSuccessStatusCode();

        var ownerSet = await SetAppearance(player, encounter.Id, player.ParticipantId, 3, ["Stunned", "Blinded"]);
        ownerSet.EnsureSuccessStatusCode();
        var snapshot = (await ownerSet.Content.ReadFromJsonAsync<BattlemapSnapshotDto>(JsonOptions))!;
        var token = Assert.Single(snapshot.Tokens, x => x.ParticipantId == player.ParticipantId);
        Assert.Equal(3, token.FootprintSize);
        Assert.Equal(["Stunned", "Blinded"], token.StatusConditions);

        var forbidden = await SetAppearance(other, encounter.Id, player.ParticipantId, 2, []);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var masterSet = await SetAppearance(master, encounter.Id, player.ParticipantId, 5, []);
        masterSet.EnsureSuccessStatusCode();
        var masterSnapshot = (await masterSet.Content.ReadFromJsonAsync<BattlemapSnapshotDto>(JsonOptions))!;
        var updatedToken = Assert.Single(masterSnapshot.Tokens, x => x.ParticipantId == player.ParticipantId);
        Assert.Equal(5, updatedToken.FootprintSize);
        Assert.Empty(updatedToken.StatusConditions!);
    }

    [Fact]
    public async Task Token_appearance_rejects_footprint_size_outside_one_to_five()
    {
        var master = await CreateCampaign("appearance-validation");
        var encounter = await CreateEncounter(master);
        (await Configure(master, encounter.Id, TinyPng, "image/png", "map.png", 10, 10)).EnsureSuccessStatusCode();
        var added = await Client.SendAsync(Authenticated(
            HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/combatants",
            master,
            new AddCombatantRequest("Goblin", 2, 8)));
        var encounterDto = (await added.Content.ReadFromJsonAsync<EncounterDto>(JsonOptions))!;
        var creature = Assert.Single(encounterDto.Combatants, x => x.ParticipantId is null);

        var tooLarge = await SetAppearance(master, encounter.Id, creature.Id, 6, []);
        Assert.Equal(HttpStatusCode.BadRequest, tooLarge.StatusCode);

        var unknownCondition = await SetAppearance(master, encounter.Id, creature.Id, 1, ["NotACondition"]);
        Assert.Equal(HttpStatusCode.BadRequest, unknownCondition.StatusCode);
    }

    [Fact]
    public async Task Player_creates_reposition_and_configure_own_marker_but_other_player_cannot()
    {
        var master = await CreateCampaign("marker-lifecycle");
        var player = await Join(master.PlayerCode, "Ayla");
        var other = await Join(master.PlayerCode, "Borin");
        var encounter = await CreateEncounter(master);
        (await Configure(master, encounter.Id, TinyPng, "image/png", "map.png", 10, 10)).EnsureSuccessStatusCode();

        var created = await CreateMarker(player, encounter.Id, "Lobo invocado");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var snapshot = (await created.Content.ReadFromJsonAsync<BattlemapSnapshotDto>(JsonOptions))!;
        var marker = Assert.Single(snapshot.Tokens, x => x.IsMarker);
        Assert.Equal("Lobo invocado", marker.DisplayName);
        Assert.Equal(player.ParticipantId, marker.OwnerParticipantId);
        Assert.Null(marker.Column);

        var ownerMove = await Move(player, encounter.Id, marker.ParticipantId, 4, 5, marker.PositionVersion);
        ownerMove.EnsureSuccessStatusCode();
        var movedSnapshot = (await ownerMove.Content.ReadFromJsonAsync<BattlemapSnapshotDto>(JsonOptions))!;
        var movedMarker = Assert.Single(movedSnapshot.Tokens, x => x.IsMarker);
        Assert.Equal(4, movedMarker.Column);

        var forbiddenMove = await Move(other, encounter.Id, marker.ParticipantId, 1, 1, movedMarker.PositionVersion);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenMove.StatusCode);

        var ownerArea = await SetAttackArea(player, encounter.Id, marker.ParticipantId, "Circle", 2, null, null);
        ownerArea.EnsureSuccessStatusCode();
        var areaSnapshot = (await ownerArea.Content.ReadFromJsonAsync<BattlemapSnapshotDto>(JsonOptions))!;
        Assert.NotNull(Assert.Single(areaSnapshot.Tokens, x => x.IsMarker).AttackArea);

        var ownerAppearance = await SetAppearance(player, encounter.Id, marker.ParticipantId, 2, ["Prone"]);
        ownerAppearance.EnsureSuccessStatusCode();
        var appearanceSnapshot = (await ownerAppearance.Content.ReadFromJsonAsync<BattlemapSnapshotDto>(JsonOptions))!;
        var configuredMarker = Assert.Single(appearanceSnapshot.Tokens, x => x.IsMarker);
        Assert.Equal(2, configuredMarker.FootprintSize);
        Assert.Equal(["Prone"], configuredMarker.StatusConditions);

        var forbiddenAppearance = await SetAppearance(other, encounter.Id, marker.ParticipantId, 1, []);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenAppearance.StatusCode);

        var forbiddenRemove = await RemoveMarker(other, encounter.Id, marker.ParticipantId);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRemove.StatusCode);

        var removed = await RemoveMarker(player, encounter.Id, marker.ParticipantId);
        removed.EnsureSuccessStatusCode();
        var afterRemove = (await removed.Content.ReadFromJsonAsync<BattlemapSnapshotDto>(JsonOptions))!;
        Assert.DoesNotContain(afterRemove.Tokens, x => x.IsMarker);
    }

    [Fact]
    public async Task Master_can_remove_any_players_marker()
    {
        var master = await CreateCampaign("marker-master-remove");
        var player = await Join(master.PlayerCode, "Ayla");
        var encounter = await CreateEncounter(master);
        (await Configure(master, encounter.Id, TinyPng, "image/png", "map.png", 10, 10)).EnsureSuccessStatusCode();
        var created = await CreateMarker(player, encounter.Id, "Marcador");
        var marker = Assert.Single((await created.Content.ReadFromJsonAsync<BattlemapSnapshotDto>(JsonOptions))!.Tokens, x => x.IsMarker);

        var removed = await RemoveMarker(master, encounter.Id, marker.ParticipantId);
        removed.EnsureSuccessStatusCode();
    }

    private Task<HttpResponseMessage> CreateMarker(SessionResponse session, Guid encounterId, string label) =>
        Client.SendAsync(Authenticated(
            HttpMethod.Post,
            $"/api/campaigns/{session.CampaignId}/encounters/{encounterId}/battlemap/markers",
            session,
            new CreateMarkerRequest(label)));

    private Task<HttpResponseMessage> RemoveMarker(SessionResponse session, Guid encounterId, Guid markerId) =>
        Client.SendAsync(Authenticated(
            HttpMethod.Delete,
            $"/api/campaigns/{session.CampaignId}/encounters/{encounterId}/battlemap/markers/{markerId}",
            session));

    private Task<HttpResponseMessage> SetAppearance(
        SessionResponse session, Guid encounterId, Guid tokenId, int footprintSize, IReadOnlyList<string> statusConditions) =>
        Client.SendAsync(Authenticated(
            HttpMethod.Put,
            $"/api/campaigns/{session.CampaignId}/encounters/{encounterId}/battlemap/tokens/{tokenId}/appearance",
            session,
            new SetTokenAppearanceRequest(footprintSize, statusConditions)));

    private Task<HttpResponseMessage> SetAttackArea(
        SessionResponse session, Guid encounterId, Guid tokenId, string shape, int radius, int? width, int? directionDegrees) =>
        Client.SendAsync(Authenticated(
            HttpMethod.Put,
            $"/api/campaigns/{session.CampaignId}/encounters/{encounterId}/battlemap/tokens/{tokenId}/attack-area",
            session,
            new SetAttackAreaRequest(shape, radius, width, directionDegrees)));

    private Task<HttpResponseMessage> RemoveAttackArea(SessionResponse session, Guid encounterId, Guid tokenId) =>
        Client.SendAsync(Authenticated(
            HttpMethod.Delete,
            $"/api/campaigns/{session.CampaignId}/encounters/{encounterId}/battlemap/tokens/{tokenId}/attack-area",
            session));

    private async Task<EncounterDto> CreateEncounter(SessionResponse session)
    {
        var response = await Client.SendAsync(Authenticated(
            HttpMethod.Post, $"/api/campaigns/{session.CampaignId}/encounters", session));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EncounterDto>(JsonOptions))!;
    }

    private async Task<SessionResponse> Join(string code, string displayName)
    {
        var response = await Client.PostAsJsonAsync("/api/campaigns/join", new JoinCampaignRequest(code, displayName));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions))!;
    }

    private Task<HttpResponseMessage> Configure(
        SessionResponse session,
        Guid encounterId,
        byte[] bytes,
        string mediaType,
        string fileName,
        int columns,
        int rows,
        string? expectedRevision = null,
        bool isGridVisible = true)
    {
        var content = new MultipartFormDataContent();
        var image = new ByteArrayContent(bytes);
        image.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        content.Add(image, "image", fileName);
        content.Add(new StringContent(columns.ToString()), "columns");
        content.Add(new StringContent(rows.ToString()), "rows");
        content.Add(new StringContent(isGridVisible.ToString()), "isGridVisible");
        if (expectedRevision is not null) content.Add(new StringContent(expectedRevision), "expectedRevision");
        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/campaigns/{session.CampaignId}/encounters/{encounterId}/battlemap") { Content = content };
        request.Headers.Add("X-Participant-Token", session.ParticipantToken);
        return Client.SendAsync(request);
    }

    private Task<HttpResponseMessage> Move(
        SessionResponse session,
        Guid encounterId,
        Guid participantId,
        int? column,
        int? row,
        string? expectedPositionVersion) =>
        Client.SendAsync(Authenticated(
            HttpMethod.Put,
            $"/api/campaigns/{session.CampaignId}/encounters/{encounterId}/battlemap/tokens/{participantId}/position",
            session,
            new MoveBattlemapTokenRequest(column, row, expectedPositionVersion)));
}
