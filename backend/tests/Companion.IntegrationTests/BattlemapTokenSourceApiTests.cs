using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Companion.Application.Catalog;
using Companion.Application.Contracts;
using Companion.Application.Creatures;
using Companion.Domain.Models;

namespace Companion.IntegrationTests;

/// <summary>Spec 016: any participant drops a compendium creature or a catalog item on the board as a token.</summary>
public sealed class BattlemapTokenSourceApiTests(CompanionApiFactory factory) : ApiTestBase(factory)
{
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public async Task Player_adds_a_compendium_creature_with_its_size_and_picture()
    {
        var (master, player, encounterId) = await Board("tk-creature");
        var ogre = await FindOption(player, "ogre");
        Assert.Equal(CreatureSize.Large, ogre.Size);

        var board = await CreateMarker(player, encounterId, new CreateMarkerRequest(null, CreatureId: ogre.Id));

        var token = board.Tokens.Single(x => x.IsMarker);
        Assert.Equal(BattlemapMarker.LabelFrom(ogre.Name), token.DisplayName);
        Assert.Equal("creature", token.SourceKind);
        Assert.Equal(ogre.Id, token.SourceId);
        Assert.Equal(2, token.FootprintSize);
        Assert.Equal(ogre.ImageUrl, token.ImageUrl);
        Assert.Equal(player.ParticipantId, token.OwnerParticipantId);
        // Not a combatant: the creature never enters the initiative order.
        Assert.False(token.IsCreature);
        Assert.Null(token.CreatureId);

        // The master sees the same token, and the player can still move and remove it.
        var masterBoard = await Get(master, encounterId);
        Assert.Contains(masterBoard.Tokens, x => x.ParticipantId == token.ParticipantId && x.SourceKind == "creature");
        (await Client.SendAsync(Authenticated(HttpMethod.Put, $"{BoardPath(player, encounterId)}/tokens/{token.ParticipantId}/position",
            player, new MoveBattlemapTokenRequest(2, 3, null)))).EnsureSuccessStatusCode();
        (await Client.SendAsync(Authenticated(HttpMethod.Delete, $"{BoardPath(player, encounterId)}/markers/{token.ParticipantId}", player)))
            .EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Player_adds_a_catalog_item_and_may_rename_it()
    {
        var (_, player, encounterId) = await Board("tk-item");
        var item = await FindItem(player, "corda");

        var named = await CreateMarker(player, encounterId, new CreateMarkerRequest(null, ItemId: item.Id));
        var renamed = await CreateMarker(player, encounterId, new CreateMarkerRequest("Corda do poço", ItemId: item.Id));

        var tokens = renamed.Tokens.Where(x => x.IsMarker).ToArray();
        Assert.Equal(2, tokens.Length);
        Assert.Contains(tokens, x => x.DisplayName == BattlemapMarker.LabelFrom(item.Name));
        Assert.Contains(tokens, x => x.DisplayName == "Corda do poço");
        Assert.All(tokens, x =>
        {
            Assert.Equal("item", x.SourceKind);
            Assert.Equal(item.Id, x.SourceId);
            Assert.Equal(1, x.FootprintSize);
            Assert.Null(x.ImageUrl);
        });
        Assert.Single(named.Tokens, x => x.IsMarker);
    }

    [Fact]
    public async Task Free_text_tokens_keep_working_without_a_source()
    {
        var (_, player, encounterId) = await Board("tk-free");
        var board = await CreateMarker(player, encounterId, new CreateMarkerRequest("Lobo invocado"));
        var token = board.Tokens.Single(x => x.IsMarker);
        Assert.Equal("Lobo invocado", token.DisplayName);
        Assert.Null(token.SourceKind);
        Assert.Null(token.SourceId);
    }

    [Fact]
    public async Task Players_never_see_or_use_the_masters_own_creatures()
    {
        var (master, player, encounterId) = await Board("tk-secret");
        var goblin = await Send<CreatureDto>(Authenticated(HttpMethod.Get,
            $"/api/campaigns/{master.CampaignId}/creatures/{(await FindOption(master, "goblin")).Id}", master));
        var secret = await Send<CreatureDto>(Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/creatures", master,
            Save(goblin, "Zumbaraque Secreto")));

        var masterOptions = await Options(master, "zumbaraque");
        Assert.Contains(masterOptions.Items, x => x.Id == secret.Id);
        var playerOptions = await Options(player, "zumbaraque");
        Assert.DoesNotContain(playerOptions.Items, x => x.Id == secret.Id);

        var forbidden = await Client.SendAsync(Authenticated(HttpMethod.Post, $"{BoardPath(player, encounterId)}/markers", player,
            new CreateMarkerRequest(null, CreatureId: secret.Id)));
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);

