using Companion.Domain.Characters;

namespace Companion.Application.Characters;

// Spec 011, contracts/character-api.md. Versions travel as base64 rowversions.

public sealed record InventoryItemDto(Guid Id, int SortOrder, Guid? CatalogItemId, string Version, InventoryItemData Data);

public sealed record CharacterSpellDto(Guid Id, int SortOrder, Guid? CatalogSpellId, CharacterSpellData Data);

public sealed record SavedRollDto(Guid Id, int SortOrder, bool Incomplete, SavedRollData Data);

/// <summary>Every read and write of the sheet answers with the whole character, already recalculated.</summary>
public sealed record CharacterDto(
    Guid ParticipantId,
    CharacterSheetData Sheet,
    string Version,
    IReadOnlyList<InventoryItemDto> Items,
    IReadOnlyList<CharacterSpellDto> Spells,
    IReadOnlyList<SavedRollDto> SavedRolls,
    CharacterStats Stats,
    IReadOnlyList<string> Notices);

public sealed record SaveSheetRequest(CharacterSheetData? Data, string? Version);

public sealed record SaveItemRequest(InventoryItemData? Data, string? Version);

public sealed record AddFromCatalogRequest(Guid CatalogItemId, int? Quantity);

public sealed record EquipItemRequest(bool Equipped);

public sealed record SaveSpellRequest(CharacterSpellData? Data);

public sealed record AddSpellFromCatalogRequest(Guid CatalogSpellId);

public sealed record SaveRollRequest(SavedRollData? Data);
