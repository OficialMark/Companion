using Companion.Domain.Models;

namespace Companion.UnitTests;

public sealed class EncounterTests
{
    [Fact]
    public void Start_orders_by_initiative_then_creation_order()
    {
        var encounter = new Encounter(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var first = encounter.AddCombatant(Guid.NewGuid(), "Primeiro", 0);
        var second = encounter.AddCombatant(Guid.NewGuid(), "Segundo", 0);
        first.SetInitiative(12);
        second.SetInitiative(12);

        encounter.Start();
        var ordered = encounter.Combatants.OrderByDescending(x => x.Initiative).ThenBy(x => x.CreationOrder).ToArray();

        Assert.Equal(first.Id, ordered[0].Id);
        Assert.Equal(1, encounter.Round);
        Assert.Equal(0, encounter.CurrentTurnIndex);
    }

    [Fact]
    public void Advance_after_last_combatant_increments_round()
    {
        var encounter = ActiveEncounter(2);
        encounter.AdvanceTurn();
        encounter.AdvanceTurn();
        Assert.Equal(2, encounter.Round);
        Assert.Equal(0, encounter.CurrentTurnIndex);
    }

    [Fact]
    public void Cannot_start_with_missing_initiative()
    {
        var encounter = new Encounter(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        encounter.AddCombatant(Guid.NewGuid(), "Sem iniciativa", 0);
        Assert.Throws<InvalidOperationException>(encounter.Start);
    }

    [Fact]
    public void Roll_again_replaces_existing_initiatives_while_preparing()
    {
        var encounter = new Encounter(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var first = encounter.AddCombatant(Guid.NewGuid(), "Primeiro", 1);
        var second = encounter.AddCombatant(Guid.NewGuid(), "Segundo", -1);
        encounter.RollInitiative(() => 10);

        encounter.RollInitiative(() => 18);

        Assert.Equal(19, first.Initiative);
        Assert.Equal(17, second.Initiative);
    }

    [Fact]
    public void Roll_again_during_combat_preserves_current_combatant()
    {
        var encounter = new Encounter(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var first = encounter.AddCombatant(Guid.NewGuid(), "Primeiro", 0);
        var second = encounter.AddCombatant(Guid.NewGuid(), "Segundo", 0);
        first.SetInitiative(20);
        second.SetInitiative(10);
        encounter.Start();

        var rolls = new Queue<int>([1, 20]);
        encounter.RollInitiative(() => rolls.Dequeue());
        var reordered = encounter.Combatants.OrderByDescending(x => x.Initiative).ThenBy(x => x.CreationOrder).ToArray();

        Assert.Equal(first.Id, reordered[encounter.CurrentTurnIndex!.Value].Id);
        Assert.Equal(1, first.Initiative);
        Assert.Equal(20, second.Initiative);
    }

    private static Encounter ActiveEncounter(int count)
    {
        var encounter = new Encounter(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        for (var index = 0; index < count; index++)
        {
            encounter.AddCombatant(Guid.NewGuid(), $"Combatente {index}", index).SetInitiative(10);
        }
        encounter.Start();
        return encounter;
    }
}
