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
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 40)
        {
            throw new ArgumentException("O nome deve ter entre 1 e 40 caracteres.", nameof(displayName));
        }

        Id = id;
        CampaignId = campaignId;
        DisplayName = displayName.Trim();
        Role = role;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public ParticipantRole Role { get; private set; }
    public byte[] TokenHash { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public int? InitiativeModifier { get; private set; }

    public void UpdateInitiativeModifier(int? modifier)
    {
        if (modifier is < -100 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(modifier), "O modificador deve estar entre -100 e 100.");
        }

        InitiativeModifier = modifier;
    }
}
