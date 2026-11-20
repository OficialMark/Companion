namespace Companion.Domain.Characters;

/// <summary>
/// A player's sheet (spec 011). One per player participant; the key is the participant id.
/// The editable fields live in <see cref="Data"/> (a JSON column): nothing is queried by them,
/// and keeping them in one validated record means one place defines what a sheet holds.
/// </summary>
public sealed class CharacterSheet
{
    private CharacterSheet() { }

    public CharacterSheet(Guid participantId, string ownerKey, DateTimeOffset now)
    {
        ParticipantId = participantId;
        OwnerKey = ownerKey;
        Data = CharacterSheetData.Default;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// Row id, also the key items, spells and rolls hang from: the participant that first opened the
    /// sheet. It is no longer the owner: the sheet belongs to <see cref="OwnerKey"/> and survives that
    /// participant and its campaign (2026-11-20 decision).
    /// </summary>
    public Guid ParticipantId { get; private set; }

    /// <summary>
    /// The account: the normalized player name (<see cref="CharacterOwner.Key"/>). The same name in any
    /// campaign opens this same sheet. Unique; legacy duplicates keep a "LEGACY:" key and are unused.
    /// </summary>
    public string OwnerKey { get; private set; } = string.Empty;
    public CharacterSheetData Data { get; private set; } = CharacterSheetData.Default;
    public byte[] Version { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Moves the sheet to <paramref name="ownerKey"/>. Used when the migration's SQL key and
    /// <see cref="CharacterOwner.Key"/> disagree for the participant that created this row, so the
    /// player keeps their sheet instead of hitting the row's primary key on every open.
    /// </summary>
    public void AssignOwner(string ownerKey, DateTimeOffset now)
    {
        OwnerKey = ownerKey;
        UpdatedAt = now;
    }

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
