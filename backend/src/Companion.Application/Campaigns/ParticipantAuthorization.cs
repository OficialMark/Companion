using Companion.Application.Contracts;
using Companion.Domain.Models;

namespace Companion.Application.Campaigns;

public sealed class ParticipantAuthorization(
    ICompanionDbContext db,
    IParticipantTokenService tokens,
    TimeProvider timeProvider)
{
    public Participant Require(Guid campaignId, string? rawToken, bool masterOnly = false) =>
        RequireSession(campaignId, rawToken, masterOnly).Participant;

    /// <summary>
    /// Resolve o token pela sessão (spec 011, research R3): uma busca pelo índice único de
    /// <c>ParticipantSessions.TokenHash</c>, sem varrer os participantes da campanha. A sessão de
    /// outra campanha responde 401 com a mesma mensagem de uma sessão inexistente.
    /// </summary>
    public (Participant Participant, ParticipantSession Session) RequireSession(
        Guid campaignId, string? rawToken, bool masterOnly = false)
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

        var session = db.ParticipantSessions.FirstOrDefault(x => x.TokenHash == hash);
        var participant = session is null || session.CampaignId != campaignId
            ? null
            : db.Participants.FirstOrDefault(x => x.Id == session.ParticipantId && x.CampaignId == campaignId);

        if (session is null || participant is null)
        {
            throw new AppException(AppErrorKind.Unauthorized, "A sessão não pertence a esta campanha.");
        }

        if (masterOnly && participant.Role != ParticipantRole.Master)
        {
            throw new AppException(AppErrorKind.Forbidden, "Somente o mestre pode executar esta ação.");
        }

        // No máximo uma escrita por hora por sessão: a leitura acima já diz se está velha.
        var now = timeProvider.GetUtcNow();
        if (session.IsLastSeenStale(now))
        {
            db.TouchSession(session.Id, now);
        }

        return (participant, session);
    }
}
