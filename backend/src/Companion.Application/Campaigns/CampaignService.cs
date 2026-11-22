using System.Security.Cryptography;
using System.Text.Json;
using Companion.Application.Contracts;
using Companion.Domain.Characters;
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
        var displayName = NormalizeParticipantName(request.DisplayName);
        var (playerCode, masterCode) = CreateUniqueCodes();
        var credential = tokens.Create();
        var now = timeProvider.GetUtcNow();
        var campaign = new Campaign(Guid.NewGuid(), campaignName, now);
        var master = new Participant(Guid.NewGuid(), campaign.Id, displayName, ParticipantRole.Master, credential.Hash, now);
        db.Add(campaign);
        db.Add(new CampaignAccessCode(playerCode, campaign.Id, ParticipantRole.Player));
        db.Add(new CampaignAccessCode(masterCode, campaign.Id, ParticipantRole.Master));
        db.Add(master);
        db.Add(new ParticipantSession(Guid.NewGuid(), master.Id, campaign.Id, credential.Hash, now));
        await db.SaveChangesAsync(cancellationToken);
        return new SessionResponse(campaign.Id, playerCode, masterCode, master.Id, master.DisplayName, master.Role, credential.Token);
    }

    /// <summary>
    /// Entra na conta pelo nome (spec 011, US 2b). O nome é normalizado e procurado em
    /// (campanha, papel do código, NameKey): se existe, só ganha uma sessão nova e as outras sessões
    /// continuam valendo (FR-043); se não existe, a conta é criada. ParticipantJoined só sai na criação.
    /// </summary>
    public async Task<SessionResponse> JoinAsync(JoinCampaignRequest request, CancellationToken cancellationToken)
    {
        var code = (request.Code ?? string.Empty).Trim();
        if (code.Length != 4 || code.Any(character => character is < '0' or > '9'))
            throw new AppException(AppErrorKind.NotFound, "Código de acesso inválido.");
        var displayName = NormalizeParticipantName(request.DisplayName);
        var access = db.CampaignAccessCodes.FirstOrDefault(x => x.Code == code)
            ?? throw new AppException(AppErrorKind.NotFound, "Código de acesso inválido.");
        var campaign = db.Campaigns.FirstOrDefault(x => x.Id == access.CampaignId)
            ?? throw new AppException(AppErrorKind.NotFound, "Código de acesso inválido.");
        var credential = tokens.Create();
        var (participant, created) = await OpenAccountAsync(campaign.Id, access.Role, displayName, credential.Hash, cancellationToken);
        if (created)
            await notifier.NotifyAsync(campaign.Id, "ParticipantJoined", ToDto(participant), cancellationToken);
        var playerCode = db.CampaignAccessCodes.First(x => x.CampaignId == campaign.Id && x.Role == ParticipantRole.Player).Code;
        return new SessionResponse(campaign.Id, playerCode, null, participant.Id, participant.DisplayName, participant.Role, credential.Token);
    }

    /// <summary>"Sair da campanha" (FR-043): apaga só a sessão do token usado; as outras continuam.</summary>
    public async Task LeaveAsync(Guid campaignId, string token, CancellationToken cancellationToken)
    {
        var (_, session) = authorization.RequireSession(campaignId, token);
        db.Remove(session);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<(Participant Participant, bool Created)> OpenAccountAsync(
        Guid campaignId, ParticipantRole role, string displayName, byte[] sessionHash, CancellationToken cancellationToken)
    {
        var nameKey = PersonName.Key(displayName);
        var now = timeProvider.GetUtcNow();
        var existing = FindAccount(campaignId, role, nameKey);
        if (existing is not null)
        {
            existing.SetHiddenFromPanel(false);
            await AddSessionAsync(existing, sessionHash, now, cancellationToken);
            return (existing, false);
        }

        var participant = new Participant(Guid.NewGuid(), campaignId, displayName, role, sessionHash, now);
        var session = new ParticipantSession(Guid.NewGuid(), participant.Id, campaignId, sessionHash, now);
        db.Add(participant);
        db.Add(session);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return (participant, true);
        }
        catch (Exception exception) when (exception.GetType().Name is "DbUpdateException")
        {
            // Corrida: outro join com o mesmo nome venceu o índice único (CampaignId, Role, NameKey).
            // A inserção inteira voltou (participante e sessão na mesma transação). Descarta as
            // entidades pendentes e entra na conta vencedora. Sem vencedora, o erro era outro: repassa.
            db.Detach(session);
            db.Detach(participant);
            var winner = FindAccount(campaignId, role, nameKey);
            if (winner is null) throw;
            await AddSessionAsync(winner, sessionHash, now, cancellationToken);
            return (winner, false);
        }
    }

    private Participant? FindAccount(Guid campaignId, ParticipantRole role, string nameKey) =>
        db.Participants.FirstOrDefault(x => x.CampaignId == campaignId && x.Role == role && x.NameKey == nameKey);

    private async Task AddSessionAsync(Participant participant, byte[] sessionHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        db.Add(new ParticipantSession(Guid.NewGuid(), participant.Id, participant.CampaignId, sessionHash, now));
        await db.SaveChangesAsync(cancellationToken);
    }

    // Participants.TokenHash (NOT NULL, único): desde a spec 011 a autorização lê só ParticipantSessions,
    // mas a conta nova grava aqui o hash da primeira sessão (único, porque o hash da sessão é único).
    // Assim, se a API voltar para a versão anterior (que autoriza por esta coluna), o primeiro aparelho
    // de cada conta continua entrando (T076).

    public Task<CampaignSnapshot> GetSnapshotAsync(Guid campaignId, string token, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var current = authorization.Require(campaignId, token);
        var campaign = db.Campaigns.FirstOrDefault(x => x.Id == campaignId)
            ?? throw new AppException(AppErrorKind.NotFound, "Campanha não encontrada.");
        var participantEntities = db.Participants.Where(x => x.CampaignId == campaignId).OrderBy(x => x.CreatedAt).AsEnumerable().ToArray();
        var participants = participantEntities.Select(ToDto).ToArray();
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
            ToDto(current), participants, rolls, encounterDto));
    }

    public async Task<CampaignDto> RenameAsync(
        Guid campaignId, string token, RenameCampaignRequest request, CancellationToken cancellationToken)
    {
        var participant = authorization.Require(campaignId, token);
        if (participant.Role != ParticipantRole.Master)
            throw new AppException(AppErrorKind.Forbidden, "Somente o mestre pode alterar o nome da mesa.");

        var name = ValidateName(request.Name, 80, "campanha");
        var campaign = db.Campaigns.FirstOrDefault(x => x.Id == campaignId)
            ?? throw new AppException(AppErrorKind.NotFound, "Campanha não encontrada.");
        campaign.Rename(name);
        await db.SaveChangesAsync(cancellationToken);

        var updated = new CampaignDto(campaign.Id,
            db.CampaignAccessCodes.First(x => x.CampaignId == campaignId && x.Role == ParticipantRole.Player).Code,
            campaign.Name);
        await notifier.NotifyAsync(campaignId, "CampaignRenamed", updated, cancellationToken);
        return updated;
    }

    public async Task<ParticipantDto> SetParticipantPanelVisibilityAsync(
        Guid campaignId, Guid participantId, string token, SetParticipantPanelVisibilityRequest request,
        CancellationToken cancellationToken)
    {
        var actor = authorization.Require(campaignId, token);
        if (actor.Role != ParticipantRole.Master)
            throw new AppException(AppErrorKind.Forbidden, "Somente o mestre pode editar o painel de participantes.");

        var participant = db.Participants.FirstOrDefault(x => x.Id == participantId && x.CampaignId == campaignId)
            ?? throw new AppException(AppErrorKind.NotFound, "Participante não encontrado nesta campanha.");
        participant.SetHiddenFromPanel(request.IsHiddenFromPanel);
        await db.SaveChangesAsync(cancellationToken);
        await notifier.ParticipantsChangedAsync(campaignId, cancellationToken);
        return ToDto(participant);
    }

    public const string CombatProfileFromSheet = "Iniciativa e vida vêm da sua ficha. Edite pela aba Ficha.";

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
        // FR-010: with a sheet, initiative and HP are calculated from it (CharacterService keeps the
        // participant in sync). Editing them here would make the table and the sheet disagree.
        var ownerKey = Companion.Domain.Characters.CharacterOwner.Key(participant);
        if (db.CharacterSheets.Any(x => x.OwnerKey == ownerKey))
            throw new AppException(AppErrorKind.Conflict, CombatProfileFromSheet);
        try
        {
            participant.UpdateCombatProfile(request.InitiativeModifier,
                preserveHitPoints ? participant.CurrentHitPoints : request.CurrentHitPoints,
                preserveHitPoints ? participant.MaxHitPoints : request.MaxHitPoints);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new AppException(AppErrorKind.Validation, exception.Message);
        }
        await db.SaveChangesAsync(cancellationToken);
        var ownerDto = ToDto(participant);
        await notifier.NotifyAsync(campaignId, "ParticipantUpdated", ToDto(participant), cancellationToken);
        return ownerDto;
    }

    public async Task<ParticipantDto> UploadAvatarAsync(
        Guid campaignId, string token, byte[] content, string mediaType, CancellationToken cancellationToken)
    {
        var participant = authorization.Require(campaignId, token);
        if (content.Length is < 1 or > ParticipantAvatar.MaxImageBytes)
            throw new AppException(AppErrorKind.Validation, "A foto deve ter no máximo 1 MB.");
        if (!ImageFormat.IsValid(content, mediaType))
            throw new AppException(AppErrorKind.Validation, "A foto deve ser PNG, JPEG ou WebP válido.");
        var now = timeProvider.GetUtcNow();
        var avatar = db.ParticipantAvatars.FirstOrDefault(x => x.ParticipantId == participant.Id);
        if (avatar is null)
        {
            avatar = new ParticipantAvatar(participant.Id, content, mediaType, now);
            db.Add(avatar);
        }
        else
        {
            avatar.Replace(content, mediaType, now);
        }
        participant.SetAvatarRevision(avatar.Revision);
        await db.SaveChangesAsync(cancellationToken);
        await notifier.NotifyAsync(campaignId, "ParticipantUpdated", ToDto(participant), cancellationToken);
        return ToDto(participant);
    }

    public async Task<ParticipantDto> RemoveAvatarAsync(Guid campaignId, string token, CancellationToken cancellationToken)
    {
        var participant = authorization.Require(campaignId, token);
        var avatar = db.ParticipantAvatars.FirstOrDefault(x => x.ParticipantId == participant.Id);
        if (avatar is not null) db.Remove(avatar);
        participant.SetAvatarRevision(null);
        await db.SaveChangesAsync(cancellationToken);
        await notifier.NotifyAsync(campaignId, "ParticipantUpdated", ToDto(participant), cancellationToken);
        return ToDto(participant);
    }

    public Task<ParticipantAvatarDto> GetAvatarAsync(
        Guid campaignId, Guid participantId, Guid revision, string token, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        authorization.Require(campaignId, token);
        var belongsToCampaign = db.Participants.Any(x => x.Id == participantId && x.CampaignId == campaignId);
        var avatar = belongsToCampaign
            ? db.ParticipantAvatars.FirstOrDefault(x => x.ParticipantId == participantId && x.Revision == revision)
            : null;
        if (avatar is null)
            throw new AppException(AppErrorKind.NotFound, "A foto solicitada não está mais disponível.");
        return Task.FromResult(new ParticipantAvatarDto(avatar.Content, avatar.MediaType, avatar.Revision));
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

    // Vida (atual e máxima) visível a todos os participantes (FR-009a) — não filtrada por observador.
    private ParticipantDto ToDto(Participant participant) =>
        DtoMapper.ToDto(participant, presence.IsOnline(participant.Id));

    internal static DiceRollDto ToDto(DiceRoll roll, string displayName)
    {
        var results = DeserializeResults(roll);
        var first = results[0];
        return new DiceRollDto(roll.Id, roll.ParticipantId, displayName, roll.Expression,
            first.Terms.SelectMany(x => x.Values).ToArray(), first.Modifier, first.Total, results, roll.CreatedAt, roll.Label);
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

    private static string NormalizeParticipantName(string? input)
    {
        try
        {
            return PersonName.Normalize(input);
        }
        catch (ArgumentException exception)
        {
            throw new AppException(AppErrorKind.Validation, exception.Message);
        }
    }

    private static string ValidateName(string? input, int maxLength, string field)
    {
        var value = input?.Trim() ?? string.Empty;
        if (value.Length is 0 || value.Length > maxLength)
            throw new AppException(AppErrorKind.Validation, $"O nome de {field} deve ter entre 1 e {maxLength} caracteres.");
        return value;
    }
}
