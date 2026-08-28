using Companion.Application.Campaigns;
using Companion.Application.Contracts;
using Companion.Domain.Models;

namespace Companion.Application.Encounters;

public sealed class AttackService(
    ICompanionDbContext db,
    ParticipantAuthorization authorization,
    ICampaignNotifier notifier,
    TimeProvider timeProvider)
{
    public async Task<(AttackResultDto Result, bool Created)> ResolveAsync(
        Guid campaignId, Guid encounterId, string token, Guid clientRequestId,
        ResolveAttackRequest request, CancellationToken cancellationToken)
    {
        var author = authorization.Require(campaignId, token);
        var encounter = Load(campaignId, encounterId);
        if (encounter.Status != EncounterStatus.Active)
            throw new AppException(AppErrorKind.Validation, "O combate não está ativo.");
        if (author.Role != ParticipantRole.Master && !encounter.Combatants.Any(x => x.ParticipantId == author.Id))
            throw new AppException(AppErrorKind.Forbidden, "Somente personagens no combate e mestres podem atacar.");

        var existing = db.CombatAttacks.FirstOrDefault(x => x.AuthorParticipantId == author.Id && x.ClientRequestId == clientRequestId);
        if (existing is not null)
            return (BuildResult(existing, encounter, author), false);
        if (request.TargetCombatantIds is null || request.TargetCombatantIds.Count is < 1 or > 20)
            throw new AppException(AppErrorKind.Validation, "Selecione entre 1 e 20 alvos.");
        if (request.TargetCombatantIds.Distinct().Count() != request.TargetCombatantIds.Count)
            throw new AppException(AppErrorKind.Validation, "Cada alvo deve aparecer somente uma vez.");

        var roll = db.DiceRolls.FirstOrDefault(x => x.Id == request.DiceRollId && x.CampaignId == campaignId)
            ?? throw new AppException(AppErrorKind.NotFound, "A rolagem de ataque não foi encontrada.");
        if (roll.ParticipantId != author.Id)
            throw new AppException(AppErrorKind.Forbidden, "O ataque deve usar uma rolagem feita pelo próprio autor.");
        if (roll.RevealedAt is null)
            throw new AppException(AppErrorKind.Validation, "A rolagem precisa ser concluída antes da escolha dos alvos.");
        if (CampaignService.DeserializeResults(roll).Count != 1)
            throw new AppException(AppErrorKind.Validation, "Ataques aceitam somente uma rolagem com resultado único.");
        if (db.CombatAttacks.Any(x => x.DiceRollId == roll.Id))
            throw new AppException(AppErrorKind.Conflict, "Esta rolagem já foi usada em outro ataque.");

        IReadOnlyList<DamageApplication> applications;
        try { applications = encounter.ApplyDamage(request.TargetCombatantIds, Math.Max(0, roll.Total)); }
        catch (InvalidOperationException exception) { throw new AppException(AppErrorKind.Validation, exception.Message); }

        var attack = new CombatAttack(Guid.NewGuid(), encounter.Id, author.Id, roll.Id, clientRequestId,
            Math.Max(0, roll.Total), timeProvider.GetUtcNow());
        db.Add(attack);
        foreach (var application in applications)
            db.Add(new CombatAttackTarget(attack.Id, application.CombatantId, application.DamageApplied,
                application.HitPointsBefore, application.HitPointsAfter));
        await db.SaveChangesAsync(cancellationToken);
        await notifier.NotifyAsync(campaignId, "DamageApplied", new
        {
            encounterId = encounter.Id,
            targets = applications.Select(x => new { combatantId = x.CombatantId, damageApplied = x.DamageApplied }).ToArray()
        }, cancellationToken);
        await notifier.EncounterInvalidatedAsync(campaignId, encounter.Id, cancellationToken);
        return (BuildResult(attack, encounter, author, applications), true);
    }

    private AttackResultDto BuildResult(
        CombatAttack attack, Encounter encounter, Participant observer,
        IReadOnlyList<DamageApplication>? newApplications = null)
    {
        var combatants = encounter.Combatants.ToDictionary(x => x.Id);
        var targetDtos = newApplications is not null
            ? newApplications.Select(x => TargetDto(x.CombatantId, x.DamageApplied, x.HitPointsBefore, x.HitPointsAfter)).ToArray()
            : db.CombatAttackTargets.Where(x => x.AttackId == attack.Id).AsEnumerable()
                .Select(x => TargetDto(x.CombatantId, x.DamageApplied, x.HitPointsBefore, x.HitPointsAfter)).ToArray();
        return new AttackResultDto(new CombatAttackDto(attack.Id, attack.DiceRollId, attack.AuthorParticipantId,
            attack.Damage, attack.CreatedAt, targetDtos), DtoMapper.ToDto(encounter, observer));

        AttackTargetDto TargetDto(Guid combatantId, int damageApplied, int before, int after)
        {
            var combatant = combatants.GetValueOrDefault(combatantId)
                ?? throw new AppException(AppErrorKind.Conflict, "O alvo do ataque não está mais no combate.");
            var canSee = observer.Role == ParticipantRole.Master || combatant.ParticipantId == observer.Id;
            return new AttackTargetDto(combatant.Id, combatant.Name, damageApplied,
                canSee ? before : null, canSee ? after : null);
        }
    }

    private Encounter Load(Guid campaignId, Guid encounterId)
    {
        var encounter = db.Encounters.FirstOrDefault(x => x.Id == encounterId && x.CampaignId == campaignId)
            ?? throw new AppException(AppErrorKind.NotFound, "Combate não encontrado.");
        _ = db.Combatants.Where(x => x.EncounterId == encounterId).ToArray();
        return encounter;
    }
}