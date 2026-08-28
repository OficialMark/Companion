namespace Companion.Domain.Models;

public sealed class CombatAttackTarget
{
    private CombatAttackTarget() { }

    public CombatAttackTarget(Guid attackId, Guid combatantId, int damageApplied, int hitPointsBefore, int hitPointsAfter)
    {
        if (damageApplied < 0 || hitPointsBefore < 0 || hitPointsAfter < 0 || hitPointsAfter > hitPointsBefore)
            throw new ArgumentOutOfRangeException(nameof(damageApplied));
        AttackId = attackId;
        CombatantId = combatantId;
        DamageApplied = damageApplied;
        HitPointsBefore = hitPointsBefore;
        HitPointsAfter = hitPointsAfter;
    }

    public Guid AttackId { get; private set; }
    public Guid CombatantId { get; private set; }
    public int DamageApplied { get; private set; }
    public int HitPointsBefore { get; private set; }
    public int HitPointsAfter { get; private set; }
}