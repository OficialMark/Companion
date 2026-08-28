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
        var (playerCode, masterCode) = CreateUniqueCodes();
        var credential = tokens.Create();
        var now = timeProvider.GetUtcNow();
        var campaign = new Campaign(Guid.NewGuid(), campaignName, now);
        var master = new Participant(Guid.NewGuid(), campaign.Id, displayName, ParticipantRole.Master, credential.Hash, now);
        db.Add(campaign);
        db.Add(new CampaignAccessCode(playerCode, campaign.Id, ParticipantRole.Player));
        db.Add(new CampaignAccessCode(masterCode, campaign.Id, ParticipantRole.Master));
        db.Add(master);
        await db.SaveChangesAsync(cancellationToken);
        return new SessionResponse(campaign.Id, playerCode, masterCode, master.Id, master.DisplayName, master.Role, credential.Token);
    }

    public async Task<SessionResponse> JoinAsync(JoinCampaignRequest request, CancellationToken cancellationToken)
    {
        var code = (request.Code ?? string.Empty).Trim();
        if (code.Length != 4 || code.Any(character => character is < '0' or > '9'))
            throw new AppException(AppErrorKind.NotFound, "Código de acesso inválido.");
        var displayName = ValidateName(request.DisplayName, 40, "participante");
        var access = db.CampaignAccessCodes.FirstOrDefault(x => x.Code == code)
            ?? throw new AppException(AppErrorKind.NotFound, "Código de acesso inválido.");
        var campaign = db.Campaigns.FirstOrDefault(x => x.Id == access.CampaignId)
            ?? throw new AppException(AppErrorKind.NotFound, "Código de acesso inválido.");
        var credential = tokens.Create();
        var participant = new Participant(Guid.NewGuid(), campaign.Id, displayName, access.Role, credential.Hash, timeProvider.GetUtcNow());
        db.Add(participant);
        await db.SaveChangesAsync(cancellationToken);
        await notifier.NotifyAsync(campaign.Id, "ParticipantJoined", ToDto(participant), cancellationToken);
        var playerCode = db.CampaignAccessCodes.First(x => x.CampaignId == campaign.Id && x.Role == ParticipantRole.Player).Code;
        return new SessionResponse(campaign.Id, playerCode, null, participant.Id, participant.DisplayName, participant.Role, credential.Token);
    }

    public Task<CampaignSnapshot> GetSnapshotAsync(Guid campaignId, string token, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var current = authorization.Require(campaignId, token);
        var campaign = db.Campaigns.FirstOrDefault(x => x.Id == campaignId)
            ?? throw new AppException(AppErrorKind.NotFound, "Campanha não encontrada.");
        var participantEntities = db.Participants.Where(x => x.CampaignId == campaignId).OrderBy(x => x.CreatedAt).AsEnumerable().ToArray();
        var participants = participantEntities.Select(x => ToDto(x, x.Id == current.Id)).ToArray();
        var participantNames = participantEntities.ToDictionary(x => x.Id, x => x.DisplayName);
        var rolls = db.DiceRolls.Where(x => x.CampaignId == campaignId && x.RevealedAt != null)
            .OrderByDescending(x => x.CreatedAt).Take(50).AsEnumerable()
            .Select(x => ToDto(x, participantNames.GetValueOrDefault(x.ParticipantId, "Participante"))).ToArray();
        var encounter = db.Encounters.Where(x => x.CampaignId == campaignId && x.Status != EncounterStatus.Ended)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefault();
        EncounterDto? encounterDto = null;
        if (encounter is not null)
        {
            _ = db.Combatants.Where(x => x.EncounterId == encounter.Id).ToArray();
            encounterDto = DtoMapper.ToDto(encounter, current);
        }
        var playerCode = db.CampaignAccessCodes.First(x => x.CampaignId == campaignId && x.Role == ParticipantRole.Player).Code;
        return Task.FromResult(new CampaignSnapshot(new CampaignDto(campaign.Id, playerCode, campaign.Name),
            ToDto(current, true), participants, rolls, encounterDto));
    }

    public Task<ParticipantDto> UpdateInitiativeModifierAsync(
        Guid campaignId, string token, UpdateInitiativeModifierRequest request, CancellationToken cancellationToken) =>
        UpdateCombatProfileAsync(campaignId, token,
            new UpdateCombatProfileRequest(request.InitiativeModifier, null), cancellationToken, preserveHitPoints: true);

    public async Task<ParticipantDto> UpdateCombatProfileAsync(
        Guid campaignId, string token, UpdateCombatProfileRequest request, CancellationToken cancellationToken,
        bool preserveHitPoints = false)
    {
        var participant = authorization.Require(campaignId, token);
        if (participant.Role != ParticipantRole.Player)
            throw new AppException(AppErrorKind.Forbidden, "Somente jogadores possuem perfil de combate.");
        try
        {
            participant.UpdateCombatProfile(request.InitiativeModifier,
                preserveHitPoints ? participant.CurrentHitPoints : request.CurrentHitPoints);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new AppException(AppErrorKind.Validation, exception.Message);
        }
        await db.SaveChangesAsync(cancellationToken);
        var ownerDto = ToDto(participant, true);
        await notifier.NotifyAsync(campaignId, "ParticipantUpdated", ToDto(participant), cancellationToken);
        return ownerDto;
    }

    public IReadOnlyList<ParticipantDto> GetParticipants(Guid campaignId) =>
        db.Participants.Where(x => x.CampaignId == campaignId).OrderBy(x => x.CreatedAt).AsEnumerable().Select(x => ToDto(x)).ToArray();

    private (string PlayerCode, string MasterCode) CreateUniqueCodes()
    {
        var used = db.CampaignAccessCodes.Select(x => x.Code).ToHashSet(StringComparer.Ordinal);
        if (used.Count > 9_998) throw new AppException(AppErrorKind.Conflict, "Não há códigos livres suficientes para criar outra mesa.");
        var selected = new List<string>(2);
        for (var attempt = 0; attempt < 64 && selected.Count < 2; attempt++)
        {
            var code = tokens.CreateCampaignCode();
            if (used.Add(code)) selected.Add(code);
        }
        for (var value = 0; value < 10_000 && selected.Count < 2; value++)
        {
            var code = value.ToString("D4");
            if (used.Add(code)) selected.Add(code);
        }
        if (selected.Count < 2) throw new AppException(AppErrorKind.Conflict, "Não há códigos livres suficientes para criar outra mesa.");
        return (selected[0], selected[1]);
    }

    private ParticipantDto ToDto(Participant participant, bool includeHitPoints = false) =>
        new(participant.Id, participant.DisplayName, participant.Role, presence.IsOnline(participant.Id),
            participant.InitiativeModifier, includeHitPoints ? participant.CurrentHitPoints : null);

    internal static DiceRollDto ToDto(DiceRoll roll, string displayName)
    {
        var results = DeserializeResults(roll);
        var first = results[0];
        return new DiceRollDto(roll.Id, roll.ParticipantId, displayName, roll.Expression,
            first.Terms.SelectMany(x => x.Values).ToArray(), first.Modifier, first.Total, results, roll.CreatedAt);
    }

    internal static IReadOnlyList<DiceRollResultDto> DeserializeResults(DiceRoll roll)
    {
        try
        {
            var results = JsonSerializer.Deserialize<DiceRollResultDto[]>(roll.ValuesJson);
            if (results is { Length: > 0 }) return results;
        }
        catch (JsonException) { }
        var values = JsonSerializer.Deserialize<int[]>(roll.ValuesJson) ?? [];
        var expression = DiceExpression.Parse(roll.Expression);
        var term = expression.Terms[0];
        return [new DiceRollResultDto(1, [new DiceTermResultDto(term.Normalized, values, checked(term.Sign * values.Sum()))], roll.Modifier, roll.Total)];
    }

    private static string ValidateName(string? input, int maxLength, string field)
    {
        var value = input?.Trim() ?? string.Empty;
        if (value.Length is 0 || value.Length > maxLength)
            throw new AppException(AppErrorKind.Validation, $"O nome de {field} deve ter entre 1 e {maxLength} caracteres.");
        return value;
    }
}