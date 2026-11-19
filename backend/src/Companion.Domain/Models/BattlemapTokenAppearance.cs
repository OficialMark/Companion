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
