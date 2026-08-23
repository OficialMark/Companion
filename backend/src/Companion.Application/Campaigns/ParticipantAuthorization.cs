using System.Security.Cryptography;
using Companion.Application.Contracts;
using Companion.Domain.Models;

namespace Companion.Application.Campaigns;

public sealed class ParticipantAuthorization(
    ICompanionDbContext db,
    IParticipantTokenService tokens)
{
    public Participant Require(Guid campaignId, string? rawToken, bool masterOnly = false)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            throw new AppException(AppErrorKind.Unauthorized, "A credencial do participante é obrigatória.");
        }

        byte[] hash;
        try
        {
            hash = tokens.Hash(rawToken);
        }
        catch (FormatException)
        {
            throw new AppException(AppErrorKind.Unauthorized, "A credencial do participante é inválida.");
        }

        var participant = db.Participants
            .Where(x => x.CampaignId == campaignId)
            .AsEnumerable()
            .FirstOrDefault(x => CryptographicOperations.FixedTimeEquals(x.TokenHash, hash));

        if (participant is null)
        {
            throw new AppException(AppErrorKind.Unauthorized, "A sessão não pertence a esta campanha.");
        }

        if (masterOnly && participant.Role != ParticipantRole.Master)
        {
            throw new AppException(AppErrorKind.Forbidden, "Somente o mestre pode executar esta ação.");
        }

        return participant;
    }
}
