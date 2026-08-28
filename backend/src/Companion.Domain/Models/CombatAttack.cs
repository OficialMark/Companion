namespace Companion.Domain.Models;

public sealed class CombatAttack
{
    private CombatAttack() { }

    public CombatAttack(Guid id, Guid encounterId, Guid authorParticipantId, Guid diceRollId, Guid clientRequestId, int damage, DateTimeOffset createdAt)
    {
        if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
        Id = id;
        EncounterId = encounterId;
        AuthorParticipantId = authorParticipantId;
        DiceRollId = diceRollId;
        ClientRequestId = clientRequestId;
        Damage = damage;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid EncounterId { get; private set; }
    public Guid AuthorParticipantId { get; private set; }
    public Guid DiceRollId { get; private set; }
    public Guid ClientRequestId { get; private set; }
    public int Damage { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}