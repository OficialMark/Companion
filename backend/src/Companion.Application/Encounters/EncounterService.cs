using Companion.Application.Campaigns;
using Companion.Application.Contracts;
using Companion.Domain.Models;

namespace Companion.Application.Encounters;

public sealed class EncounterService(
    ICompanionDbContext db,
    ParticipantAuthorization authorization,
    IDiceRandomizer randomizer,
    IPresenceTracker presence,
    ICampaignNotifier notifier,
    TimeProvider timeProvider)
{
    public async Task<EncounterDto> CreateAsync(Guid campaignId, string token, CancellationToken cancellationToken)
    {
        var master = authorization.Require(campaignId, token, masterOnly: true);
        if (db.Encounters.Any(x => x.CampaignId == campaignId && x.Status != EncounterStatus.Ended))
            throw new AppException(AppErrorKind.Conflict, "Já existe um combate ativo ou em preparação.");
        var encounter = new Encounter(Guid.NewGuid(), campaignId, timeProvider.GetUtcNow());
        db.Add(encounter);
        await db.SaveChangesAsync(cancellationToken);
        await notifier.EncounterInvalidatedAsync(campaignId, encounter.Id, cancellationToken);
        return DtoMapper.ToDto(encounter, master);
    }

    public async Task<EncounterDto> AddCombatantAsync(
        Guid campaignId, Guid encounterId, string token, AddCombatantRequest request, CancellationToken cancellationToken)
    {
        var master = authorization.Require(campaignId, token, masterOnly: true);
        var encounter = Load(campaignId, encounterId);
        try
        {
            var combatant = encounter.AddCombatant(Guid.NewGuid(), request.Name, request.InitiativeModifier,
                currentHitPoints: request.CurrentHitPoints);
            db.Add(combatant);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw new AppException(AppErrorKind.Validation, exception.Message);
        }
        await db.SaveChangesAsync(cancellationToken);
        await notifier.EncounterInvalidatedAsync(campaignId, encounter.Id, cancellationToken);
        return DtoMapper.ToDto(encounter, master);
    }

    public Task<EncounterDto> RollInitiativeAsync(
        Guid campaignId, Guid encounterId, string token, RollInitiativeRequest? request, CancellationToken cancellationToken) =>
        MutateMasterAsync(campaignId, encounterId, token, encounter =>
        {
            if (request?.CombatantId is null && encounter.Status == EncounterStatus.Preparing)
            {
                var linked = encounter.Combatants.Where(x => x.ParticipantId.HasValue).Select(x => x.ParticipantId!.Value).ToHashSet();
                var onlinePlayers = db.Participants.Where(x => x.CampaignId == campaignId && x.Role == ParticipantRole.Player)
                    .AsEnumerable().Where(x => presence.IsOnline(x.Id) && !linked.Contains(x.Id)).OrderBy(x => x.CreatedAt).ToArray();
                foreach (var participant in onlinePlayers)
                {
                    var combatant = encounter.AddCombatant(Guid.NewGuid(), participant.DisplayName,
                        participant.InitiativeModifier ?? 0, participant.Id, participant.CurrentHitPoints);
                    db.Add(combatant);
                }
            }
            encounter.RollInitiative(() => randomizer.Roll(20), request?.CombatantId);
        }, cancellationToken);

    public Task<EncounterDto> UpdateCombatantAsync(
        Guid campaignId, Guid encounterId, Guid combatantId, string token,
        UpdateCombatantRequest request, CancellationToken cancellationToken) =>
        MutateMasterAsync(campaignId, encounterId, token,
            encounter => encounter.UpdateCombatant(combatantId, request.Name, request.InitiativeModifier, request.CurrentHitPoints), cancellationToken);

    public async Task<EncounterDto> UpdateHitPointsAsync(
        Guid campaignId, Guid encounterId, Guid combatantId, string token,
        UpdateHitPointsRequest request, CancellationToken cancellationToken)
    {
        var participant = authorization.Require(campaignId, token);
        var encounter = Load(campaignId, encounterId);
        if (encounter.Status != EncounterStatus.Active)
            throw new AppException(AppErrorKind.Validation, "A vida direta só pode ser alterada durante um combate ativo.");
        var combatant = encounter.Combatants.FirstOrDefault(x => x.Id == combatantId)
            ?? throw new AppException(AppErrorKind.NotFound, "Combatente não encontrado.");
        if (participant.Role != ParticipantRole.Master && combatant.ParticipantId != participant.Id)
            throw new AppException(AppErrorKind.Forbidden, "Você só pode editar diretamente a vida do próprio personagem.");
        try { encounter.UpdateHitPoints(combatantId, request.CurrentHitPoints); }
        catch (ArgumentOutOfRangeException exception) { throw new AppException(AppErrorKind.Validation, exception.Message); }
        await db.SaveChangesAsync(cancellationToken);
        await notifier.EncounterInvalidatedAsync(campaignId, encounter.Id, cancellationToken);
        return DtoMapper.ToDto(encounter, participant);
    }

    public async Task<EncounterDto> RemoveCombatantAsync(
        Guid campaignId, Guid encounterId, Guid combatantId, string token, CancellationToken cancellationToken)
    {
        var master = authorization.Require(campaignId, token, masterOnly: true);
        var encounter = Load(campaignId, encounterId);
        var positions = db.BattlemapTokenPositions.Where(x => x.CombatantId == combatantId).ToArray();
        foreach (var position in positions) db.Remove(position);
        var areas = db.AttackAreas.Where(x => x.CombatantId == combatantId).ToArray();
        foreach (var area in areas) db.Remove(area);
        try { encounter.RemoveCombatant(combatantId); }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        { throw new AppException(AppErrorKind.Validation, exception.Message); }
        await db.SaveChangesAsync(cancellationToken);
        await notifier.EncounterInvalidatedAsync(campaignId, encounter.Id, cancellationToken);
        return DtoMapper.ToDto(encounter, master);
    }
    public Task<EncounterDto> StartAsync(Guid campaignId, Guid encounterId, string token, CancellationToken cancellationToken) =>
        MutateMasterAsync(campaignId, encounterId, token, encounter => encounter.Start(), cancellationToken);
    public Task<EncounterDto> AdvanceAsync(Guid campaignId, Guid encounterId, string token, CancellationToken cancellationToken) =>
        MutateMasterAsync(campaignId, encounterId, token, encounter => encounter.AdvanceTurn(), cancellationToken);
    public Task<EncounterDto> EndAsync(Guid campaignId, Guid encounterId, string token, CancellationToken cancellationToken) =>
        MutateMasterAsync(campaignId, encounterId, token, encounter => encounter.End(timeProvider.GetUtcNow()), cancellationToken);

    private async Task<EncounterDto> MutateMasterAsync(
        Guid campaignId, Guid encounterId, string token, Action<Encounter> action, CancellationToken cancellationToken)
    {
        var master = authorization.Require(campaignId, token, masterOnly: true);
        var encounter = Load(campaignId, encounterId);
        try { action(encounter); }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        { throw new AppException(AppErrorKind.Validation, exception.Message); }
        await db.SaveChangesAsync(cancellationToken);
        await notifier.EncounterInvalidatedAsync(campaignId, encounter.Id, cancellationToken);
        return DtoMapper.ToDto(encounter, master);
    }

    internal Encounter Load(Guid campaignId, Guid encounterId)
    {
        var encounter = db.Encounters.FirstOrDefault(x => x.Id == encounterId && x.CampaignId == campaignId)
            ?? throw new AppException(AppErrorKind.NotFound, "Combate não encontrado.");
        _ = db.Combatants.Where(x => x.EncounterId == encounterId).ToArray();
        return encounter;
    }
}