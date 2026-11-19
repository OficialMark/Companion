using System.Net;
using System.Net.Http.Json;
using Companion.Application.Campaigns;
using Companion.Application.Contracts;
using Companion.Domain.Models;
using Companion.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Companion.IntegrationTests;

/// <summary>Spec 011, User Story 2b: conta pelo nome, várias sessões e "Sair" por aparelho.</summary>
public sealed class AccountJoinTests(CompanionApiFactory factory) : ApiTestBase(factory)
{
    private CompanionApiFactory Factory { get; } = factory;

    [Fact]
    public async Task Scenario1_new_name_creates_the_normalized_account()
    {
        var master = await CreateCampaign(Suffix());
        var before = Joined(master.CampaignId);

        var loren = await Join(master.PlayerCode, "  loren  ");

        Assert.Equal("Loren", loren.DisplayName);
        Assert.Equal(ParticipantRole.Player, loren.Role);
        Assert.Equal(before + 1, Joined(master.CampaignId));
        var snapshot = await Snapshot(master.CampaignId, loren);
        Assert.Equal(2, snapshot.Participants.Count);
        Assert.Contains(snapshot.Participants, x => x.Id == loren.ParticipantId && x.DisplayName == "Loren");
    }

    [Fact]
    public async Task Scenario2_same_name_in_any_case_opens_the_existing_account()
    {
        var master = await CreateCampaign(Suffix());
        var first = await Join(master.PlayerCode, "loren b. vonn neuman");
        var joinedAfterFirst = Joined(master.CampaignId);

        var second = await Join(master.PlayerCode, "loren b. VONN neuman");
        var third = await Join(master.PlayerCode, "LOREN B. VONN NEUMAN");

        Assert.Equal("Loren B. Vonn Neuman", first.DisplayName);
        Assert.Equal(first.ParticipantId, second.ParticipantId);
        Assert.Equal(first.ParticipantId, third.ParticipantId);
        Assert.Equal("Loren B. Vonn Neuman", third.DisplayName);
        Assert.Equal(joinedAfterFirst, Joined(master.CampaignId));
        var snapshot = await Snapshot(master.CampaignId, third);
        Assert.Equal(2, snapshot.Participants.Count);
    }

    [Fact]
    public async Task Scenario3_a_second_device_keeps_both_sessions_valid()
    {
        var master = await CreateCampaign(Suffix());
        var phone = await Join(master.PlayerCode, "Loren");
        var computer = await Join(master.PlayerCode, "loren");

        Assert.Equal(phone.ParticipantId, computer.ParticipantId);
        Assert.NotEqual(phone.ParticipantToken, computer.ParticipantToken);
        Assert.Equal(phone.ParticipantId, (await Snapshot(master.CampaignId, phone)).CurrentParticipant.Id);
        Assert.Equal(phone.ParticipantId, (await Snapshot(master.CampaignId, computer)).CurrentParticipant.Id);
    }