        var allowed = await CreateMarker(master, encounterId, new CreateMarkerRequest(null, CreatureId: secret.Id));
        Assert.Contains(allowed.Tokens, x => x.SourceId == secret.Id && x.DisplayName == "Zumbaraque Secreto");
    }

    [Fact]
    public async Task Rejects_both_sources_and_unknown_entries()
    {
        var (_, player, encounterId) = await Board("tk-invalid");
        var ogre = await FindOption(player, "ogre");
        var item = await FindItem(player, "corda");

        var both = await Client.SendAsync(Authenticated(HttpMethod.Post, $"{BoardPath(player, encounterId)}/markers", player,
            new CreateMarkerRequest(null, CreatureId: ogre.Id, ItemId: item.Id)));
        Assert.Equal(HttpStatusCode.BadRequest, both.StatusCode);

        var unknownCreature = await Client.SendAsync(Authenticated(HttpMethod.Post, $"{BoardPath(player, encounterId)}/markers", player,
            new CreateMarkerRequest(null, CreatureId: Guid.NewGuid())));
        Assert.Equal(HttpStatusCode.NotFound, unknownCreature.StatusCode);

        var unknownItem = await Client.SendAsync(Authenticated(HttpMethod.Post, $"{BoardPath(player, encounterId)}/markers", player,
            new CreateMarkerRequest(null, ItemId: Guid.NewGuid())));
        Assert.Equal(HttpStatusCode.NotFound, unknownItem.StatusCode);

        var noLabel = await Client.SendAsync(Authenticated(HttpMethod.Post, $"{BoardPath(player, encounterId)}/markers", player,
            new CreateMarkerRequest("  ")));
        Assert.Equal(HttpStatusCode.BadRequest, noLabel.StatusCode);
    }

    [Fact]
    public async Task Token_options_expose_no_stat_block_and_page()
    {
        var (_, player, _) = await Board("tk-options");
        var response = await Client.SendAsync(Authenticated(HttpMethod.Get,
            $"/api/campaigns/{player.CampaignId}/creatures/token-options?pageSize=5&page=2", player));
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("hitPoints", json);
        Assert.DoesNotContain("armorClass", json);
        var page = (await response.Content.ReadFromJsonAsync<CreatureTokenOptionPageDto>(JsonOptions))!;
        Assert.Equal(5, page.Items.Count);
        Assert.Equal(2, page.Page);
        Assert.True(page.Total >= 700);

        var anonymous = await Client.GetAsync($"/api/campaigns/{player.CampaignId}/creatures/token-options");
        Assert.False(anonymous.IsSuccessStatusCode);
    }

    private async Task<(SessionResponse Master, SessionResponse Player, Guid EncounterId)> Board(string suffix)
    {
        var master = await CreateCampaign(suffix);
        var join = await Client.PostAsJsonAsync("/api/campaigns/join", new JoinCampaignRequest(master.PlayerCode, "Ayla"));
        join.EnsureSuccessStatusCode();
        var player = (await join.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions))!;
        var encounter = await Send<EncounterDto>(Authenticated(HttpMethod.Post, $"/api/campaigns/{master.CampaignId}/encounters", master));

        var content = new MultipartFormDataContent();
        var image = new ByteArrayContent(TinyPng);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(image, "image", "map.png");
        content.Add(new StringContent("10"), "columns");
        content.Add(new StringContent("10"), "rows");
        var request = new HttpRequestMessage(HttpMethod.Put, BoardPath(master, encounter.Id)) { Content = content };
        request.Headers.Add("X-Participant-Token", master.ParticipantToken);
        (await Client.SendAsync(request)).EnsureSuccessStatusCode();
        return (master, player, encounter.Id);
    }

    private static string BoardPath(SessionResponse session, Guid encounterId) =>
        $"/api/campaigns/{session.CampaignId}/encounters/{encounterId}/battlemap";

    private Task<BattlemapSnapshotDto> CreateMarker(SessionResponse session, Guid encounterId, CreateMarkerRequest body) =>
        Send<BattlemapSnapshotDto>(Authenticated(HttpMethod.Post, $"{BoardPath(session, encounterId)}/markers", session, body));

    private Task<BattlemapSnapshotDto> Get(SessionResponse session, Guid encounterId) =>
        Send<BattlemapSnapshotDto>(Authenticated(HttpMethod.Get, BoardPath(session, encounterId), session));

    private Task<CreatureTokenOptionPageDto> Options(SessionResponse session, string q) =>
        Send<CreatureTokenOptionPageDto>(Authenticated(HttpMethod.Get,
            $"/api/campaigns/{session.CampaignId}/creatures/token-options?q={Uri.EscapeDataString(q)}&pageSize=60", session));

    private async Task<CreatureTokenOptionDto> FindOption(SessionResponse session, string q)
    {
        var page = await Options(session, q);
        return page.Items.OrderBy(x => x.Name.Length).First();
    }

    private async Task<CatalogItemSummaryDto> FindItem(SessionResponse session, string q)
    {
        var page = await Send<CatalogPageDto<CatalogItemSummaryDto>>(Authenticated(HttpMethod.Get,
            $"/api/campaigns/{session.CampaignId}/catalog/items?q={Uri.EscapeDataString(q)}", session));
        return page.Items.First();
    }

    private static SaveCreatureRequest Save(CreatureDto x, string name) => new(
        name, null, x.Size, x.Type, x.Alignment, x.ArmorClass, x.ArmorClassNote, x.HitPoints, x.HitDice, x.Speed, x.Initiative,
        x.Strength, x.Dexterity, x.Constitution, x.Intelligence, x.Wisdom, x.Charisma, x.SavingThrows, x.Skills,
        x.DamageVulnerabilities, x.DamageResistances, x.DamageImmunities, x.ConditionImmunities, x.Senses, x.Languages,
        x.ChallengeRating, x.ExperiencePoints, x.ProficiencyBonus, x.Traits, x.Actions, x.BonusActions, x.Reactions,
        x.LegendaryActions, x.LegendaryActionsIntro, x.Notes, null);

    private async Task<T> Send<T>(HttpRequestMessage request)
    {
        var response = await Client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }
}
