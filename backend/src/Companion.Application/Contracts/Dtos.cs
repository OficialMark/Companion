using System.Text.Json.Serialization;
using Companion.Domain.Models;

namespace Companion.Application.Contracts;

public sealed record CreateCampaignRequest(string CampaignName, string DisplayName);
public sealed record JoinCampaignRequest(string Code, string DisplayName);
public sealed record RollDiceRequest(string Expression, bool Publish = true, string? Label = null, DiceDamageBreakdownRequest? DamageBreakdown = null);
public sealed record DiceDamageComponentRequest(string Label, string DamageType, int TermCount);
public sealed record DiceDamageBreakdownRequest(string? BaseType, int BaseTermCount, IReadOnlyList<DiceDamageComponentRequest> Components);
public sealed record AddCombatantRequest(string Name, int InitiativeModifier, int? CurrentHitPoints = null, int? MaxHitPoints = null);
public sealed record UpdateCombatantRequest(string Name, int InitiativeModifier, int? CurrentHitPoints = null, int? MaxHitPoints = null);
public sealed record RollInitiativeRequest(Guid? CombatantId);
public sealed record UpdateInitiativeModifierRequest(int? InitiativeModifier);
public sealed record UpdateCombatProfileRequest(int? InitiativeModifier, int? CurrentHitPoints, int? MaxHitPoints = null);
public sealed record UpdateHitPointsRequest(int CurrentHitPoints);
public sealed record ResolveAttackRequest(Guid DiceRollId, IReadOnlyList<Guid> TargetCombatantIds);
public sealed record ConfigureBattlemapRequest(
    byte[] ImageContent,
    string ImageMediaType,
    string ImageFileName,
    int Columns,
    int Rows,
    bool IsGridVisible,
    string? ExpectedRevision,
    // Spec 008 (US2): fog drawn on the upload preview, applied in the same save as the image
    // so no participant ever receives the new map uncovered. HasFogMask distinguishes
    // "field absent" (legacy clients: keep current fog rules) from "empty" (fully revealed).
    bool HasFogMask = false,
    string? FogMask = null);
public sealed record MoveBattlemapTokenRequest(int? Column, int? Row, string? ExpectedPositionVersion);
public sealed record SetAttackAreaRequest(string Shape, int Radius, int? Width, int? DirectionDegrees);
public sealed record SetFogRequest(string? Mask);
public sealed record SetHitPointsVisibilityRequest(bool Hidden);
public sealed record SetTokenAppearanceRequest(int FootprintSize, IReadOnlyList<string> StatusConditions);
public sealed record CreateMarkerRequest(string Label);

public sealed record SessionResponse(
    Guid CampaignId,
    string PlayerCode,
    string? MasterCode,
    Guid ParticipantId,
    string DisplayName,
    ParticipantRole Role,
    string ParticipantToken);

public sealed record CampaignDto(Guid Id, string PlayerCode, string Name);
public sealed record ParticipantDto(
    Guid Id,
    string DisplayName,
    ParticipantRole Role,
    bool IsOnline,
    int? InitiativeModifier,
    int? CurrentHitPoints = null,
    int? MaxHitPoints = null,
    Guid? AvatarRevision = null);
public sealed record ParticipantAvatarDto(byte[] Content, string MediaType, Guid Revision);
public sealed record DiceRollDto(
    Guid Id,
    Guid ParticipantId,
    string DisplayName,
    string Expression,
    IReadOnlyList<int> Values,
    int Modifier,
    int Total,
    IReadOnlyList<DiceRollResultDto> Results,
    DateTimeOffset CreatedAt,
    string? Label = null);
public sealed record DiceTermResultDto(string Expression, IReadOnlyList<int> Values, int Subtotal);
/// <summary>
/// Um resultado (uma repetição). <see cref="Terms"/>, <see cref="Modifier"/> e <see cref="Total"/> são os da execução
/// escolhida, para clientes antigos. Os campos novos (spec 011) ficam fora do JSON quando nulos, então uma expressão
/// legada serializa exatamente como antes.
/// </summary>
public sealed record DiceRollResultDto(
    int Sequence,
    IReadOnlyList<DiceTermResultDto> Terms,
    int Modifier,
    int Total,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<DiceModifierDto>? Modifiers = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Selection = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<DiceExecutionDto>? Executions = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? SelectedExecution = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DiceDamageBreakdownDto? DamageBreakdown = null);
