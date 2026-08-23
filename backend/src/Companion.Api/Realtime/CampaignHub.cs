using Companion.Application.Campaigns;
using Companion.Application.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace Companion.Api.Realtime;

public sealed class CampaignHub(
    ParticipantAuthorization authorization,
    ICompanionDbContext db,
    IPresenceTracker presence,
    ICampaignNotifier notifier) : Hub
{
    public static string GroupName(Guid campaignId) => $"campaign:{campaignId:N}";

    public async Task JoinCampaign(Guid campaignId, string participantToken)
    {
        var participant = authorization.Require(campaignId, participantToken);
        Context.Items["ParticipantId"] = participant.Id;
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(campaignId));
        if (presence.Connected(participant.Id, Context.ConnectionId))
        {
            await notifier.ParticipantsChangedAsync(campaignId, Context.ConnectionAborted);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (presence.Disconnected(Context.ConnectionId, out var participantId))
        {
            var campaignId = db.Participants.Where(x => x.Id == participantId)
                .Select(x => x.CampaignId).FirstOrDefault();
            if (campaignId != Guid.Empty)
            {
                await notifier.ParticipantsChangedAsync(campaignId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}
