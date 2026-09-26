namespace Companion.Domain.Models;

public sealed class BattlemapMarker
{
    private BattlemapMarker() { }

    public BattlemapMarker(Guid id, Guid battlemapId, Guid ownerParticipantId, string label, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(label) || label.Trim().Length > 40)
        {
            throw new ArgumentException("O nome do token deve ter entre 1 e 40 caracteres.", nameof(label));
        }

        Id = id;
        BattlemapId = battlemapId;
        OwnerParticipantId = ownerParticipantId;
        Label = label.Trim();
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid BattlemapId { get; private set; }
    public Guid OwnerParticipantId { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
}
