namespace Companion.Domain.Models;

public sealed class AttackArea
{
    public const int MinRadius = 1;
    public const int MaxRadius = 100;
    public const int MinWidth = 1;
    public const int MaxWidth = 100;
    public const int MinDirectionDegrees = 0;
    public const int MaxDirectionDegrees = 359;

    private AttackArea() { }

    private AttackArea(Guid id, Guid battlemapId, Guid? participantId, Guid? combatantId, Guid? markerId, DateTimeOffset createdAt)
    {
        Id = id;
        BattlemapId = battlemapId;
        ParticipantId = participantId;
        CombatantId = combatantId;
        MarkerId = markerId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid BattlemapId { get; private set; }
    public Guid? ParticipantId { get; private set; }
    public Guid? CombatantId { get; private set; }
    public Guid? MarkerId { get; private set; }
    public AttackAreaShape Shape { get; private set; }
    public int Radius { get; private set; }
    public int? Width { get; private set; }
    public int? DirectionDegrees { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public byte[] Version { get; private set; } = [];

    public static AttackArea ForParticipant(
        Guid id, Guid battlemapId, Guid participantId,
        AttackAreaShape shape, int radius, int? width, int? directionDegrees, DateTimeOffset now)
    {
        var area = new AttackArea(id, battlemapId, participantId, null, null, now);
        area.Apply(shape, radius, width, directionDegrees, now);
        return area;
    }

    public static AttackArea ForCombatant(
        Guid id, Guid battlemapId, Guid combatantId,
        AttackAreaShape shape, int radius, int? width, int? directionDegrees, DateTimeOffset now)
    {
        var area = new AttackArea(id, battlemapId, null, combatantId, null, now);
        area.Apply(shape, radius, width, directionDegrees, now);
        return area;
    }

    public static AttackArea ForMarker(
        Guid id, Guid battlemapId, Guid markerId,
        AttackAreaShape shape, int radius, int? width, int? directionDegrees, DateTimeOffset now)
    {
        var area = new AttackArea(id, battlemapId, null, null, markerId, now);
        area.Apply(shape, radius, width, directionDegrees, now);
        return area;
    }

    public void Update(AttackAreaShape shape, int radius, int? width, int? directionDegrees, DateTimeOffset updatedAt) =>
        Apply(shape, radius, width, directionDegrees, updatedAt);

    private void Apply(AttackAreaShape shape, int radius, int? width, int? directionDegrees, DateTimeOffset updatedAt)
    {
        if (radius is < MinRadius or > MaxRadius)
            throw new ArgumentOutOfRangeException(nameof(radius), $"O raio/distância deve estar entre {MinRadius} e {MaxRadius}.");

        var directional = shape is AttackAreaShape.Cone or AttackAreaShape.Cylinder;
        if (directional)
        {
            if (width is null || width is < MinWidth or > MaxWidth)
                throw new ArgumentOutOfRangeException(nameof(width), $"A largura deve estar entre {MinWidth} e {MaxWidth}.");
            if (directionDegrees is null || directionDegrees is < MinDirectionDegrees or > MaxDirectionDegrees)
                throw new ArgumentOutOfRangeException(nameof(directionDegrees), $"A direção deve estar entre {MinDirectionDegrees} e {MaxDirectionDegrees} graus.");
        }
        else if (width is not null || directionDegrees is not null)
        {
            throw new ArgumentException("Largura e direção só se aplicam a cone ou cilindro.", nameof(width));
        }

        Shape = shape;
        Radius = radius;
        Width = directional ? width : null;
        DirectionDegrees = directional ? directionDegrees : null;
        UpdatedAt = updatedAt;
    }
}
