using Companion.Application.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace Companion.Api.Realtime;

public sealed class SignalRCampaignNotifier(
    IHubContext<CampaignHub> hub,
    ICompanionDbContext db,
    IPresenceTracker presence,
    TimeProvider timeProvider) : ICampaignNotifier
{
    public Task NotifyAsync(Guid campaignId, string type, object payload, CancellationToken cancellationToken = default)
    {
        var message = new CampaignEvent(Guid.NewGuid(), campaignId, type, timeProvider.GetUtcNow(), payload);
        return hub.Clients.Group(CampaignHub.GroupName(campaignId))
            .SendAsync("CampaignEvent", message, cancellationToken);
    }

    public Task EncounterInvalidatedAsync(Guid campaignId, Guid encounterId, CancellationToken cancellationToken = default) =>
        NotifyAsync(campaignId, "EncounterInvalidated", new EncounterInvalidationDto(encounterId), cancellationToken);

    public Task BattlemapInvalidatedAsync(Guid campaignId, Guid encounterId, string revision, CancellationToken cancellationToken = default) =>
        NotifyAsync(campaignId, "BattlemapInvalidated", new BattlemapInvalidationDto(encounterId, revision), cancellationToken);
    public Task ParticipantsChangedAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        var participants = db.Participants.Where(x => x.CampaignId == campaignId)
            .OrderBy(x => x.CreatedAt).AsEnumerable()
            .Select(x => new ParticipantDto(x.Id, x.DisplayName, x.Role, presence.IsOnline(x.Id), x.InitiativeModifier))
            .ToArray();
        var message = new CampaignEvent(
            Guid.NewGuid(), campaignId, "ParticipantsChanged", timeProvider.GetUtcNow(), participants);
        return hub.Clients.Group(CampaignHub.GroupName(campaignId))
            .SendAsync("ParticipantsChanged", message, cancellationToken);
    }
}
