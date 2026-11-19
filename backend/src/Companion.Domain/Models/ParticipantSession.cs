namespace Companion.Domain.Models;

/// <summary>
/// Uma sessão (aparelho) de um participante (spec 011, FR-043). Uma conta pode ter várias sessões
/// ao mesmo tempo; sair da campanha apaga só a sessão daquele aparelho. A autorização procura a
/// sessão pelo hash do token (índice único), nunca varrendo os participantes da campanha.
/// </summary>
public sealed class ParticipantSession
{
    public const int TokenHashLength = 32;

    /// <summary>Intervalo mínimo entre duas gravações de <see cref="LastSeenAt"/>: evita uma escrita por requisição.</summary>
    public static readonly TimeSpan LastSeenResolution = TimeSpan.FromHours(1);

    private ParticipantSession() { }

    public ParticipantSession(Guid id, Guid participantId, Guid campaignId, byte[] tokenHash, DateTimeOffset createdAt)
    {
        if (tokenHash is not { Length: TokenHashLength })
        {
            throw new ArgumentException("O hash da sessão deve ter 32 bytes.", nameof(tokenHash));
        }

        Id = id;
        ParticipantId = participantId;
        CampaignId = campaignId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        LastSeenAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid ParticipantId { get; private set; }
    /// <summary>Desnormalizado de <see cref="Participant.CampaignId"/> para a autorização conferir a campanha sem join.</summary>
    public Guid CampaignId { get; private set; }
    public byte[] TokenHash { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }

    /// <summary>True quando a última visita registrada tem uma hora ou mais.</summary>
    public bool IsLastSeenStale(DateTimeOffset now) => now - LastSeenAt >= LastSeenResolution;
}
