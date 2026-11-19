using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Companion.Application.Contracts;
using Companion.Application.Creatures;
using Companion.Domain.Models;

namespace Companion.IntegrationTests;

public sealed class CreatureApiTests(CompanionApiFactory factory) : ApiTestBase(factory)
{
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public async Task Compendium_is_seeded_searchable_and_filterable()
    {
        var master = await CreateCampaign("cr-seed");
        var all = await List(master, "");
        Assert.True(all.Total >= 700, $"compendium has {all.Total} creatures");

        // Accent-insensitive search on the Portuguese name, and on the English one.
        var aguia = await List(master, "?q=AGUIA");
        Assert.Contains(aguia.Items, x => x.Name.StartsWith("Águia"));
        var english = await List(master, "?q=gibbering");
        Assert.Contains(english.Items, x => x.NameEn == "Gibbering Mouther");
        Assert.DoesNotContain("(Gibbering Mouther)", english.Items.Single(x => x.NameEn == "Gibbering Mouther").Name);

        var cheap = await List(master, "?crMax=0.25&size=Small&pageSize=60");
        Assert.NotEmpty(cheap.Items);
        Assert.All(cheap.Items, x => Assert.Equal(CreatureSize.Small, x.Size));
        Assert.All(cheap.Items, x => Assert.True(Creature.ParseChallenge(x.ChallengeRating) <= 0.25m));

        var paged = await List(master, "?page=2&pageSize=10");
        Assert.Equal(10, paged.Items.Count);
        Assert.Equal(2, paged.Page);

        var types = await Send<string[]>(Authenticated(HttpMethod.Get, $"/api/campaigns/{master.CampaignId}/creatures/types", master));
        Assert.Contains("aberração", types);
    }

    [Fact]
    public async Task Library_is_master_only_but_images_are_visible_to_players()
    {
        var master = await CreateCampaign("cr-auth");
        var player = await Join(master, "Ayla");
        var forbidden = await Client.SendAsync(Authenticated(HttpMethod.Get, $"/api/campaigns/{player.CampaignId}/creatures", player));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var goblin = await Find(master, "goblin");
        Assert.NotNull(goblin.ImageUrl);
        var image = await Client.SendAsync(Authenticated(HttpMethod.Get, goblin.ImageUrl!, player));
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/webp", image.Content.Headers.ContentType?.MediaType);
        Assert.Contains(Factory.ImageSource.Requests, url => url.Contains("pocketdm.com.br"));
    }

    [Fact]
    public async Task Editing_a_compendium_creature_creates_a_campaign_copy_isolated_from_other_campaigns()
    {
        var master = await CreateCampaign("cr-edit");
        var other = await CreateCampaign("cr-other");
        var goblin = await Find(master, "goblin");
        var original = await Get(master, goblin.Id);

        var edited = await Send<CreatureDto>(Authenticated(HttpMethod.Put, $"/api/campaigns/{master.CampaignId}/creatures/{goblin.Id}", master,
            Save(original) with { ArmorClass = 18, Notes = "lidera a patrulha", ExpectedRevision = original.Revision }));
        Assert.Equal(CreatureOrigin.Edited, edited.Origin);
        Assert.Equal(goblin.Id, edited.BaseCreatureId);
        Assert.Equal(18, edited.ArmorClass);
        Assert.Equal("lidera a patrulha", edited.Notes);

        // Listing and getting by the original id now return the campaign's version.
        var listed = await Find(master, "goblin");
        Assert.Equal(edited.Id, listed.Id);
        Assert.Equal(CreatureOrigin.Edited, listed.Origin);
        Assert.Equal(18, (await Get(master, goblin.Id)).ArmorClass);

        // Another campaign still sees the untouched original.
        Assert.Equal(original.ArmorClass, (await Get(other, goblin.Id)).ArmorClass);
        Assert.Equal(CreatureOrigin.Compendium, (await Find(other, "goblin")).Origin);

        // Stale revision -> 409.
        var stale = await Client.SendAsync(Authenticated(HttpMethod.Put, $"/api/campaigns/{master.CampaignId}/creatures/{edited.Id}", master,
            Save(edited) with { ExpectedRevision = original.Revision }));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        // Restore: deleting the copy brings back the original.
        var restored = await Send<CreatureDto>(Authenticated(HttpMethod.Delete, $"/api/campaigns/{master.CampaignId}/creatures/{edited.Id}", master));
        Assert.Equal(goblin.Id, restored.Id);
        Assert.Equal(CreatureOrigin.Compendium, restored.Origin);

        var compendiumDelete = await Client.SendAsync(Authenticated(HttpMethod.Delete, $"/api/campaigns/{master.CampaignId}/creatures/{goblin.Id}", master));
        Assert.Equal(HttpStatusCode.BadRequest, compendiumDelete.StatusCode);
    }

