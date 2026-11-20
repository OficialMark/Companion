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
    IReadOnlyList<string> Notices,
    IReadOnlyList<CharacterFeatureDto> Features);

/// <summary>A class, species or feat feature with its optional use counter (spec 013, US2).</summary>
public sealed record CharacterFeatureDto(Guid Id, int SortOrder, CharacterFeatureData Data);

public sealed record SaveFeatureRequest(CharacterFeatureData? Data);

/// <summary>Uses spent (negative) or regained (positive); the server keeps the result between 0 and the maximum.</summary>
public sealed record FeatureUsesRequest(int Delta);

public sealed record MoveFeatureRequest(int Direction);

/// <summary>Nullable on purpose: an empty body must be refused, not read as the first enum value (a short rest).</summary>
public sealed record RestRequest(RestKind? Kind);

public sealed record SaveSheetRequest(CharacterSheetData? Data, string? Version);

public sealed record SaveItemRequest(InventoryItemData? Data, string? Version);

/// <summary><see cref="BaseCatalogItemId"/> is required for generic magic items ("Arma +1"): the mundane weapon or armor to copy the block from.</summary>
public sealed record AddFromCatalogRequest(Guid CatalogItemId, int? Quantity, Guid? BaseCatalogItemId = null);

public sealed record EquipItemRequest(bool Equipped);

public sealed record SaveSpellRequest(CharacterSpellData? Data);

public sealed record AddSpellFromCatalogRequest(Guid CatalogSpellId);

public sealed record SaveRollRequest(SavedRollData? Data);
