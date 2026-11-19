using Companion.Application.Contracts;
using Companion.Domain.Characters;
using Companion.Domain.Models;

namespace Companion.Application.Campaigns;

/// <summary>Resultado de uma rodada do backfill, para o startup logar.</summary>
/// <param name="Renamed">Participantes cujo DisplayName mudou com a normalização.</param>
/// <param name="KeysAssigned">Participantes que passaram a ser a conta do nome (NameKey preenchida).</param>
/// <param name="Invalid">Participantes cujo nome não normaliza: ficam como estão, sem NameKey.</param>
public sealed record ParticipantNameBackfillResult(int Renamed, int KeysAssigned, IReadOnlyList<Guid> Invalid)
{
    public bool Changed => Renamed > 0 || KeysAssigned > 0;
}

/// <summary>
/// Normaliza os nomes dos participantes de antes da spec 011 e decide qual deles é a conta de cada
/// nome (FR-044, research R5). Roda no startup depois do MigrateAsync. Idempotente: só olha quem
/// tem NameKey nula, e um grupo (campanha, papel, chave) que já tem dona não ganha outra.
/// No grupo sem dona, só o participante mais recente (CreatedAt; empate pelo Id) recebe a NameKey;
/// os demais ficam com NameKey nula e seguem acessíveis pelas sessões que já têm.
/// </summary>
public sealed class ParticipantNameBackfill(ICompanionDbContext db)
{
    public async Task<ParticipantNameBackfillResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var pending = db.Participants.Where(x => x.NameKey == null).ToList();
        if (pending.Count == 0) return new ParticipantNameBackfillResult(0, 0, []);

        // Chaves que já têm dona nas campanhas afetadas (contas criadas pelo join ou rodadas anteriores).
        var claimed = db.Participants
            .Where(x => x.NameKey != null && db.Participants.Any(p => p.NameKey == null && p.CampaignId == x.CampaignId))
            .Select(x => new { x.CampaignId, x.Role, x.NameKey })
            .AsEnumerable()
            .Select(x => (x.CampaignId, x.Role, x.NameKey!))
            .ToHashSet();

        var renamed = 0;
        var keysAssigned = 0;
        var invalid = new List<Guid>();
        var valid = new List<(Participant Participant, string Name, string Key)>(pending.Count);
        foreach (var participant in pending)
        {
            try
            {
                var name = PersonName.Normalize(participant.DisplayName);
                valid.Add((participant, name, PersonName.Key(name)));
            }
            catch (ArgumentException)
            {
                invalid.Add(participant.Id);
            }
        }

        foreach (var group in valid.GroupBy(x => (x.Participant.CampaignId, x.Participant.Role, x.Key)))
        {
            var owner = claimed.Contains(group.Key)
                ? null
                : group.OrderByDescending(x => x.Participant.CreatedAt).ThenByDescending(x => x.Participant.Id).First().Participant;
            foreach (var (participant, name, _) in group)
            {
                var ownsName = ReferenceEquals(participant, owner);
                if (participant.DisplayName != name) renamed++;
                if (ownsName) keysAssigned++;
                if (participant.DisplayName != name || ownsName) participant.Rename(name, ownsName);
            }
        }

        if (renamed > 0 || keysAssigned > 0) await db.SaveChangesAsync(cancellationToken);
        return new ParticipantNameBackfillResult(renamed, keysAssigned, invalid);
    }
}
