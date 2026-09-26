using Companion.Domain.Models;

namespace Companion.Application.Contracts;

public sealed record CreateCampaignRequest(string CampaignName, string DisplayName);
public sealed record JoinCampaignRequest(string Code, string DisplayName);
public sealed record RollDiceRequest(string Expression, bool Publish = true);
public sealed record AddCombatantRequest(string Name, int InitiativeModifier, int? CurrentHitPoints = null);
public sealed record UpdateCombatantRequest(string Name, int InitiativeModifier, int? CurrentHitPoints = null);
public sealed record RollInitiativeRequest(Guid? CombatantId);
public sealed record UpdateInitiativeModifierRequest(int? InitiativeModifier);
public sealed record UpdateCombatProfileRequest(int? InitiativeModifier, int? CurrentHitPoints);
public sealed record UpdateHitPointsRequest(int CurrentHitPoints);
public sealed record ResolveAttackRequest(Guid DiceRollId, IReadOnlyList<Guid> TargetCombatantIds);
public sealed record ConfigureBattlemapRequest(
    byte[] ImageContent,
    string ImageMediaType,
    string ImageFileName,
    int Columns,
    int Rows,
    bool IsGridVisible,
    string? ExpectedRevision);
public sealed record MoveBattlemapTokenRequest(int? Column, int? Row, string? ExpectedPositionVersion);
public sealed record SetAttackAreaRequest(string Shape, int Radius, int? Width, int? DirectionDegrees);

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
    int? CurrentHitPoints = null);
public sealed record DiceRollDto(
    Guid Id,
    Guid ParticipantId,
    string DisplayName,
    string Expression,
    IReadOnlyList<int> Values,
    int Modifier,
    int Total,
    IReadOnlyList<DiceRollResultDto> Results,
    DateTimeOffset CreatedAt);
public sealed record DiceTermResultDto(string Expression, IReadOnlyList<int> Values, int Subtotal);
public sealed record DiceRollResultDto(int Sequence, IReadOnlyList<DiceTermResultDto> Terms, int Modifier, int Total);
public sealed record CombatantDto(
    Guid Id,
    Guid? ParticipantId,
    string Name,
    int InitiativeModifier,
    int? Initiative,
    int CreationOrder,
    bool IsCurrent,
    int? CurrentHitPoints = null,
    bool IsDefeated = false);
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
    AttackAreaDto? AttackArea = null);
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
    IReadOnlyList<BattlemapTokenDto> Tokens);
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