    [Fact]
    public async Task Scenario4_same_name_in_another_campaign_is_another_account()
    {
        var campaignA = await CreateCampaign(Suffix());
        var campaignB = await CreateCampaign(Suffix());
        var inA = await Join(campaignA.PlayerCode, "Loren");
        var inB = await Join(campaignB.PlayerCode, "Loren");

        Assert.NotEqual(inA.ParticipantId, inB.ParticipantId);
        var crossRequest = Authenticated(HttpMethod.Get, $"/api/campaigns/{campaignB.CampaignId}/snapshot", inA);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.SendAsync(crossRequest)).StatusCode);
    }

    [Fact]
    public async Task Scenario5_player_and_master_accounts_with_the_same_name_never_mix()
    {
        var campaign = await CreateCampaign(Suffix());
        var player = await Join(campaign.PlayerCode, "Loren");
        var masterLoren = await Join(campaign.MasterCode!, "Loren");
        var masterAgain = await Join(campaign.MasterCode!, "LOREN");

        Assert.NotEqual(player.ParticipantId, masterLoren.ParticipantId);
        Assert.Equal(ParticipantRole.Player, player.Role);
        Assert.Equal(ParticipantRole.Master, masterLoren.Role);
        Assert.Equal(masterLoren.ParticipantId, masterAgain.ParticipantId);
        Assert.Null(masterLoren.MasterCode);
        Assert.Equal(ParticipantRole.Master, (await Snapshot(campaign.CampaignId, masterAgain)).CurrentParticipant.Role);
    }

    [Fact]
    public async Task Scenario6_legacy_duplicates_resolve_to_the_most_recent_and_lose_nothing()
    {
        var master = await CreateCampaign(Suffix());
        var tokens = Services.GetRequiredService<IParticipantTokenService>();
        var olderToken = tokens.Create();
        var newerToken = tokens.Create();
        var olderId = Guid.NewGuid();
        var newerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var scope = Services.CreateAsyncScope())
        {
            // Como estava antes da spec 011: dois "loren" no mesmo papel, sem NameKey, cada um com seu token.
            var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
            await InsertLegacyParticipant(db, olderId, master.CampaignId, "loren", olderToken.Hash, now.AddDays(-2));
            await InsertLegacyParticipant(db, newerId, master.CampaignId, "loren", newerToken.Hash, now.AddDays(-1));
            await scope.ServiceProvider.GetRequiredService<ParticipantNameBackfill>().RunAsync();
        }

        var joined = await Join(master.PlayerCode, "Loren");

        Assert.Equal(newerId, joined.ParticipantId);
        var olderSession = new SessionResponse(master.CampaignId, master.PlayerCode, null, olderId, "Loren", ParticipantRole.Player, olderToken.Token);
        var olderSnapshot = await Snapshot(master.CampaignId, olderSession);
        Assert.Equal(olderId, olderSnapshot.CurrentParticipant.Id);
        Assert.Equal("Loren", olderSnapshot.CurrentParticipant.DisplayName);
        Assert.Equal(3, olderSnapshot.Participants.Count);
        Assert.Equal(2, olderSnapshot.Participants.Count(x => x.DisplayName == "Loren"));
    }

    [Fact]
    public async Task Ten_concurrent_joins_with_a_new_name_create_exactly_one_account()
    {
        var master = await CreateCampaign(Suffix());
        var name = $"corrida {Suffix()}";
        var before = Joined(master.CampaignId);

        var sessions = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Join(master.PlayerCode, name)));

        Assert.Single(sessions.Select(x => x.ParticipantId).Distinct());
        Assert.Equal(10, sessions.Select(x => x.ParticipantToken).Distinct().Count());
        Assert.Equal(before + 1, Joined(master.CampaignId));
        var snapshot = await Snapshot(master.CampaignId, sessions[0]);
        Assert.Equal(2, snapshot.Participants.Count);
        foreach (var session in sessions)
            Assert.Equal(sessions[0].ParticipantId, (await Snapshot(master.CampaignId, session)).CurrentParticipant.Id);
    }

    [Fact]
    public async Task Leave_deletes_only_the_session_of_that_device()
    {
        var master = await CreateCampaign(Suffix());
        var phone = await Join(master.PlayerCode, "Loren");
        var computer = await Join(master.PlayerCode, "Loren");

        var leave = await Client.SendAsync(Authenticated(HttpMethod.Delete,
            $"/api/campaigns/{master.CampaignId}/participants/me/session", phone));

        Assert.Equal(HttpStatusCode.NoContent, leave.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.SendAsync(Authenticated(HttpMethod.Get,
            $"/api/campaigns/{master.CampaignId}/snapshot", phone))).StatusCode);
        Assert.Equal(phone.ParticipantId, (await Snapshot(master.CampaignId, computer)).CurrentParticipant.Id);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.SendAsync(Authenticated(HttpMethod.Delete,
            $"/api/campaigns/{master.CampaignId}/participants/me/session", phone))).StatusCode);

        // Voltar pelo nome depois de sair abre a mesma conta.
        var back = await Join(master.PlayerCode, "LOREN");
        Assert.Equal(phone.ParticipantId, back.ParticipantId);
    }

    [Fact]
    public async Task Leave_with_a_token_of_another_campaign_is_unauthorized_and_deletes_nothing()
    {
        var campaignA = await CreateCampaign(Suffix());
        var campaignB = await CreateCampaign(Suffix());
        var player = await Join(campaignA.PlayerCode, "Loren");

        var leave = await Client.SendAsync(Authenticated(HttpMethod.Delete,
            $"/api/campaigns/{campaignB.CampaignId}/participants/me/session", player));

        Assert.Equal(HttpStatusCode.Unauthorized, leave.StatusCode);
        Assert.Equal(player.ParticipantId, (await Snapshot(campaignA.CampaignId, player)).CurrentParticipant.Id);
    }

    [Fact]
    public async Task Create_campaign_normalizes_the_master_name_and_its_session_works()
    {
        var response = await Client.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest("Mesa", "  ana   MARIA  "));
        response.EnsureSuccessStatusCode();
        var created = (await response.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions))!;

        Assert.Equal("Ana Maria", created.DisplayName);
        var snapshot = await Snapshot(created.CampaignId, created);
        Assert.Equal(ParticipantRole.Master, snapshot.CurrentParticipant.Role);
        // O mesmo nome com o código de mestre volta para a conta de quem criou a mesa.
        var again = await Join(created.MasterCode!, "ana maria");
        Assert.Equal(created.ParticipantId, again.ParticipantId);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Invalid_names_are_rejected(string name)
    {
        var master = await CreateCampaign(Suffix());
        var response = await Client.PostAsJsonAsync("/api/campaigns/join", new JoinCampaignRequest(master.PlayerCode, name));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Last_seen_is_written_only_when_older_than_one_hour()
    {
        var master = await CreateCampaign(Suffix());
        var player = await Join(master.PlayerCode, "Loren");
        var recent = DateTimeOffset.UtcNow.AddMinutes(-10);
        var stale = DateTimeOffset.UtcNow.AddHours(-3);

        await SetLastSeen(player.ParticipantId, recent);
        await Snapshot(master.CampaignId, player);
        Assert.Equal(recent, await GetLastSeen(player.ParticipantId), TimeSpan.FromMilliseconds(1));

        await SetLastSeen(player.ParticipantId, stale);
        await Snapshot(master.CampaignId, player);
        Assert.True(await GetLastSeen(player.ParticipantId) > DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    private static string Suffix() => Guid.NewGuid().ToString("N")[..6];

    private int Joined(Guid campaignId) => Factory.Notifications.For(campaignId).Count(x => x.Type == "ParticipantJoined");

    private async Task<SessionResponse> Join(string code, string name)
    {
        var response = await Client.PostAsJsonAsync("/api/campaigns/join", new JoinCampaignRequest(code, name));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions))!;
    }

    private async Task<CampaignSnapshot> Snapshot(Guid campaignId, SessionResponse session)
    {
        var response = await Client.SendAsync(Authenticated(HttpMethod.Get, $"/api/campaigns/{campaignId}/snapshot", session));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CampaignSnapshot>(JsonOptions))!;
    }

    private static Task InsertLegacyParticipant(
        CompanionDbContext db, Guid id, Guid campaignId, string name, byte[] tokenHash, DateTimeOffset createdAt) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [Participants] ([Id], [CampaignId], [DisplayName], [Role], [TokenHash], [CreatedAt])
            VALUES ({id}, {campaignId}, {name}, 'Player', {tokenHash}, {createdAt});
            INSERT INTO [ParticipantSessions] ([Id], [ParticipantId], [CampaignId], [TokenHash], [CreatedAt], [LastSeenAt])
            VALUES (NEWID(), {id}, {campaignId}, {tokenHash}, {createdAt}, {createdAt});
            """);

    private async Task SetLastSeen(Guid participantId, DateTimeOffset value)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        await db.ParticipantSessions.Where(x => x.ParticipantId == participantId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.LastSeenAt, value));
    }

    private async Task<DateTimeOffset> GetLastSeen(Guid participantId)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        return await db.ParticipantSessions.Where(x => x.ParticipantId == participantId).Select(x => x.LastSeenAt).SingleAsync();
    }
}
