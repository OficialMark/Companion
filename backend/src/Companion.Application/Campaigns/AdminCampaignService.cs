using Companion.Application.Contracts;
using Companion.Domain.Models;

namespace Companion.Application.Campaigns;

public sealed class AdminCampaignService(
    ICompanionDbContext db,
    IAdminCredentialValidator credentials,
    IPresenceTracker presence,
    ICampaignNotifier notifier)
{
    public IReadOnlyList<AdminCampaignDto> List(string credential)
    {
        Authorize(credential);
        var campaigns = db.Campaigns.OrderByDescending(x => x.CreatedAt).ToArray();
        var ids = campaigns.Select(x => x.Id).ToHashSet();
        var participants = db.Participants.Where(x => ids.Contains(x.CampaignId)).ToArray();
        var codes = db.CampaignAccessCodes.Where(x => ids.Contains(x.CampaignId)).ToArray();

        return campaigns.Select(campaign =>
        {
            var campaignParticipants = participants.Where(x => x.CampaignId == campaign.Id).ToArray();
            return new AdminCampaignDto(
                campaign.Id,
                campaign.Name,
                campaign.CreatedAt,
                campaignParticipants.Length,
                campaignParticipants.Count(x => presence.IsOnline(x.Id)),
                codes.Single(x => x.CampaignId == campaign.Id && x.Role == ParticipantRole.Player).Code,
                codes.Single(x => x.CampaignId == campaign.Id && x.Role == ParticipantRole.Master).Code);
        }).ToArray();
    }

    public async Task DeleteAsync(Guid campaignId, string credential, CancellationToken cancellationToken)
    {
        Authorize(credential);
        if (!await db.DeleteCampaignAggregateAsync(campaignId, cancellationToken))
        {
            throw new AppException(AppErrorKind.NotFound, "Mesa não encontrada.");
        }

        await notifier.NotifyAsync(campaignId, "CampaignDeleted", new { campaignId }, cancellationToken);
    }

    private void Authorize(string credential)
    {
        if (!credentials.IsValid(credential))
        {
            throw new AppException(AppErrorKind.Unauthorized, "Credencial administrativa inválida.");
        }
    }
}
