namespace Companion.Domain.Characters;

/// <summary>
/// A player's sheet (spec 011). One per player participant; the key is the participant id.
/// The editable fields live in <see cref="Data"/> (a JSON column): nothing is queried by them,
/// and keeping them in one validated record means one place defines what a sheet holds.
/// </summary>
public sealed class CharacterSheet
{
    private CharacterSheet() { }

    public CharacterSheet(Guid participantId, DateTimeOffset now)
    {
        ParticipantId = participantId;
        Data = CharacterSheetData.Default;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid ParticipantId { get; private set; }
    public CharacterSheetData Data { get; private set; } = CharacterSheetData.Default;
    public byte[] Version { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(CharacterSheetData data, DateTimeOffset now)
    {
        Data = data.Validated();
        UpdatedAt = now;
    }

    /// <summary>Current HP never exceeds the final maximum (edge case: unequipping a HP item).</summary>
    public bool ClampCurrentHitPoints(int finalMaxHitPoints, DateTimeOffset now)
    {
        if (Data.CurrentHitPoints <= finalMaxHitPoints) return false;
        Data = Data with { CurrentHitPoints = finalMaxHitPoints };
        UpdatedAt = now;
        return true;
    }
}

public sealed class InventoryItem
{
    public const int MaxItemsPerCharacter = 200;

    private InventoryItem() { }

    public InventoryItem(Guid id, Guid participantId, int sortOrder, InventoryItemData data, Guid? catalogItemId, DateTimeOffset now)
    {
        Id = id;
        ParticipantId = participantId;
        SortOrder = sortOrder;
        Data = data.Validated();
        CatalogItemId = catalogItemId;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid ParticipantId { get; private set; }
    public int SortOrder { get; private set; }
    public Guid? CatalogItemId { get; private set; }
    public InventoryItemData Data { get; private set; } = null!;
    public byte[] Version { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }

    public ItemEntry ToEntry() => new(Id, SortOrder, Data);

    public void Update(InventoryItemData data) => Data = data.Validated();

    public void SetEquipped(bool equipped) => Data = (Data with { Equipped = equipped }).Validated();
}

public sealed class CharacterSpell
{
    public const int MaxSpellsPerCharacter = 300;

    private CharacterSpell() { }

    public CharacterSpell(Guid id, Guid participantId, int sortOrder, CharacterSpellData data, Guid? catalogSpellId, DateTimeOffset now)
    {
        Id = id;
        ParticipantId = participantId;
        SortOrder = sortOrder;
        Data = data.Validated();
        CatalogSpellId = catalogSpellId;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid ParticipantId { get; private set; }
    public int SortOrder { get; private set; }
    public Guid? CatalogSpellId { get; private set; }
    public CharacterSpellData Data { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    public SpellEntry ToEntry() => new(Id, Data);

    public void Update(CharacterSpellData data) => Data = data.Validated();
}

public sealed class SavedRoll
{
    public const int MaxRollsPerCharacter = 60;

    private SavedRoll() { }

    public SavedRoll(Guid id, Guid participantId, int sortOrder, SavedRollData data, DateTimeOffset now)
    {
        Id = id;
        ParticipantId = participantId;
        SortOrder = sortOrder;
        Data = data.Validated();
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid ParticipantId { get; private set; }
    public int SortOrder { get; private set; }
    public SavedRollData Data { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    public void Update(SavedRollData data) => Data = data.Validated();
}
