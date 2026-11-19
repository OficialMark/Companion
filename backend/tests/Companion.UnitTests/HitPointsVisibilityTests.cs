using Companion.Application.Contracts;
using Companion.Domain.Models;

namespace Companion.UnitTests;

public sealed class HitPointsVisibilityTests
{
    [Fact]
    public void Only_creatures_can_have_hidden_hit_points()
    {
        var encounter = new Encounter(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var creature = encounter.AddCombatant(Guid.NewGuid(), "Goblin", 0, currentHitPoints: 7);
        var player = encounter.AddCombatant(Guid.NewGuid(), "Ayla", 0, Guid.NewGuid(), currentHitPoints: 10);

        creature.SetHitPointsHidden(true);
        Assert.True(creature.IsHitPointsHidden);
        creature.SetHitPointsHidden(false);
        Assert.False(creature.IsHitPointsHidden);

        Assert.Throws<InvalidOperationException>(() => player.SetHitPointsHidden(true));
        player.SetHitPointsHidden(false); // showing is always allowed
        Assert.False(player.IsHitPointsHidden);
    }

    [Fact]
    public void Encounter_dto_hides_hidden_creature_hit_points_from_players_but_keeps_defeated()
    {
        var encounter = new Encounter(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var ownerId = Guid.NewGuid();
        var player = encounter.AddCombatant(Guid.NewGuid(), "Ayla", 0, ownerId, currentHitPoints: 12, maxHitPoints: 20);
        var goblin = encounter.AddCombatant(Guid.NewGuid(), "Goblin", 0, currentHitPoints: 0, maxHitPoints: 7);
        var orc = encounter.AddCombatant(Guid.NewGuid(), "Orc", 0, currentHitPoints: 15, maxHitPoints: 15);
        goblin.SetHitPointsHidden(true);

        var forPlayer = DtoMapper.ToDto(encounter, ownerId, includeAllHitPoints: false);
        var hiddenDto = forPlayer.Combatants.Single(x => x.Id == goblin.Id);
        Assert.Null(hiddenDto.CurrentHitPoints);
        Assert.Null(hiddenDto.MaxHitPoints);
        Assert.True(hiddenDto.IsDefeated);
        Assert.True(hiddenDto.IsHitPointsHidden);
        Assert.Equal(12, forPlayer.Combatants.Single(x => x.Id == player.Id).CurrentHitPoints);
        Assert.Equal(15, forPlayer.Combatants.Single(x => x.Id == orc.Id).CurrentHitPoints);

        var forMaster = DtoMapper.ToDto(encounter, Guid.NewGuid(), includeAllHitPoints: true);
        var masterDto = forMaster.Combatants.Single(x => x.Id == goblin.Id);
        Assert.Equal(0, masterDto.CurrentHitPoints);
        Assert.Equal(7, masterDto.MaxHitPoints);
        Assert.True(masterDto.IsHitPointsHidden);
    }

    [Fact]
    public void Participant_dto_always_carries_hit_points_and_avatar()
    {
        // Regression: the presence broadcast (ParticipantsChanged) used to build the DTO
        // without HP/avatar, wiping both on every client when anyone connected.
        var participant = new Participant(Guid.NewGuid(), Guid.NewGuid(), "Ayla", ParticipantRole.Player, [1], DateTimeOffset.UtcNow);
        participant.UpdateCombatProfile(2, 15, 20);
        var revision = Guid.NewGuid();
        participant.SetAvatarRevision(revision);

        var dto = DtoMapper.ToDto(participant, isOnline: true);

        Assert.Equal(15, dto.CurrentHitPoints);
        Assert.Equal(20, dto.MaxHitPoints);
        Assert.Equal(revision, dto.AvatarRevision);
        Assert.True(dto.IsOnline);
    }

    [Fact]
    public void Every_status_condition_fits_the_persisted_column()
    {
        var appearance = BattlemapTokenAppearance.ForParticipant(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var all = Enum.GetValues<StatusCondition>();
        Assert.Equal(18, all.Length);

        appearance.SetStatusConditions([StatusCondition.Bleeding, StatusCondition.Paralyzed, StatusCondition.Bleeding]);
        Assert.Equal([StatusCondition.Bleeding, StatusCondition.Paralyzed], appearance.StatusConditions);

        appearance.SetStatusConditions(all);
        Assert.True(string.Join(',', appearance.StatusConditions).Length <= 512);
    }
}
