using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Companion.Application.Contracts;

namespace Companion.IntegrationTests;

public sealed class ParticipantAvatarApiTests(CompanionApiFactory factory) : ApiTestBase(factory)
{
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public async Task Participant_uploads_photo_that_other_participants_can_read_and_then_removes_it()
    {
        var master = await CreateCampaign("avatar");
        var player = await Join(master, "Lia");

        var upload = await Upload(player, TinyPng, "image/png");
        upload.EnsureSuccessStatusCode();
        var updated = (await upload.Content.ReadFromJsonAsync<ParticipantDto>(JsonOptions))!;
        Assert.NotNull(updated.AvatarRevision);

        var snapshot = await Snapshot(master);
        var listed = Assert.Single(snapshot.Participants, x => x.Id == player.ParticipantId);
        Assert.Equal(updated.AvatarRevision, listed.AvatarRevision);

        var image = await Client.SendAsync(Authenticated(HttpMethod.Get,
            $"/api/campaigns/{master.CampaignId}/participants/{player.ParticipantId}/avatar?revision={updated.AvatarRevision}", master));
        image.EnsureSuccessStatusCode();
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        Assert.Equal(TinyPng, await image.Content.ReadAsByteArrayAsync());

        var removed = await Client.SendAsync(Authenticated(HttpMethod.Delete,
            $"/api/campaigns/{player.CampaignId}/participants/me/avatar", player));
        removed.EnsureSuccessStatusCode();
        Assert.Null((await removed.Content.ReadFromJsonAsync<ParticipantDto>(JsonOptions))!.AvatarRevision);

        var stale = await Client.SendAsync(Authenticated(HttpMethod.Get,
            $"/api/campaigns/{master.CampaignId}/participants/{player.ParticipantId}/avatar?revision={updated.AvatarRevision}", master));
        Assert.Equal(HttpStatusCode.NotFound, stale.StatusCode);
    }

    [Fact]
    public async Task Replacing_photo_issues_a_new_revision()
    {
        var master = await CreateCampaign("avatar-replace");
        var first = (await (await Upload(master, TinyPng, "image/png")).Content.ReadFromJsonAsync<ParticipantDto>(JsonOptions))!;
        var second = (await (await Upload(master, TinyPng, "image/png")).Content.ReadFromJsonAsync<ParticipantDto>(JsonOptions))!;
        Assert.NotNull(first.AvatarRevision);
        Assert.NotEqual(first.AvatarRevision, second.AvatarRevision);
    }

    [Fact]
    public async Task Rejects_invalid_or_oversized_photos()
    {
        var master = await CreateCampaign("avatar-invalid");

        var fake = await Upload(master, "not an image"u8.ToArray(), "image/png");
        Assert.Equal(HttpStatusCode.BadRequest, fake.StatusCode);

        var oversized = new byte[1024 * 1024 + 1];
        TinyPng.CopyTo(oversized, 0);
        var tooLarge = await Upload(master, oversized, "image/png");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLarge.StatusCode);
    }

    [Fact]
    public async Task Outsider_cannot_read_a_campaign_photo()
    {
        var owner = await CreateCampaign("avatar-owner");
        var outsider = await CreateCampaign("avatar-outsider");
        var updated = (await (await Upload(owner, TinyPng, "image/png")).Content.ReadFromJsonAsync<ParticipantDto>(JsonOptions))!;

        var crossCampaign = await Client.SendAsync(Authenticated(HttpMethod.Get,
            $"/api/campaigns/{owner.CampaignId}/participants/{owner.ParticipantId}/avatar?revision={updated.AvatarRevision}", outsider));
        Assert.Equal(HttpStatusCode.Unauthorized, crossCampaign.StatusCode);

        var wrongCampaignPath = await Client.SendAsync(Authenticated(HttpMethod.Get,
            $"/api/campaigns/{outsider.CampaignId}/participants/{owner.ParticipantId}/avatar?revision={updated.AvatarRevision}", outsider));
        Assert.Equal(HttpStatusCode.NotFound, wrongCampaignPath.StatusCode);
    }

    private Task<HttpResponseMessage> Upload(SessionResponse session, byte[] content, string mediaType)
    {
        var request = Authenticated(HttpMethod.Put, $"/api/campaigns/{session.CampaignId}/participants/me/avatar", session);
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        form.Add(file, "image", "avatar.png");
        request.Content = form;
        return Client.SendAsync(request);
    }

    private async Task<SessionResponse> Join(SessionResponse master, string name)
    {
        var response = await Client.PostAsJsonAsync("/api/campaigns/join", new JoinCampaignRequest(master.PlayerCode, name));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions))!;
    }

    private async Task<CampaignSnapshot> Snapshot(SessionResponse session)
    {
        var response = await Client.SendAsync(Authenticated(HttpMethod.Get, $"/api/campaigns/{session.CampaignId}/snapshot", session));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CampaignSnapshot>(JsonOptions))!;
    }
}
