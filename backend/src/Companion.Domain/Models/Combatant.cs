namespace Companion.Domain.Models;

public sealed class Combatant
{
    private Combatant() { }

    public Combatant(Guid id, Guid encounterId, string name, int initiativeModifier, int creationOrder)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 60)
        {
            throw new ArgumentException("O nome do combatente deve ter entre 1 e 60 caracteres.", nameof(name));
        }

        if (initiativeModifier is < -100 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(initiativeModifier));
        }

        Id = id;
        EncounterId = encounterId;
        Name = name.Trim();
        InitiativeModifier = initiativeModifier;
        CreationOrder = creationOrder;
    }

    public Guid Id { get; private set; }
    public Guid EncounterId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int InitiativeModifier { get; private set; }
    public int? Initiative { get; private set; }
    public int CreationOrder { get; private set; }

    public void SetInitiative(int d20) => Initiative = checked(d20 + InitiativeModifier);
}
