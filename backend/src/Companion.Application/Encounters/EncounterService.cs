using Companion.Application.Creatures;
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
                currentHitPoints: request.CurrentHitPoints, maxHitPoints: request.MaxHitPoints);
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
                        participant.InitiativeModifier ?? 0, participant.Id, participant.CurrentHitPoints, participant.MaxHitPoints);
                    db.Add(combatant);
                }
            }
            encounter.RollInitiative(() => randomizer.Roll(20), request?.CombatantId);
        }, cancellationToken);

    public Task<EncounterDto> UpdateCombatantAsync(
        Guid campaignId, Guid encounterId, Guid combatantId, string token,
        UpdateCombatantRequest request, CancellationToken cancellationToken) =>
        MutateMasterAsync(campaignId, encounterId, token,
            encounter => encounter.UpdateCombatant(combatantId, request.Name, request.InitiativeModifier, request.CurrentHitPoints, request.MaxHitPoints), cancellationToken);

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

    public Task<EncounterDto> SetHitPointsVisibilityAsync(
        Guid campaignId, Guid encounterId, Guid combatantId, string token,
        SetHitPointsVisibilityRequest request, CancellationToken cancellationToken) =>
        MutateMasterAsync(campaignId, encounterId, token, encounter =>
        {
            var combatant = encounter.Combatants.FirstOrDefault(x => x.Id == combatantId)
                ?? throw new AppException(AppErrorKind.NotFound, "Combatente não encontrado.");
            combatant.SetHitPointsHidden(request.Hidden);
        }, cancellationToken);

    public Task<EncounterDto> SetAllCreatureHitPointsVisibilityAsync(
        Guid campaignId, Guid encounterId, string token,
        SetHitPointsVisibilityRequest request, CancellationToken cancellationToken) =>
        MutateMasterAsync(campaignId, encounterId, token, encounter =>
        {
            foreach (var creature in encounter.Combatants.Where(x => x.IsCreature))
                creature.SetHitPointsHidden(request.Hidden);
        }, cancellationToken);

    public const int MaxCreaturesPerAdd = 20;

    /// <summary>
    /// Adds 1-20 combatants from a library creature (spec 008, US4): numbered names, HP and
    /// initiative from the sheet, linked to the creature, token footprint from its size.
    /// While preparing they join like any combatant; during combat they are reinforcements that roll
    /// initiative on entry without moving the turn. Refused once the encounter ended.
    /// </summary>
    public async Task<EncounterDto> AddCreaturesAsync(
        Guid campaignId, Guid encounterId, string token, AddCreaturesToEncounterRequest request, CancellationToken cancellationToken)
    {
        var master = authorization.Require(campaignId, token, masterOnly: true);
        if (request.Count is < 1 or > MaxCreaturesPerAdd)
            throw new AppException(AppErrorKind.Validation, $"Adicione de 1 a {MaxCreaturesPerAdd} criaturas por vez.");
        var encounter = Load(campaignId, encounterId);
        if (encounter.Status == EncounterStatus.Ended)
            throw new AppException(AppErrorKind.Validation, "O combate já foi encerrado.");
        var reinforcement = encounter.Status == EncounterStatus.Active;
        var creature = db.Creatures.SingleOrDefault(x => x.Id == request.CreatureId && (x.CampaignId == null || x.CampaignId == campaignId))
            ?? throw new AppException(AppErrorKind.NotFound, "Criatura não encontrada.");
        if (creature.IsCompendium)
            creature = db.Creatures.SingleOrDefault(x => x.CampaignId == campaignId && x.BaseCreatureId == creature.Id) ?? creature;

        // If the master hid HP for every creature, newcomers must not leak theirs to the players.
        var existingCreatures = encounter.Combatants.Where(x => x.IsCreature).ToArray();
        var hideHitPoints = existingCreatures.Length > 0 && existingCreatures.All(x => x.IsHitPointsHidden);
        var names = CreatureNaming.Next(creature.Name, encounter.Combatants.Select(x => x.Name), request.Count, Combatant.MaxNameLength);
        var battlemap = db.Battlemaps.SingleOrDefault(x => x.EncounterId == encounter.Id);
        try
        {
            foreach (var name in names)
            {
                var modifier = Math.Clamp(creature.InitiativeModifier, -100, 100);
                // During combat the newcomer rolls initiative on entry and joins the order without moving the turn.
                var combatant = reinforcement
                    ? encounter.AddReinforcement(Guid.NewGuid(), name, modifier, creature.HitPoints, creature.HitPoints, () => randomizer.Roll(20))
                    : encounter.AddCombatant(Guid.NewGuid(), name, modifier, currentHitPoints: creature.HitPoints, maxHitPoints: creature.HitPoints);
                combatant.LinkCreature(creature.Id);
                if (hideHitPoints) combatant.SetHitPointsHidden(true);
                db.Add(combatant);
                if (battlemap is not null && creature.FootprintSize > 1)
                {
                    var appearance = BattlemapTokenAppearance.ForCombatant(Guid.NewGuid(), battlemap.Id, combatant.Id);
                    appearance.SetFootprintSize(creature.FootprintSize);
                    db.Add(appearance);
                }
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            throw new AppException(AppErrorKind.Validation, exception.Message);
        }
        // A reinforcement changes the turn order even when CurrentTurnIndex stays the same: bump the
        // rowversion so a concurrent "next turn" computed on the old list fails with 409 instead.
        if (reinforcement) db.MarkModified(encounter);
        await db.SaveChangesAsync(cancellationToken);
        await notifier.EncounterInvalidatedAsync(campaignId, encounter.Id, cancellationToken);
        if (battlemap is not null)
            await notifier.BattlemapInvalidatedAsync(campaignId, encounter.Id, DtoMapper.Revision(battlemap.Version), cancellationToken);
        return DtoMapper.ToDto(encounter, master);
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
        var appearances = db.TokenAppearances.Where(x => x.CombatantId == combatantId).ToArray();
        foreach (var appearance in appearances) db.Remove(appearance);
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