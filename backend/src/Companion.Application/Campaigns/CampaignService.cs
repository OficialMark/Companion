using System.Text.Json;
using Companion.Application.Contracts;
using Companion.Domain.Dice;
using Companion.Domain.Models;

namespace Companion.Application.Campaigns;

public sealed class CampaignService(
    ICompanionDbContext db,
    IParticipantTokenService tokens,
    IPresenceTracker presence,
    ICampaignNotifier notifier,
    TimeProvider timeProvider,
    ParticipantAuthorization authorization)
{
    public async Task<SessionResponse> CreateAsync(CreateCampaignRequest request, CancellationToken cancellationToken)
    {
        var campaignName = ValidateName(request.CampaignName, 80, "campanha");
        var displayName = ValidateName(request.DisplayName, 40, "participante");
        var code = CreateUniqueCode();
        var credential = tokens.Create();
        var now = timeProvider.GetUtcNow();
        var campaign = new Campaign(Guid.NewGuid(), code, campaignName, now);
        var master = new Participant(Guid.NewGuid(), campaign.Id, displayName, ParticipantRole.Master, credential.Hash, now);

        db.Add(campaign);
        db.Add(master);
        await db.SaveChangesAsync(cancellationToken);

        return new SessionResponse(campaign.Id, campaign.Code, master.Id, master.DisplayName, master.Role, credential.Token);
    }

    public async Task<SessionResponse> JoinAsync(JoinCampaignRequest request, CancellationToken cancellationToken)
    {
        var code = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
        var displayName = ValidateName(request.DisplayName, 40, "participante");
        var campaign = db.Campaigns.FirstOrDefault(x => x.Code == code)
            ?? throw new AppException(AppErrorKind.NotFound, "Campanha não encontrada.");

        if (db.Participants.Where(x => x.CampaignId == campaign.Id)
            .AsEnumerable().Any(x => string.Equals(x.DisplayName, displayName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AppException(AppErrorKind.Conflict, "Este nome já está em uso na campanha.");
        }

        var credential = tokens.Create();
        var participant = new Participant(
            Guid.NewGuid(), campaign.Id, displayName, ParticipantRole.Player,
            credential.Hash, timeProvider.GetUtcNow());
        db.Add(participant);
        await db.SaveChangesAsync(cancellationToken);
        await notifier.NotifyAsync(campaign.Id, "ParticipantJoined", ToDto(participant), cancellationToken);

        return new SessionResponse(campaign.Id, campaign.Code, participant.Id, participant.DisplayName, participant.Role, credential.Token);
    }

    public Task<CampaignSnapshot> GetSnapshotAsync(Guid campaignId, string token, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var current = authorization.Require(campaignId, token);
        var campaign = db.Campaigns.FirstOrDefault(x => x.Id == campaignId)
            ?? throw new AppException(AppErrorKind.NotFound, "Campanha não encontrada.");
        var participants = db.Participants.Where(x => x.CampaignId == campaignId)
            .OrderBy(x => x.CreatedAt).AsEnumerable().Select(ToDto).ToArray();
        var participantNames = participants.ToDictionary(x => x.Id, x => x.DisplayName);
        var rolls = db.DiceRolls.Where(x => x.CampaignId == campaignId)
            .OrderByDescending(x => x.CreatedAt).Take(50).AsEnumerable()
            .Select(x => ToDto(x, participantNames.GetValueOrDefault(x.ParticipantId, "Participante"))).ToArray();
        var encounter = db.Encounters.Where(x => x.CampaignId == campaignId && x.Status != EncounterStatus.Ended)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefault();

        EncounterDto? encounterDto = null;
        if (encounter is not null)
        {
            _ = db.Combatants.Where(x => x.EncounterId == encounter.Id).ToArray();
            encounterDto = DtoMapper.ToDto(encounter);
        }

        return Task.FromResult(new CampaignSnapshot(
            new CampaignDto(campaign.Id, campaign.Code, campaign.Name),
            ToDto(current), participants, rolls, encounterDto));
    }

    public IReadOnlyList<ParticipantDto> GetParticipants(Guid campaignId) =>
        db.Participants.Where(x => x.CampaignId == campaignId)
            .OrderBy(x => x.CreatedAt).AsEnumerable().Select(ToDto).ToArray();

    private string CreateUniqueCode()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = tokens.CreateCampaignCode();
            if (!db.Campaigns.Any(x => x.Code == code))
            {
                return code;
            }
        }

        throw new AppException(AppErrorKind.Conflict, "Não foi possível gerar um código de campanha. Tente novamente.");
    }

    private ParticipantDto ToDto(Participant participant) =>
        new(participant.Id, participant.DisplayName, participant.Role, presence.IsOnline(participant.Id));

    internal static DiceRollDto ToDto(DiceRoll roll, string displayName)
    {
        var results = DeserializeResults(roll);
        var first = results[0];
        return new DiceRollDto(roll.Id, roll.ParticipantId, displayName, roll.Expression,
            first.Terms.SelectMany(x => x.Values).ToArray(), first.Modifier, first.Total, results, roll.CreatedAt);
    }

    private static IReadOnlyList<DiceRollResultDto> DeserializeResults(DiceRoll roll)
    {
        try
        {
            var results = JsonSerializer.Deserialize<DiceRollResultDto[]>(roll.ValuesJson);
            if (results is { Length: > 0 }) return results;
        }
        catch (JsonException)
        {
            // Rows created before grouped rolls stored a flat integer array.
        }

        var values = JsonSerializer.Deserialize<int[]>(roll.ValuesJson) ?? [];
        var expression = DiceExpression.Parse(roll.Expression);
        var term = expression.Terms[0];
        return [new DiceRollResultDto(1,
            [new DiceTermResultDto(term.Normalized, values, checked(term.Sign * values.Sum()))],
            roll.Modifier, roll.Total)];
    }

    private static string ValidateName(string? input, int maxLength, string field)
    {
        var value = input?.Trim() ?? string.Empty;
        if (value.Length is 0 || value.Length > maxLength)
        {
            throw new AppException(AppErrorKind.Validation, $"O nome de {field} deve ter entre 1 e {maxLength} caracteres.");
        }

        return value;
    }
}
