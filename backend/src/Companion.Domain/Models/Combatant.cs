namespace Companion.Domain.Models;

public sealed class Combatant
{
    private Combatant() { }

    public Combatant(
        Guid id,
        Guid encounterId,
        string name,
        int initiativeModifier,
        int creationOrder,
        Guid? participantId = null,
        int? currentHitPoints = null,
        int? maxHitPoints = null)
    {
        Validate(name, initiativeModifier, currentHitPoints, maxHitPoints);
        Id = id;
        EncounterId = encounterId;
        Name = name.Trim();
        InitiativeModifier = initiativeModifier;
        CreationOrder = creationOrder;
        ParticipantId = participantId;
        CurrentHitPoints = currentHitPoints;
        MaxHitPoints = maxHitPoints;
    }

    public Guid Id { get; private set; }
    public Guid EncounterId { get; private set; }
    public Guid? ParticipantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int InitiativeModifier { get; private set; }
    public int? Initiative { get; private set; }
    public int? CurrentHitPoints { get; private set; }
    public int? MaxHitPoints { get; private set; }
    public int CreationOrder { get; private set; }
    public byte[] Version { get; private set; } = [];

    public void SetInitiative(int d20) => Initiative = checked(d20 + InitiativeModifier);

    public void Update(string name, int initiativeModifier, int? currentHitPoints = null, int? maxHitPoints = null)
    {
        Validate(name, initiativeModifier, currentHitPoints ?? CurrentHitPoints, maxHitPoints ?? MaxHitPoints);
        Name = name.Trim();
        InitiativeModifier = initiativeModifier;
        if (currentHitPoints.HasValue) CurrentHitPoints = currentHitPoints;
        if (maxHitPoints.HasValue) MaxHitPoints = maxHitPoints;
        Initiative = null;
    }

    public void UpdateHitPoints(int currentHitPoints)
    {
        if (currentHitPoints is < 0 or > 99_999)
        {
            throw new ArgumentOutOfRangeException(nameof(currentHitPoints), "A vida deve estar entre 0 e 99999.");
        }
        CurrentHitPoints = currentHitPoints;
    }

    public DamageApplication ApplyDamage(int damage)
    {
        if (CurrentHitPoints is null)
        {
            throw new InvalidOperationException("O combatente precisa ter vida informada.");
        }
        var normalized = Math.Max(0, damage);
        var before = CurrentHitPoints.Value;
        var after = Math.Max(0, before - normalized);
        CurrentHitPoints = after;
        return new DamageApplication(Id, before, after, before - after);
    }

    private static void Validate(string name, int initiativeModifier, int? currentHitPoints, int? maxHitPoints)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 60)
        {
            throw new ArgumentException("O nome do combatente deve ter entre 1 e 60 caracteres.", nameof(name));
        }
        if (initiativeModifier is < -100 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(initiativeModifier));
        }
        if (currentHitPoints is < 0 or > 99_999)
        {
            throw new ArgumentOutOfRangeException(nameof(currentHitPoints), "A vida deve estar entre 0 e 99999.");
        }
        if (maxHitPoints is < 0 or > 99_999)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHitPoints), "A vida máxima deve estar entre 0 e 99999.");
        }
    }
}

public sealed record DamageApplication(Guid CombatantId, int HitPointsBefore, int HitPointsAfter, int DamageApplied);