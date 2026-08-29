using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Companion.Application.Contracts;

namespace Companion.IntegrationTests;

public sealed class BattlemapSecurityApiTests(CompanionApiFactory factory) : ApiTestBase(factory)
{
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public async Task Cross_campaign_token_cannot_read_battlemap()
    {
        var owner = await CreateCampaign("map-owner");
        var outsider = await CreateCampaign("map-outsider");
        var encounter = await CreateEncounter(owner);
        (await Configure(owner, encounter.Id, TinyPng, "safe.png")).EnsureSuccessStatusCode();

        var request = Authenticated(
            HttpMethod.Get,
            $"/api/campaigns/{owner.CampaignId}/encounters/{encounter.Id}/battlemap",
            outsider);
        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task File_name_is_sanitized_and_oversized_upload_is_rejected()
    {
        var master = await CreateCampaign("map-security");
        var encounter = await CreateEncounter(master);
        var configured = await Configure(master, encounter.Id, TinyPng, "../../private/map.png");
        configured.EnsureSuccessStatusCode();
        var result = (await configured.Content.ReadFromJsonAsync<ReplaceBattlemapResultDto>(JsonOptions))!;
        Assert.Equal("map.png", result.Battlemap.ImageFileName);

        var oversized = new byte[20 * 1024 * 1024 + 1];
        oversized[0] = 0x89; oversized[1] = 0x50; oversized[2] = 0x4E; oversized[3] = 0x47;
        var response = await Configure(master, encounter.Id, oversized, "too-large.png", result.Battlemap.Revision);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    private async Task<EncounterDto> CreateEncounter(SessionResponse session)
    {
        var response = await Client.SendAsync(Authenticated(
            HttpMethod.Post, $"/api/campaigns/{session.CampaignId}/encounters", session));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EncounterDto>(JsonOptions))!;
    }

    private Task<HttpResponseMessage> Configure(
        SessionResponse session,
        Guid encounterId,
        byte[] content,
        string fileName,
        string? expectedRevision = null)
    {
        var form = new MultipartFormDataContent();
        var image = new ByteArrayContent(content);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(image, "image", fileName);
        form.Add(new StringContent("10"), "columns");
        form.Add(new StringContent("10"), "rows");
        if (expectedRevision is not null) form.Add(new StringContent(expectedRevision), "expectedRevision");
        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/campaigns/{session.CampaignId}/encounters/{encounterId}/battlemap") { Content = form };
        request.Headers.Add("X-Participant-Token", session.ParticipantToken);
        return Client.SendAsync(request);
    }
}
