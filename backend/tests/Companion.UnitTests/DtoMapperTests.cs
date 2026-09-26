using Companion.Application.Contracts;
using Companion.Domain.Models;

namespace Companion.UnitTests;

public sealed class DtoMapperTests
{
    [Fact]
    public void ToDto_includes_every_combatants_hit_points_for_any_observer()
    {
        var encounter = new Encounter(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var ownerParticipantId = Guid.NewGuid();
        var owned = encounter.AddCombatant(Guid.NewGuid(), "Dono", 0, ownerParticipantId, currentHitPoints: 10, maxHitPoints: 20);
        var creature = encounter.AddCombatant(Guid.NewGuid(), "Criatura", 0, currentHitPoints: 30, maxHitPoints: 40);

        var observerId = Guid.NewGuid(); // a third participant, neither the owner nor the master
        var dto = DtoMapper.ToDto(encounter, observerId, includeAllHitPoints: false);

        var ownedDto = dto.Combatants.Single(x => x.Id == owned.Id);
        var creatureDto = dto.Combatants.Single(x => x.Id == creature.Id);
        Assert.Equal(10, ownedDto.CurrentHitPoints);
        Assert.Equal(20, ownedDto.MaxHitPoints);
        Assert.Equal(30, creatureDto.CurrentHitPoints);
        Assert.Equal(40, creatureDto.MaxHitPoints);
    }
}
