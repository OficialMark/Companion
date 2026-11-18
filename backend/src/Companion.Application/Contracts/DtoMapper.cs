using Companion.Domain.Models;

namespace Companion.Application.Contracts;

public static class DtoMapper
{
    public static EncounterDto ToDto(Encounter encounter) => ToDto(encounter, null, true);

    public static EncounterDto ToDto(Encounter encounter, Participant observer) =>
        ToDto(encounter, observer.Id, observer.Role == ParticipantRole.Master);

    // Vida (atual e máxima) visível a todos (spec 006), exceto criaturas cujo HP o mestre
    // ocultou (spec 007, FR-014): para quem não é mestre, o servidor não envia os números,
    // só se a criatura está derrotada.
    public static EncounterDto ToDto(Encounter encounter, Guid? observerParticipantId, bool includeAllHitPoints)
    {
        var ordered = encounter.Combatants
            .OrderByDescending(x => x.Initiative ?? int.MinValue)
            .ThenBy(x => x.CreationOrder)
            .ToArray();
        var combatants = ordered.Select((x, index) =>
        {
            var hidden = x.IsHitPointsHidden && !includeAllHitPoints && x.ParticipantId != observerParticipantId;
            return new CombatantDto(
                x.Id,
                x.ParticipantId,
                x.Name,
                x.InitiativeModifier,
                x.Initiative,
                x.CreationOrder,
                encounter.Status == EncounterStatus.Active && encounter.CurrentTurnIndex == index,
                hidden ? null : x.CurrentHitPoints,
                x.CurrentHitPoints == 0,
                hidden ? null : x.MaxHitPoints,
                x.IsHitPointsHidden,
                x.CreatureId);
        }).ToArray();
        return new EncounterDto(encounter.Id, encounter.Status, encounter.Round, encounter.CurrentTurnIndex, combatants);
    }

    // Único ponto de montagem do ParticipantDto: o evento ParticipantsChanged (presença)
    // substitui a lista inteira no cliente, então omitir HP/avatar aqui apagava a vida e a
    // foto de todos sempre que alguém conectava ou desconectava.
    public static ParticipantDto ToDto(Participant participant, bool isOnline) =>
        new(participant.Id, participant.DisplayName, participant.Role, isOnline,
            participant.InitiativeModifier, participant.CurrentHitPoints, participant.MaxHitPoints,
            participant.AvatarRevision);

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
