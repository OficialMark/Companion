using Companion.Domain.Models;

namespace Companion.Application.Creatures;

/// <summary>Where a creature row comes from, from the campaign's point of view.</summary>
public enum CreatureOrigin { Compendium, Edited, Custom }

public sealed record CreatureActionDto(string Name, string Description);

public sealed record CreatureSummaryDto(
    Guid Id,
    string Name,
    string? NameEn,
    CreatureSize Size,
    string Type,
    string ChallengeRating,
    int ArmorClass,
    int HitPoints,
    CreatureOrigin Origin,
    string? ImageUrl);

public sealed record CreatureDto(
    Guid Id,
    Guid? BaseCreatureId,
    CreatureOrigin Origin,
    string Name,
    string? NameEn,
    string SourceBook,
    string? SourceUrl,
    CreatureSize Size,
    string Type,
    string? Alignment,
    int ArmorClass,
    string? ArmorClassNote,
    int HitPoints,
    string? HitDice,
    string? Speed,
    int? Initiative,
    int Strength, int Dexterity, int Constitution, int Intelligence, int Wisdom, int Charisma,
    string? SavingThrows,
    string? Skills,
    string? DamageVulnerabilities,
    string? DamageResistances,
    string? DamageImmunities,
    string? ConditionImmunities,
    string? Senses,
    string? Languages,
    string ChallengeRating,
    int? ExperiencePoints,
    int? ProficiencyBonus,
    IReadOnlyList<CreatureActionDto> Traits,
    IReadOnlyList<CreatureActionDto> Actions,
    IReadOnlyList<CreatureActionDto> BonusActions,
    IReadOnlyList<CreatureActionDto> Reactions,
    IReadOnlyList<CreatureActionDto> LegendaryActions,
    string? LegendaryActionsIntro,
    string? Notes,
    string? ImageUrl,
    bool HasImage,
    int FootprintSize,
    int InitiativeModifier,
    string Revision);

/// <summary>Spec 016: what the board token picker needs of a creature, and nothing more (no stat block).</summary>
public sealed record CreatureTokenOptionDto(Guid Id, string Name, CreatureSize Size, string Type, string? ImageUrl);
public sealed record CreatureTokenOptionPageDto(IReadOnlyList<CreatureTokenOptionDto> Items, int Total, int Page, int PageSize);

public sealed record CreaturePageDto(IReadOnlyList<CreatureSummaryDto> Items, int Total, int Page, int PageSize);

public sealed record CreatureQuery(
    string? Q = null,
    CreatureSize? Size = null,
    string? Type = null,
    decimal? CrMin = null,
    decimal? CrMax = null,
    CreatureOrigin? Origin = null,
    int Page = 1,
    int PageSize = 30);

public sealed record SaveCreatureRequest(
    string Name,
    string? NameEn,
    CreatureSize Size,
    string Type,
    string? Alignment,
    int ArmorClass,
    string? ArmorClassNote,
    int HitPoints,
    string? HitDice,
    string? Speed,
    int? Initiative,
    int Strength, int Dexterity, int Constitution, int Intelligence, int Wisdom, int Charisma,
    string? SavingThrows,
    string? Skills,
    string? DamageVulnerabilities,
    string? DamageResistances,
    string? DamageImmunities,
    string? ConditionImmunities,
    string? Senses,
    string? Languages,
    string ChallengeRating,
    int? ExperiencePoints,
    int? ProficiencyBonus,
    IReadOnlyList<CreatureActionDto>? Traits,
    IReadOnlyList<CreatureActionDto>? Actions,
    IReadOnlyList<CreatureActionDto>? BonusActions,
    IReadOnlyList<CreatureActionDto>? Reactions,
    IReadOnlyList<CreatureActionDto>? LegendaryActions,
    string? LegendaryActionsIntro,
    string? Notes,
    string? ExpectedRevision);

public sealed record SaveCreatureNotesRequest(string? Notes, string? ExpectedRevision);

public sealed record AddCreaturesToEncounterRequest(Guid CreatureId, int Count);

public sealed record CreatureImageResult(byte[] Content, string MediaType, Guid Revision);
