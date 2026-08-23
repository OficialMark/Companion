using Companion.Domain.Models;

namespace Companion.Application.Contracts;

public sealed record CreateCampaignRequest(string CampaignName, string DisplayName);
public sealed record JoinCampaignRequest(string Code, string DisplayName);
public sealed record RollDiceRequest(string Expression, bool Publish = true);
public sealed record AddCombatantRequest(string Name, int InitiativeModifier);
public sealed record RollInitiativeRequest(Guid? CombatantId);

public sealed record SessionResponse(
    Guid CampaignId,
    string CampaignCode,
    Guid ParticipantId,
    string DisplayName,
    ParticipantRole Role,
    string ParticipantToken);

public sealed record CampaignDto(Guid Id, string Code, string Name);
public sealed record ParticipantDto(Guid Id, string DisplayName, ParticipantRole Role, bool IsOnline);
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
    string Name,
    int InitiativeModifier,
    int? Initiative,
    int CreationOrder,
    bool IsCurrent);
public sealed record EncounterDto(
    Guid Id,
    EncounterStatus Status,
    int Round,
    int? CurrentTurnIndex,
    IReadOnlyList<CombatantDto> Combatants);
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
