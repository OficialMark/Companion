using Companion.Domain.Characters;

namespace Companion.Domain.Models;

public sealed class Participant
{
    private Participant() { }

    public Participant(
        Guid id,
        Guid campaignId,
        string displayName,
        ParticipantRole role,
        byte[] tokenHash,
        DateTimeOffset createdAt)
    {
        Id = id;
        CampaignId = campaignId;
        Role = role;
        // Conta nova é dona do nome: recebe a NameKey (FR-042). Lança ArgumentException se o nome for inválido.
        Rename(displayName, ownsName: true);
        TokenHash = tokenHash;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    /// <summary>
    /// Identidade da conta na campanha e no papel (FR-042): <see cref="PersonName.Key"/> do nome.
    /// Nula para participantes antigos com nome repetido que não são o mais recente do grupo (FR-044):
    /// eles seguem acessíveis pelas sessões que já têm, mas o join pelo nome não os encontra.
    /// </summary>
    public string? NameKey { get; private set; }
    public ParticipantRole Role { get; private set; }
    public byte[] TokenHash { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public int? InitiativeModifier { get; private set; }
    public int? CurrentHitPoints { get; private set; }
    public int? MaxHitPoints { get; private set; }
    /// <summary>Revision of the current profile photo; null when the participant has none.</summary>
    public Guid? AvatarRevision { get; private set; }

    /// <summary>
    /// Guarda o nome normalizado (FR-041). <paramref name="ownsName"/> decide se este participante é a
    /// conta que o join pelo nome encontra; o backfill passa false para os repetidos mais antigos.
    /// </summary>
    /// <exception cref="ArgumentException">Nome vazio ou com mais de 40 caracteres depois de normalizado.</exception>
    public void Rename(string displayName, bool ownsName)
    {
        var normalized = PersonName.Normalize(displayName);
        DisplayName = normalized;
        NameKey = ownsName ? PersonName.Key(normalized) : null;
    }

    public void SetAvatarRevision(Guid? revision) => AvatarRevision = revision;

    public void UpdateInitiativeModifier(int? modifier) => UpdateCombatProfile(modifier, CurrentHitPoints, MaxHitPoints);

    public void UpdateCombatProfile(int? initiativeModifier, int? currentHitPoints, int? maxHitPoints = null)
    {
        if (initiativeModifier is < -100 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(initiativeModifier), "O modificador deve estar entre -100 e 100.");
        }

        if (currentHitPoints is < 0 or > 99_999)
        {
            throw new ArgumentOutOfRangeException(nameof(currentHitPoints), "A vida deve estar entre 0 e 99999.");
        }

        if (maxHitPoints is < 0 or > 99_999)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHitPoints), "A vida máxima deve estar entre 0 e 99999.");
        }

        InitiativeModifier = initiativeModifier;
        CurrentHitPoints = currentHitPoints;
        MaxHitPoints = maxHitPoints;
    }
}