using Companion.Domain.Models;

namespace Companion.Application.Contracts;

public static class DtoMapper
{
    public static EncounterDto ToDto(Encounter encounter) => ToDto(encounter, null, true);

    public static EncounterDto ToDto(Encounter encounter, Participant observer) =>
        ToDto(encounter, observer.Id, observer.Role == ParticipantRole.Master);

    public static EncounterDto ToDto(Encounter encounter, Guid? observerParticipantId, bool includeAllHitPoints)
    {
        var ordered = encounter.Combatants
            .OrderByDescending(x => x.Initiative ?? int.MinValue)
            .ThenBy(x => x.CreationOrder)
            .ToArray();
        var combatants = ordered.Select((x, index) => new CombatantDto(
            x.Id,
            x.ParticipantId,
            x.Name,
            x.InitiativeModifier,
            x.Initiative,
            x.CreationOrder,
            encounter.Status == EncounterStatus.Active && encounter.CurrentTurnIndex == index,
            includeAllHitPoints || x.ParticipantId == observerParticipantId ? x.CurrentHitPoints : null,
            x.CurrentHitPoints == 0)).ToArray();
        return new EncounterDto(encounter.Id, encounter.Status, encounter.Round, encounter.CurrentTurnIndex, combatants);
    }

    public static string Revision(byte[] version) =>
        version.Length == 0 ? string.Empty : Convert.ToBase64String(version);

    public static string Initials(string displayName)
    {
        var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Concat(parts.Take(3).Select(part => char.ToUpperInvariant(part[0])));
    }

    public static int ColorKey(Guid participantId)
    {
        var bytes = participantId.ToByteArray();
        return (int)(BitConverter.ToUInt32(bytes, 0) % 12);
    }
}