public sealed record DiceDamageBreakdownDto(string? BaseType, int? BaseSubtotal, IReadOnlyList<DiceDamageComponentDto> Components);
public sealed record DiceDamageComponentDto(string Label, string DamageType, int Subtotal);
/// <summary>Constante da expressão, com rótulo quando houver: <c>(FOR) 3</c> vira <c>{ Label: "FOR", Value: 3 }</c>.</summary>
public sealed record DiceModifierDto(string? Label, int Value);
/// <summary>Uma das duas execuções de <c>2#(…) maior|menor</c>.</summary>
public sealed record DiceExecutionDto(IReadOnlyList<DiceTermResultDto> Terms, int Modifier, int Total);
public sealed record CombatantDto(
    Guid Id,
    Guid? ParticipantId,
    string Name,
    int InitiativeModifier,
    int? Initiative,
    int CreationOrder,
    bool IsCurrent,
    int? CurrentHitPoints = null,
    bool IsDefeated = false,
    int? MaxHitPoints = null,
    bool IsHitPointsHidden = false,
    Guid? CreatureId = null);
public sealed record EncounterDto(
    Guid Id,
    EncounterStatus Status,
    int Round,
    int? CurrentTurnIndex,
    IReadOnlyList<CombatantDto> Combatants);
public sealed record AttackTargetDto(
    Guid CombatantId,
    string Name,
    int DamageApplied,
    int? HitPointsBefore,
    int? HitPointsAfter);
public sealed record CombatAttackDto(
    Guid Id,
    Guid DiceRollId,
    Guid AuthorParticipantId,
    int Damage,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AttackTargetDto> Targets);
public sealed record AttackResultDto(CombatAttackDto Attack, EncounterDto Encounter);
public sealed record EncounterInvalidationDto(Guid EncounterId);
public sealed record BattlemapInvalidationDto(Guid EncounterId, string Revision);
public sealed record AttackAreaDto(
    Guid Id,
    string Shape,
    int Radius,
    int? Width,
    int? DirectionDegrees,
    DateTimeOffset UpdatedAt);
public sealed record BattlemapTokenDto(
    Guid ParticipantId,
    string DisplayName,
    string Initials,
    int ColorKey,
    int? Column,
    int? Row,
    string? PositionVersion,
    bool IsCreature = false,
    AttackAreaDto? AttackArea = null,
    int FootprintSize = 1,
    IReadOnlyList<string>? StatusConditions = null,
    bool IsMarker = false,
    Guid? OwnerParticipantId = null,
    // Spec 008: creature tokens show the library image and remember the last roll setup.
    string? ImageUrl = null,
    Guid? CreatureId = null,
    RollPresetDto? RollPreset = null);
public sealed record RollPresetDto(int Count, int Die, string? Ability, int Bonus);
public sealed record SetRollPresetRequest(int Count, int Die, string? Ability, int Bonus);
public sealed record BattlemapSnapshotDto(
    Guid Id,
    Guid EncounterId,
    int Columns,
    int Rows,
    bool IsGridVisible,
    string Revision,
    Guid ImageRevision,
    string ImageUrl,
    string ImageMediaType,
    string ImageFileName,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<BattlemapTokenDto> Tokens,
    string? FogMask = null);
public sealed record ReplaceBattlemapResultDto(
    BattlemapSnapshotDto Battlemap,
    IReadOnlyList<Guid> ReturnedToReserve);
public sealed record BattlemapImageDto(
    byte[] Content,
    string MediaType,
    string FileName,
    Guid ImageRevision);
public sealed record CampaignSnapshot(
    CampaignDto Campaign,
    ParticipantDto CurrentParticipant,
    IReadOnlyList<ParticipantDto> Participants,
    IReadOnlyList<DiceRollDto> DiceRolls,
    EncounterDto? ActiveEncounter);

public sealed record CampaignEvent(
    Guid EventId,
    Guid CampaignId,
    string Type,
    DateTimeOffset OccurredAt,
    object Payload);

public sealed record AdminCampaignDto(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    int ParticipantCount,
    int OnlineCount,
    string PlayerCode,
    string MasterCode);
