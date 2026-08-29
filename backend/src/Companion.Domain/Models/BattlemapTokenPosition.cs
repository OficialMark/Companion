namespace Companion.Domain.Models;

public sealed class BattlemapTokenPosition
{
    private BattlemapTokenPosition() { }

    public BattlemapTokenPosition(
        Guid id,
        Guid battlemapId,
        Guid participantId,
        int column,
        int row,
        DateTimeOffset updatedAt)
    {
        Id = id;
        BattlemapId = battlemapId;
        ParticipantId = participantId;
        Move(column, row, updatedAt);
    }

    public Guid Id { get; private set; }
    public Guid BattlemapId { get; private set; }
    public Guid? ParticipantId { get; private set; }
    public Guid? CombatantId { get; private set; }
    public int Column { get; private set; }
    public int Row { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public byte[] Version { get; private set; } = [];

    public static BattlemapTokenPosition ForCombatant(
        Guid id,
        Guid battlemapId,
        Guid combatantId,
        int column,
        int row,
        DateTimeOffset updatedAt)
    {
        var position = new BattlemapTokenPosition
        {
            Id = id,
            BattlemapId = battlemapId,
            CombatantId = combatantId
        };
        position.Move(column, row, updatedAt);
        return position;
    }

    public void Move(int column, int row, DateTimeOffset updatedAt)
    {
        if (column is < 0 or >= Battlemap.MaxGridSize)
            throw new ArgumentOutOfRangeException(nameof(column), "A coluna deve estar entre 0 e 99.");
        if (row is < 0 or >= Battlemap.MaxGridSize)
            throw new ArgumentOutOfRangeException(nameof(row), "A linha deve estar entre 0 e 99.");
        Column = column;
        Row = row;
        UpdatedAt = updatedAt;
    }
}