    [Fact]
    public async Task Custom_creature_with_uploaded_image_round_trips()
    {
        var master = await CreateCampaign("cr-custom");
        var goblinSheet = await Get(master, (await Find(master, "goblin")).Id);
        var created = await Client.SendAsync(Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/creatures", master,
            Save(goblinSheet) with { Name = "Capitão Rato", Notes = null }));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var custom = (await created.Content.ReadFromJsonAsync<CreatureDto>(JsonOptions))!;
        Assert.Equal(CreatureOrigin.Custom, custom.Origin);

        var invalid = await UploadImage(master, custom.Id, [1, 2, 3], "image/png");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var uploaded = await UploadImage(master, custom.Id, TinyPng, "image/png");
        uploaded.EnsureSuccessStatusCode();
        var withImage = (await uploaded.Content.ReadFromJsonAsync<CreatureDto>(JsonOptions))!;
        var image = await Client.SendAsync(Authenticated(HttpMethod.Get, withImage.ImageUrl!, master));
        Assert.Equal(TinyPng, await image.Content.ReadAsByteArrayAsync());

        var removed = await Send<CreatureDto>(Authenticated(HttpMethod.Delete, $"/api/campaigns/{master.CampaignId}/creatures/{custom.Id}/image", master));
        Assert.Null(removed.ImageUrl);
        var deleted = await Client.SendAsync(Authenticated(HttpMethod.Delete, $"/api/campaigns/{master.CampaignId}/creatures/{custom.Id}", master));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var invalidSave = await Client.SendAsync(Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/creatures", master,
            Save(goblinSheet) with { Strength = 40 }));
        Assert.Equal(HttpStatusCode.BadRequest, invalidSave.StatusCode);
    }

    [Fact]
    public async Task Adding_creatures_to_the_encounter_numbers_them_and_links_the_token_image_and_size()
    {
        var master = await CreateCampaign("cr-add");
        var player = await Join(master, "Ayla");
        var encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/encounters", master));
        (await Configure(master, encounter.Id)).EnsureSuccessStatusCode();
        var goblin = await Get(master, (await Find(master, "goblin")).Id);
        var ogre = (await List(master, "?q=ogro&size=Large")).Items.First();

        var afterGoblins = await Send<EncounterDto>(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/creatures", master, new AddCreaturesToEncounterRequest(goblin.Id, 3)));
        var goblins = afterGoblins.Combatants.Where(x => x.CreatureId == goblin.Id).Select(x => x.Name).OrderBy(x => x).ToArray();
        Assert.Equal(["Goblin 1", "Goblin 2", "Goblin 3"], goblins);
        Assert.All(afterGoblins.Combatants, x => Assert.Equal(goblin.HitPoints, x.CurrentHitPoints));
        Assert.All(afterGoblins.Combatants, x => Assert.Equal(goblin.HitPoints, x.MaxHitPoints));
        Assert.All(afterGoblins.Combatants, x => Assert.Equal(goblin.InitiativeModifier, x.InitiativeModifier));

        var afterOgre = await Send<EncounterDto>(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/creatures", master, new AddCreaturesToEncounterRequest(ogre.Id, 1)));
        var ogreCombatant = afterOgre.Combatants.Single(x => x.CreatureId == ogre.Id);

        // Every participant's board shows the creature image and the size.
        var board = await Send<BattlemapSnapshotDto>(Authenticated(HttpMethod.Get,
            $"/api/campaigns/{player.CampaignId}/encounters/{encounter.Id}/battlemap", player));
        var goblinToken = board.Tokens.First(x => x.CreatureId == goblin.Id);
        Assert.Equal(goblin.ImageUrl, goblinToken.ImageUrl);
        Assert.Equal(1, goblinToken.FootprintSize);
        Assert.Equal(2, board.Tokens.Single(x => x.ParticipantId == ogreCombatant.Id).FootprintSize);

        var tooMany = await Client.SendAsync(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/creatures", master, new AddCreaturesToEncounterRequest(goblin.Id, 21)));
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        var asPlayer = await Client.SendAsync(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{player.CampaignId}/encounters/{encounter.Id}/creatures", player, new AddCreaturesToEncounterRequest(goblin.Id, 1)));
        Assert.Equal(HttpStatusCode.Forbidden, asPlayer.StatusCode);
    }

    [Fact]
    public async Task Creatures_can_join_a_running_combat_as_reinforcements_but_not_an_ended_one()
    {
        var master = await CreateCampaign("cr-reinf");
        var encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/encounters", master));
        var goblin = (await Find(master, "goblin")).Id;
        var url = $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/creatures";
        await Send<EncounterDto>(Authenticated(HttpMethod.Post, url, master, new AddCreaturesToEncounterRequest(goblin, 1)));
        await Send<EncounterDto>(Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/initiative", master, new RollInitiativeRequest(null)));
        var started = await Send<EncounterDto>(Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/start", master));
        Assert.Equal("Active", started.Status.ToString());
        // Hide every creature's HP, then move the turn to the last in the order so any newcomer
        // sorting ahead would shift the index if the turn were not preserved.
        await Send<EncounterDto>(Authenticated(HttpMethod.Put, $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/hit-points-visibility", master, new SetHitPointsVisibilityRequest(true)));
        var advanced = await Send<EncounterDto>(Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/next-turn", master));
        var currentBefore = advanced.Combatants.Single(x => x.IsCurrent).Id;

        var reinforced = await Send<EncounterDto>(Authenticated(HttpMethod.Post, url, master, new AddCreaturesToEncounterRequest(goblin, 2)));
        Assert.Equal(["Goblin", "Goblin 2", "Goblin 3"], reinforced.Combatants.Select(x => x.Name).OrderBy(x => x).ToArray());
        Assert.All(reinforced.Combatants, x => Assert.NotNull(x.Initiative));
        Assert.Equal("Active", reinforced.Status.ToString());
        Assert.Equal(currentBefore, reinforced.Combatants.Single(x => x.IsCurrent).Id);
        Assert.Equal(advanced.Round, reinforced.Round);
        Assert.All(reinforced.Combatants, x => Assert.True(x.IsHitPointsHidden));

        // The player never sees the newcomers' HP (joins after start so combat could begin without them).
        var player = await Join(master, "Ayla");
        var playerView = await Send<CampaignSnapshot>(Authenticated(HttpMethod.Get, $"/api/campaigns/{player.CampaignId}/snapshot", player));
        Assert.All(playerView.ActiveEncounter!.Combatants.Where(x => x.CreatureId is not null), x => Assert.Null(x.CurrentHitPoints));

        (await Client.SendAsync(Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/end", master))).EnsureSuccessStatusCode();
        var afterEnd = await Client.SendAsync(Authenticated(HttpMethod.Post, url, master, new AddCreaturesToEncounterRequest(goblin, 1)));
        Assert.Equal(HttpStatusCode.BadRequest, afterEnd.StatusCode);
    }

    [Fact]
    public async Task Roll_preset_is_kept_per_token_and_roll_label_reaches_the_history()
    {
        var master = await CreateCampaign("cr-roll");
        var encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/encounters", master));
        (await Configure(master, encounter.Id)).EnsureSuccessStatusCode();
        var goblin = (await Find(master, "goblin")).Id;
        var withGoblins = await Send<EncounterDto>(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/creatures", master, new AddCreaturesToEncounterRequest(goblin, 2)));
        var first = withGoblins.Combatants.Single(x => x.Name == "Goblin 1");
        var second = withGoblins.Combatants.Single(x => x.Name == "Goblin 2");

        var board = await Send<BattlemapSnapshotDto>(Authenticated(HttpMethod.Put,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/battlemap/tokens/{first.Id}/roll-preset", master,
            new SetRollPresetRequest(1, 20, "DEX", 2)));
        Assert.Equal(new RollPresetDto(1, 20, "dex", 2), board.Tokens.Single(x => x.ParticipantId == first.Id).RollPreset);
        Assert.Null(board.Tokens.Single(x => x.ParticipantId == second.Id).RollPreset);

        var invalid = await Client.SendAsync(Authenticated(HttpMethod.Put,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/battlemap/tokens/{first.Id}/roll-preset", master,
            new SetRollPresetRequest(1, 7, null, 0)));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var roll = Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/dice-rolls", master,
            new RollDiceRequest("1d20+2+2", true, "Goblin 1"));
        roll.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var rolled = await Send<DiceRollDto>(roll);
        Assert.Equal("Goblin 1", rolled.Label);
        var snapshot = await Send<CampaignSnapshot>(Authenticated(HttpMethod.Get, $"/api/campaigns/{master.CampaignId}/snapshot", master));
        Assert.Contains(snapshot.DiceRolls, x => x.Id == rolled.Id && x.Label == "Goblin 1");
    }

    [Fact]
    public async Task Changing_a_linked_creature_tells_every_board_to_refresh()
    {
        // Critic finding 1: image/sheet changes never reached other clients' boards.
        var master = await CreateCampaign("cr-notify");
        var encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/encounters", master));
        (await Configure(master, encounter.Id)).EnsureSuccessStatusCode();
        var goblin = (await Find(master, "goblin")).Id;
        await Send<EncounterDto>(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/creatures", master, new AddCreaturesToEncounterRequest(goblin, 1)));

        var before = Factory.Notifications.For(master.CampaignId).Count;
        var copy = (await (await UploadImage(master, goblin, TinyPng, "image/png")).Content.ReadFromJsonAsync<CreatureDto>(JsonOptions))!;
        var afterUpload = Factory.Notifications.For(master.CampaignId).Skip(before).ToArray();
        Assert.Contains(afterUpload, x => x.Type == "EncounterInvalidated" && x.EncounterId == encounter.Id);

        // The token now points at the campaign copy (new image), and restoring points it back.
        var board = await Send<BattlemapSnapshotDto>(Authenticated(HttpMethod.Get, $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/battlemap", master));
        Assert.Equal(copy.Id, board.Tokens.Single(x => x.CreatureId is not null).CreatureId);
        before = Factory.Notifications.For(master.CampaignId).Count;
        await Send<CreatureDto>(Authenticated(HttpMethod.Delete, $"/api/campaigns/{master.CampaignId}/creatures/{copy.Id}", master));
        Assert.Contains(Factory.Notifications.For(master.CampaignId).Skip(before), x => x.Type == "EncounterInvalidated");
        board = await Send<BattlemapSnapshotDto>(Authenticated(HttpMethod.Get, $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/battlemap", master));
        Assert.Equal(goblin, board.Tokens.Single(x => x.CreatureId is not null).CreatureId);
    }

    [Fact]
    public async Task A_participant_loading_the_image_never_bumps_the_masters_revision()
    {
        // Critic finding 2: the lazy fetch wrote the versioned row -> false 409 on the master's next save.
        var master = await CreateCampaign("cr-rowversion");
        var player = await Join(master, "Ayla");
        var wolf = await Get(master, (await Find(master, "lobo")).Id);
        var copy = await Send<CreatureDto>(Authenticated(HttpMethod.Put, $"/api/campaigns/{master.CampaignId}/creatures/{wolf.Id}/notes", master,
            new SaveCreatureNotesRequest("caça em bando", wolf.Revision)));
        Assert.NotNull(copy.ImageUrl);

        (await Client.SendAsync(Authenticated(HttpMethod.Get, copy.ImageUrl!, player))).EnsureSuccessStatusCode();
        (await Client.SendAsync(Authenticated(HttpMethod.Get, copy.ImageUrl!, player))).EnsureSuccessStatusCode();

        var saved = await Client.SendAsync(Authenticated(HttpMethod.Put, $"/api/campaigns/{master.CampaignId}/creatures/{copy.Id}/notes", master,
            new SaveCreatureNotesRequest("caça em bando, foge com 3 PV", copy.Revision)));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
    }

    [Fact]
    public async Task Large_creatures_added_before_the_map_exists_still_occupy_their_size()
    {
        // Critic finding 4: no appearance row was created without a map, so an Ogre ended up 1x1.
        var master = await CreateCampaign("cr-footprint");
        var encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/encounters", master));
        var ogre = (await List(master, "?q=ogro&size=Large")).Items.First();
        await Send<EncounterDto>(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/creatures", master, new AddCreaturesToEncounterRequest(ogre.Id, 2)));
        (await Configure(master, encounter.Id)).EnsureSuccessStatusCode();

        var board = await Send<BattlemapSnapshotDto>(Authenticated(HttpMethod.Get, $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/battlemap", master));
        Assert.All(board.Tokens.Where(x => x.CreatureId == ogre.Id), x => Assert.Equal(2, x.FootprintSize));

        // Round-2 finding N1: the first roll preset creates the appearance row; it must keep 2x2.
        var ogreToken = board.Tokens.First(x => x.CreatureId == ogre.Id).ParticipantId;
        var afterPreset = await Send<BattlemapSnapshotDto>(Authenticated(HttpMethod.Put,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/battlemap/tokens/{ogreToken}/roll-preset", master, new SetRollPresetRequest(1, 20, "str", 0)));
        Assert.Equal(2, afterPreset.Tokens.Single(x => x.ParticipantId == ogreToken).FootprintSize);
    }

    [Fact]
    public async Task Players_never_see_the_masters_roll_presets()
    {
        var master = await CreateCampaign("cr-preset-privacy");
        var player = await Join(master, "Ayla");
        var encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/encounters", master));
        (await Configure(master, encounter.Id)).EnsureSuccessStatusCode();
        var goblin = (await Find(master, "goblin")).Id;
        var withGoblin = await Send<EncounterDto>(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/creatures", master, new AddCreaturesToEncounterRequest(goblin, 1)));
        var token = withGoblin.Combatants.Single().Id;
        await Send<BattlemapSnapshotDto>(Authenticated(HttpMethod.Put,
            $"/api/campaigns/{master.CampaignId}/encounters/{encounter.Id}/battlemap/tokens/{token}/roll-preset", master, new SetRollPresetRequest(1, 20, "dex", 3)));

        var playerBoard = await Send<BattlemapSnapshotDto>(Authenticated(HttpMethod.Get, $"/api/campaigns/{player.CampaignId}/encounters/{encounter.Id}/battlemap", player));
        Assert.Null(playerBoard.Tokens.Single(x => x.ParticipantId == token).RollPreset);
    }

    private CompanionApiFactory Factory { get; } = factory;

    private static SaveCreatureRequest Save(CreatureDto x) => new(
        x.Name, x.NameEn, x.Size, x.Type, x.Alignment, x.ArmorClass, x.ArmorClassNote, x.HitPoints, x.HitDice, x.Speed, x.Initiative,
        x.Strength, x.Dexterity, x.Constitution, x.Intelligence, x.Wisdom, x.Charisma, x.SavingThrows, x.Skills,
        x.DamageVulnerabilities, x.DamageResistances, x.DamageImmunities, x.ConditionImmunities, x.Senses, x.Languages,
        x.ChallengeRating, x.ExperiencePoints, x.ProficiencyBonus, x.Traits, x.Actions, x.BonusActions, x.Reactions,
        x.LegendaryActions, x.LegendaryActionsIntro, x.Notes, x.Revision);

    private Task<CreaturePageDto> List(SessionResponse session, string query) =>
        Send<CreaturePageDto>(Authenticated(HttpMethod.Get, $"/api/campaigns/{session.CampaignId}/creatures{query}", session));

    private async Task<CreatureSummaryDto> Find(SessionResponse session, string name)
    {
        var page = await List(session, $"?q={Uri.EscapeDataString(name)}&pageSize=60");
        return page.Items.First(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private Task<CreatureDto> Get(SessionResponse session, Guid id) =>
        Send<CreatureDto>(Authenticated(HttpMethod.Get, $"/api/campaigns/{session.CampaignId}/creatures/{id}", session));

    private Task<HttpResponseMessage> UploadImage(SessionResponse session, Guid id, byte[] bytes, string mediaType)
    {
        var request = Authenticated(HttpMethod.Put, $"/api/campaigns/{session.CampaignId}/creatures/{id}/image", session);
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        return Client.SendAsync(request);
    }

    private async Task<SessionResponse> Join(SessionResponse master, string name)
    {
        var response = await Client.PostAsJsonAsync("/api/campaigns/join", new JoinCampaignRequest(master.PlayerCode, name));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions))!;
    }

    private Task<HttpResponseMessage> Configure(SessionResponse session, Guid encounterId)
    {
        var content = new MultipartFormDataContent();
        var image = new ByteArrayContent(TinyPng);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(image, "image", "map.png");
        content.Add(new StringContent("10"), "columns");
        content.Add(new StringContent("10"), "rows");
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/campaigns/{session.CampaignId}/encounters/{encounterId}/battlemap") { Content = content };
        request.Headers.Add("X-Participant-Token", session.ParticipantToken);
        return Client.SendAsync(request);
    }

    private async Task<T> Send<T>(HttpRequestMessage request)
    {
        var response = await Client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }
}
