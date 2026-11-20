using System.Net;
using System.Net.Http.Json;
using Companion.Domain.Characters;

namespace Companion.IntegrationTests;

/// <summary>
/// 2026-11-20 decision: the sheet belongs to the player name, not to a campaign session. The same name
/// typed in any campaign opens the same sheet, and deleting a campaign never deletes it.
/// </summary>
public sealed class CharacterPerNameTests(CompanionApiFactory factory) : CharacterTestBase(factory)
{
    [Fact]
    public async Task The_same_name_in_another_campaign_opens_the_same_sheet_and_syncs_that_table()
    {
        var name = $"Loren {Suffix()}";
        var (_, inA) = await Table(name);
        await SaveSheet(inA, await Get(inA), x => x with { Dexterity = 16, CharacterName = "Loren da Torre" });
        await AddItem(inA, Weapon("Espada Longa"));

        var (masterB, inB) = await Table(name.ToUpperInvariant());
        var fromB = await Get(inB);

        Assert.NotEqual(inA.CampaignId, inB.CampaignId);
        Assert.Equal("Loren da Torre", fromB.Sheet.CharacterName);
        Assert.Equal("Espada Longa", fromB.Items.Single().Data.Name);
        var tableB = await Send(HttpMethod.Get, $"/api/campaigns/{inB.CampaignId}/snapshot", masterB);
        var snapshot = (await tableB.Content.ReadFromJsonAsync<Application.Contracts.CampaignSnapshot>(JsonOptions))!;
        Assert.Equal(3, snapshot.Participants.Single(x => x.Id == inB.ParticipantId).InitiativeModifier);
    }

    [Fact]
    public async Task Edits_from_either_campaign_land_on_the_one_sheet()
    {
        var name = $"Mira {Suffix()}";
        var (_, inA) = await Table(name);
        var (_, inB) = await Table(name);

        var saved = await SaveSheet(inB, await Get(inB), x => x with { Level = 7 });

        Assert.Equal(7, (await Get(inA)).Sheet.Level);
        Assert.Equal(saved.Version, (await Get(inA)).Version);
    }

    [Fact]
    public async Task Deleting_a_campaign_keeps_the_players_sheet()
    {
        var name = $"Tamsin {Suffix()}";
        var (masterA, inA) = await Table(name);
        await SaveSheet(inA, await Get(inA), x => x with { CharacterName = "Tamsin" });
        await AddItem(inA, Weapon("Adaga", "1d4"));

        var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/campaigns/{masterA.CampaignId}");
        delete.Headers.Add("X-Admin-Key", "integration-admin-secret");
        var deleted = await Client.SendAsync(delete);
        Assert.True(deleted.IsSuccessStatusCode, $"admin delete: {(int)deleted.StatusCode}");

        var (_, inB) = await Table(name);
        var sheet = await Get(inB);
        Assert.Equal("Tamsin", sheet.Sheet.CharacterName);
        Assert.Equal("Adaga", sheet.Items.Single().Data.Name);
    }

    [Fact]
    public async Task Different_names_never_share_a_sheet()
    {
        var (_, a) = await Table($"Ana {Suffix()}");
        var (_, b) = await Table($"Bia {Suffix()}");
        await SaveSheet(a, await Get(a), x => x with { CharacterName = "Ana" });
        Assert.Null((await Get(b)).Sheet.CharacterName);
        var missing = await Send(HttpMethod.Get, $"{Base(b)}?ifExists=true", b);
        Assert.Equal(HttpStatusCode.OK, missing.StatusCode);
    }
}
