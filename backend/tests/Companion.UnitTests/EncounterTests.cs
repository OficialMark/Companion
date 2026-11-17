using Companion.Domain.Models;

namespace Companion.UnitTests;

public sealed class EncounterTests
{
    [Fact]
    public void Start_orders_by_initiative_then_creation_order()
    {
        var encounter = new Encounter(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var first = encounter.AddCombatant(Guid.NewGuid(), "Primeiro", 0, currentHitPoints: 10);
        var second = encounter.AddCombatant(Guid.NewGuid(), "Segundo", 0, currentHitPoints: 10);
        first.SetInitiative(12);
        second.SetInitiative(12);

        encounter.Start();
        var ordered = encounter.Combatants.OrderByDescending(x => x.Initiative).ThenBy(x => x.CreationOrder).ToArray();

        Assert.Equal(first.Id, ordered[0].Id);
        Assert.Equal(1, encounter.Round);
        Assert.Equal(0, encounter.CurrentTurnIndex);
    }

    [Fact]
    public void Adding_a_combatant_after_removing_a_non_last_one_does_not_reuse_creation_order()
    {
        var encounter = new Encounter(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var first = encounter.AddCombatant(Guid.NewGuid(), "Primeiro", 0, currentHitPoints: 10);
        var second = encounter.AddCombatant(Guid.NewGuid(), "Segundo", 0, currentHitPoints: 10);
        var third = encounter.AddCombatant(Guid.NewGuid(), "Terceiro", 0, currentHitPoints: 10);

        encounter.RemoveCombatant(second.Id);
        var fourth = encounter.AddCombatant(Guid.NewGuid(), "Quarto", 0, currentHitPoints: 10);

        var creationOrders = encounter.Combatants.Select(x => x.CreationOrder).ToArray();
        Assert.Equal(creationOrders.Length, creationOrders.Distinct().Count());
        Assert.True(fourth.CreationOrder > third.CreationOrder);
        Assert.Equal(0, first.CreationOrder);
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
        encounter.AddCombatant(Guid.NewGuid(), "Sem iniciativa", 0, currentHitPoints: 10);
        Assert.Throws<InvalidOperationException>(encounter.Start);
    }

    [Fact]
    public void Roll_again_replaces_existing_initiatives_while_preparing()
    {
        var encounter = new Encounter(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var first = encounter.AddCombatant(Guid.NewGuid(), "Primeiro", 1, currentHitPoints: 10);
        var second = encounter.AddCombatant(Guid.NewGuid(), "Segundo", -1, currentHitPoints: 10);
        encounter.RollInitiative(() => 10);

        encounter.RollInitiative(() => 18);

        Assert.Equal(19, first.Initiative);
        Assert.Equal(17, second.Initiative);
    }

    [Fact]
    public void Roll_all_again_during_combat_moves_turn_to_highest_initiative()
    {
        var encounter = new Encounter(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var first = encounter.AddCombatant(Guid.NewGuid(), "Primeiro", 0, currentHitPoints: 10);
        var second = encounter.AddCombatant(Guid.NewGuid(), "Segundo", 0, currentHitPoints: 10);
        first.SetInitiative(20);
        second.SetInitiative(10);
        encounter.Start();

        var rolls = new Queue<int>([1, 20]);
        encounter.RollInitiative(() => rolls.Dequeue());
        var reordered = encounter.Combatants.OrderByDescending(x => x.Initiative).ThenBy(x => x.CreationOrder).ToArray();

        Assert.Equal(0, encounter.CurrentTurnIndex);
        Assert.Equal(second.Id, reordered[encounter.CurrentTurnIndex!.Value].Id);
        Assert.Equal(1, first.Initiative);
        Assert.Equal(20, second.Initiative);
    }

    [Fact]
    public void Roll_one_again_during_combat_preserves_current_combatant()
    {
        var encounter = new Encounter(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var first = encounter.AddCombatant(Guid.NewGuid(), "Primeiro", 0, currentHitPoints: 10);
        var second = encounter.AddCombatant(Guid.NewGuid(), "Segundo", 0, currentHitPoints: 10);
        first.SetInitiative(20);
        second.SetInitiative(10);
        encounter.Start();

        encounter.RollInitiative(() => 1, first.Id);
        var reordered = encounter.Combatants.OrderByDescending(x => x.Initiative).ThenBy(x => x.CreationOrder).ToArray();

        Assert.Equal(first.Id, reordered[encounter.CurrentTurnIndex!.Value].Id);
    }

    [Fact]
    public void Participant_link_is_unique_and_master_edits_are_preserved()    {
        var encounter = new Encounter(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var participantId = Guid.NewGuid();
        var combatant = encounter.AddCombatant(Guid.NewGuid(), "Lia", 2, participantId, 18);

        Assert.Throws<InvalidOperationException>(() =>
            encounter.AddCombatant(Guid.NewGuid(), "Lia duplicada", 2, participantId, 18));
        encounter.UpdateCombatant(combatant.Id, "Lia editada", 5, 20);
        encounter.RollInitiative(() => 10);

        Assert.Equal("Lia editada", combatant.Name);
        Assert.Equal(5, combatant.InitiativeModifier);
        Assert.Equal(15, combatant.Initiative);
    }

    [Fact]
    public void Removing_current_combatant_advances_to_its_successor()
    {
        var encounter = ActiveEncounter(3);
        var ordered = encounter.Combatants.OrderByDescending(x => x.Initiative).ThenBy(x => x.CreationOrder).ToArray();
        encounter.RemoveCombatant(ordered[0].Id);
        var reordered = encounter.Combatants.OrderByDescending(x => x.Initiative).ThenBy(x => x.CreationOrder).ToArray();

        Assert.Equal(ordered[1].Id, reordered[encounter.CurrentTurnIndex!.Value].Id);
    }


    [Fact]
    public void Participant_validates_and_updates_own_hit_points()
    {
        var participant = new Participant(Guid.NewGuid(), Guid.NewGuid(), "Lia", ParticipantRole.Player, new byte[32], DateTimeOffset.UtcNow);
        participant.UpdateCombatProfile(3, 24);
        Assert.Equal(3, participant.InitiativeModifier);
        Assert.Equal(24, participant.CurrentHitPoints);
        Assert.Throws<ArgumentOutOfRangeException>(() => participant.UpdateCombatProfile(3, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => participant.UpdateCombatProfile(3, 100_000));
    }

    [Fact]
    public void Participant_validates_and_updates_max_hit_points()
    {
        var participant = new Participant(Guid.NewGuid(), Guid.NewGuid(), "Lia", ParticipantRole.Player, new byte[32], DateTimeOffset.UtcNow);
        participant.UpdateCombatProfile(3, 12, 18);
        Assert.Equal(12, participant.CurrentHitPoints);
        Assert.Equal(18, participant.MaxHitPoints);
        Assert.Throws<ArgumentOutOfRangeException>(() => participant.UpdateCombatProfile(3, 12, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => participant.UpdateCombatProfile(3, 12, 100_000));
    }

    [Fact]
    public void Combatant_validates_and_updates_max_hit_points()
    {
        var encounter = new Encounter(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var combatant = encounter.AddCombatant(Guid.NewGuid(), "Lobo", 0, currentHitPoints: 10, maxHitPoints: 15);
        Assert.Equal(15, combatant.MaxHitPoints);

        encounter.UpdateCombatant(combatant.Id, "Lobo", 0, currentHitPoints: 12, maxHitPoints: 20);
        Assert.Equal(20, combatant.MaxHitPoints);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            encounter.AddCombatant(Guid.NewGuid(), "Inválido", 0, currentHitPoints: 10, maxHitPoints: -1));
    }

    [Fact]
    public void Rolling_initiative_copies_participant_max_hit_points_to_the_new_combatant()
    {
        var encounter = new Encounter(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var participant = new Participant(Guid.NewGuid(), Guid.NewGuid(), "Lia", ParticipantRole.Player, new byte[32], DateTimeOffset.UtcNow);
        participant.UpdateCombatProfile(2, 14, 22);

        var combatant = encounter.AddCombatant(
            Guid.NewGuid(), participant.DisplayName, participant.InitiativeModifier ?? 0,
            participant.Id, participant.CurrentHitPoints, participant.MaxHitPoints);

        Assert.Equal(14, combatant.CurrentHitPoints);
        Assert.Equal(22, combatant.MaxHitPoints);
    }

    [Fact]
    public void Cannot_start_with_missing_hit_points()
    {
        var encounter = new Encounter(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        encounter.AddCombatant(Guid.NewGuid(), "Sem vida", 0).SetInitiative(10);
        var error = Assert.Throws<InvalidOperationException>(encounter.Start);
        Assert.Contains("vida", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Damage_uses_latest_hit_points_deduplicates_targets_and_never_goes_below_zero()
    {
        var encounter = ActiveEncounter(2);
        var targets = encounter.Combatants.ToArray();
        targets[0].UpdateHitPoints(5);
        var results = encounter.ApplyDamage([targets[0].Id, targets[1].Id], 7);
        Assert.Equal(0, targets[0].CurrentHitPoints);
        Assert.Equal(13, targets[1].CurrentHitPoints);
        Assert.Equal(5, results.Single(x => x.CombatantId == targets[0].Id).DamageApplied);
        Assert.Throws<InvalidOperationException>(() => encounter.ApplyDamage([targets[0].Id, targets[0].Id], 1));
    }

    [Fact]
    public void Active_combatant_hit_points_can_change_outside_turn()
    {
        var encounter = ActiveEncounter(2);
        var notCurrent = encounter.Combatants.Single(x => x.Id != encounter.Combatants.OrderByDescending(c => c.Initiative).First().Id);
        notCurrent.UpdateHitPoints(42);
        Assert.Equal(42, notCurrent.CurrentHitPoints);
    }
    private static Encounter ActiveEncounter(int count)
    {
        var encounter = new Encounter(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        for (var index = 0; index < count; index++)
        {
            encounter.AddCombatant(Guid.NewGuid(), $"Combatente {index}", index, currentHitPoints: 20).SetInitiative(10);
        }
        encounter.Start();
        return encounter;
    }
}
