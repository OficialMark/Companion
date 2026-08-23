using Companion.Domain.Models;

namespace Companion.Application.Contracts;

public static class DtoMapper
{
    public static EncounterDto ToDto(Encounter encounter)
    {
        var ordered = encounter.Combatants
            .OrderByDescending(x => x.Initiative ?? int.MinValue)
            .ThenBy(x => x.CreationOrder)
            .ToArray();
        var combatants = ordered.Select((x, index) => new CombatantDto(
            x.Id, x.Name, x.InitiativeModifier, x.Initiative, x.CreationOrder,
            encounter.Status == EncounterStatus.Active && encounter.CurrentTurnIndex == index)).ToArray();
        return new EncounterDto(encounter.Id, encounter.Status, encounter.Round, encounter.CurrentTurnIndex, combatants);
    }
}
