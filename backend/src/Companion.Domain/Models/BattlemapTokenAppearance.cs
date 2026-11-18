namespace Companion.Domain.Models;

public sealed class BattlemapTokenAppearance
{
    public const int MinFootprintSize = 1;
    public const int MaxFootprintSize = 5;

    private List<StatusCondition> _statusConditions = [];

    private BattlemapTokenAppearance() { }

    private BattlemapTokenAppearance(Guid id, Guid battlemapId, Guid? participantId, Guid? combatantId, Guid? markerId)
    {
        Id = id;
        BattlemapId = battlemapId;
        ParticipantId = participantId;
        CombatantId = combatantId;
        MarkerId = markerId;
        FootprintSize = MinFootprintSize;
    }

    public Guid Id { get; private set; }
    public Guid BattlemapId { get; private set; }
    public Guid? ParticipantId { get; private set; }
    public Guid? CombatantId { get; private set; }
    public Guid? MarkerId { get; private set; }
    public int FootprintSize { get; private set; }
    public IReadOnlyList<StatusCondition> StatusConditions => _statusConditions;
    public byte[] Version { get; private set; } = [];

    // Last roll configuration used for this token on this map (spec 008, US5).
    public static readonly IReadOnlySet<int> RollDice = new HashSet<int> { 4, 6, 8, 10, 12, 20, 100 };
    public int? LastRollCount { get; private set; }
    public int? LastRollDie { get; private set; }
    public string? LastRollAbility { get; private set; }
    public int? LastRollBonus { get; private set; }

    public void SetRollPreset(int count, int die, string? ability, int bonus)
    {
        if (count is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(count), "A quantidade de dados deve estar entre 1 e 20.");
        if (!RollDice.Contains(die)) throw new ArgumentOutOfRangeException(nameof(die), "Dado inválido.");
        if (ability is not null && !Ability.All.Contains(ability)) throw new ArgumentOutOfRangeException(nameof(ability), "Atributo inválido.");
        if (bonus is < -20 or > 20) throw new ArgumentOutOfRangeException(nameof(bonus), "O adicional deve estar entre -20 e +20.");
        LastRollCount = count;
        LastRollDie = die;
        LastRollAbility = ability;
        LastRollBonus = bonus;
    }

    public static BattlemapTokenAppearance ForParticipant(Guid id, Guid battlemapId, Guid participantId) =>
        new(id, battlemapId, participantId, null, null);

    public static BattlemapTokenAppearance ForCombatant(Guid id, Guid battlemapId, Guid combatantId) =>
        new(id, battlemapId, null, combatantId, null);

    public static BattlemapTokenAppearance ForMarker(Guid id, Guid battlemapId, Guid markerId) =>
        new(id, battlemapId, null, null, markerId);

    public void SetFootprintSize(int footprintSize)
    {
        if (footprintSize is < MinFootprintSize or > MaxFootprintSize)
        {
            throw new ArgumentOutOfRangeException(nameof(footprintSize),
                $"O tamanho do token deve estar entre {MinFootprintSize}x{MinFootprintSize} e {MaxFootprintSize}x{MaxFootprintSize}.");
        }
        FootprintSize = footprintSize;
    }

    public void SetStatusConditions(IReadOnlyCollection<StatusCondition> conditions)
    {
        _statusConditions = conditions.Distinct().ToList();
    }
}
