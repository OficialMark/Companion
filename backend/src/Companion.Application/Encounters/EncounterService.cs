using Companion.Application.Campaigns;
using Companion.Application.Contracts;
using Companion.Domain.Models;

namespace Companion.Application.Encounters;

public sealed class EncounterService(
    ICompanionDbContext db,
    ParticipantAuthorization authorization,
    IDiceRandomizer randomizer,
    ICampaignNotifier notifier,
    TimeProvider timeProvider)
{
    public async Task<EncounterDto> CreateAsync(Guid campaignId, string token, CancellationToken cancellationToken)
    {
        _ = authorization.Require(campaignId, token, masterOnly: true);
        if (db.Encounters.Any(x => x.CampaignId == campaignId && x.Status != EncounterStatus.Ended))
        {
            throw new AppException(AppErrorKind.Conflict, "Já existe um combate ativo ou em preparação.");
        }

        var encounter = new Encounter(Guid.NewGuid(), campaignId, timeProvider.GetUtcNow());
        db.Add(encounter);
        await db.SaveChangesAsync(cancellationToken);
        var dto = DtoMapper.ToDto(encounter);
        await notifier.NotifyAsync(campaignId, "EncounterStarted", dto, cancellationToken);
        return dto;
    }

    public async Task<EncounterDto> AddCombatantAsync(
        Guid campaignId, Guid encounterId, string token, AddCombatantRequest request, CancellationToken cancellationToken)
    {
        _ = authorization.Require(campaignId, token, masterOnly: true);
        var encounter = Load(campaignId, encounterId);
        Combatant combatant;
        try
        {
            combatant = encounter.AddCombatant(Guid.NewGuid(), request.Name, request.InitiativeModifier);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw new AppException(AppErrorKind.Validation, exception.Message);
        }

        db.Add(combatant);
        await db.SaveChangesAsync(cancellationToken);
        var dto = DtoMapper.ToDto(encounter);
        await notifier.NotifyAsync(campaignId, "CombatantAdded", dto, cancellationToken);
        return dto;
    }

    public Task<EncounterDto> RollInitiativeAsync(
        Guid campaignId, Guid encounterId, string token, RollInitiativeRequest? request, CancellationToken cancellationToken) =>
        MutateAsync(campaignId, encounterId, token, "InitiativeRolled",
            encounter => encounter.RollInitiative(() => randomizer.Roll(20), request?.CombatantId), cancellationToken);

    public Task<EncounterDto> StartAsync(
        Guid campaignId, Guid encounterId, string token, CancellationToken cancellationToken) =>
        MutateAsync(campaignId, encounterId, token, "EncounterStarted", encounter => encounter.Start(), cancellationToken);

    public Task<EncounterDto> AdvanceAsync(
        Guid campaignId, Guid encounterId, string token, CancellationToken cancellationToken) =>
        MutateAsync(campaignId, encounterId, token, "TurnAdvanced", encounter => encounter.AdvanceTurn(), cancellationToken);

    public Task<EncounterDto> EndAsync(
        Guid campaignId, Guid encounterId, string token, CancellationToken cancellationToken) =>
        MutateAsync(campaignId, encounterId, token, "EncounterEnded", encounter => encounter.End(timeProvider.GetUtcNow()), cancellationToken);

    private async Task<EncounterDto> MutateAsync(
        Guid campaignId,
        Guid encounterId,
        string token,
        string eventType,
        Action<Encounter> action,
        CancellationToken cancellationToken)
    {
        _ = authorization.Require(campaignId, token, masterOnly: true);
        var encounter = Load(campaignId, encounterId);
        try
        {
            action(encounter);
        }
        catch (InvalidOperationException exception)
        {
            throw new AppException(AppErrorKind.Validation, exception.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        var dto = DtoMapper.ToDto(encounter);
        await notifier.NotifyAsync(campaignId, eventType, dto, cancellationToken);
        return dto;
    }

    private Encounter Load(Guid campaignId, Guid encounterId)
    {
        var encounter = db.Encounters.FirstOrDefault(x => x.Id == encounterId && x.CampaignId == campaignId)
            ?? throw new AppException(AppErrorKind.NotFound, "Combate não encontrado.");
        _ = db.Combatants.Where(x => x.EncounterId == encounterId).ToArray();
        return encounter;
    }
}
