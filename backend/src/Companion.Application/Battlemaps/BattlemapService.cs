using Companion.Application.Campaigns;
using Companion.Application.Contracts;
using Companion.Domain.Models;

namespace Companion.Application.Battlemaps;

public sealed record BattlemapConfigurationResult(ReplaceBattlemapResultDto Result, bool Created);

public sealed class BattlemapService(
    ICompanionDbContext db,
    ParticipantAuthorization authorization,
    ICampaignNotifier notifier,
    TimeProvider timeProvider)
{
    public Task<BattlemapSnapshotDto> GetAsync(
        Guid campaignId,
        Guid encounterId,
        string token,
        CancellationToken cancellationToken)
    {
        authorization.Require(campaignId, token);
        var encounter = RequireEncounter(campaignId, encounterId);
        var battlemap = db.Battlemaps.SingleOrDefault(x => x.EncounterId == encounter.Id)
            ?? throw new AppException(AppErrorKind.NotFound, "O encontro ainda não possui battlemap.");
        return Task.FromResult(ToSnapshot(campaignId, battlemap));
    }

    public Task<BattlemapImageDto> GetImageAsync(
        Guid campaignId,
        Guid encounterId,
        Guid revision,
        string token,
        CancellationToken cancellationToken)
    {
        authorization.Require(campaignId, token);
        RequireEncounter(campaignId, encounterId);
        var battlemap = db.Battlemaps.SingleOrDefault(x =>
                x.EncounterId == encounterId && x.ImageRevision == revision)
            ?? throw new AppException(AppErrorKind.NotFound, "A imagem solicitada não está mais disponível.");
        return Task.FromResult(new BattlemapImageDto(
            battlemap.ImageContent, battlemap.ImageMediaType, battlemap.ImageFileName, battlemap.ImageRevision));
    }

    public async Task<BattlemapConfigurationResult> ConfigureAsync(
        Guid campaignId,
        Guid encounterId,
        string token,
        ConfigureBattlemapRequest request,
        CancellationToken cancellationToken)
    {
        authorization.Require(campaignId, token, masterOnly: true);
        RequireEncounter(campaignId, encounterId);
        ValidateImage(request.ImageContent, request.ImageMediaType);
        var now = timeProvider.GetUtcNow();
        var battlemap = db.Battlemaps.SingleOrDefault(x => x.EncounterId == encounterId);
        var created = battlemap is null;
        var returnedToReserve = new List<Guid>();

        try
        {
            if (battlemap is null)
            {
                if (!string.IsNullOrWhiteSpace(request.ExpectedRevision))
                    throw new AppException(AppErrorKind.Conflict, "O battlemap mudou. Atualize a mesa.");
                battlemap = new Battlemap(
                    Guid.NewGuid(),
                    encounterId,
                    request.ImageContent,
                    request.ImageMediaType,
                    request.ImageFileName,
                    request.Columns,
                    request.Rows,
                    request.IsGridVisible,
                    now);
                db.Add(battlemap);
            }
            else
            {
                EnsureRevision(request.ExpectedRevision, battlemap.Version, "O battlemap mudou. Atualize a mesa.");
                battlemap.Replace(
                    request.ImageContent,
                    request.ImageMediaType,
                    request.ImageFileName,
                    request.Columns,
                    request.Rows,
                    request.IsGridVisible,
                    now);
                var outside = db.BattlemapTokenPositions
                    .Where(x => x.BattlemapId == battlemap.Id)
                    .AsEnumerable()
                    .Where(x => !battlemap.Contains(x.Column, x.Row))
                    .ToArray();
                foreach (var position in outside)
                {
                    returnedToReserve.Add(position.ParticipantId ?? position.CombatantId!.Value);
                    db.Remove(position);
                }
                foreach (var area in db.AttackAreas.Where(x => x.BattlemapId == battlemap.Id).ToArray())
                    db.Remove(area);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            throw new AppException(AppErrorKind.Validation, exception.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        var snapshot = ToSnapshot(campaignId, battlemap);
        await notifier.BattlemapInvalidatedAsync(
            campaignId, encounterId, snapshot.Revision, cancellationToken);
        return new BattlemapConfigurationResult(
            new ReplaceBattlemapResultDto(snapshot, returnedToReserve), created);
    }

    public async Task<BattlemapSnapshotDto> MoveAsync(
        Guid campaignId,
        Guid encounterId,
        Guid tokenId,
        string token,
        MoveBattlemapTokenRequest request,
        CancellationToken cancellationToken)
    {
        var actor = authorization.Require(campaignId, token);
        var encounter = RequireEncounter(campaignId, encounterId);
        var participant = db.Participants.SingleOrDefault(x =>
            x.CampaignId == campaignId && x.Id == tokenId && x.Role == ParticipantRole.Player);
        var combatant = participant is null
            ? db.Combatants.SingleOrDefault(x => x.EncounterId == encounter.Id && x.Id == tokenId && x.ParticipantId == null)
            : null;
        if (participant is null && combatant is null)
            throw new AppException(AppErrorKind.NotFound, "O pino não foi encontrado.");
        if (actor.Role != ParticipantRole.Master && participant?.Id != actor.Id)
            throw new AppException(AppErrorKind.Forbidden, "Você só pode mover o próprio pino.");

        var battlemap = db.Battlemaps.SingleOrDefault(x => x.EncounterId == encounterId)
            ?? throw new AppException(AppErrorKind.NotFound, "O encontro ainda não possui battlemap.");
        if (request.Column.HasValue != request.Row.HasValue)
            throw new AppException(AppErrorKind.Validation, "Informe coluna e linha juntas ou remova ambas.");
        if (request.Column.HasValue)
        {
            try { battlemap.ValidateCoordinate(request.Column.Value, request.Row!.Value); }
            catch (ArgumentOutOfRangeException exception) { throw new AppException(AppErrorKind.Validation, exception.Message); }
        }

        var position = participant is not null
            ? db.BattlemapTokenPositions.SingleOrDefault(x =>
                x.BattlemapId == battlemap.Id && x.ParticipantId == tokenId)
            : db.BattlemapTokenPositions.SingleOrDefault(x =>
                x.BattlemapId == battlemap.Id && x.CombatantId == tokenId);
        var now = timeProvider.GetUtcNow();
        if (!request.Column.HasValue)
        {
            if (position is not null)
            {
                EnsureRevision(request.ExpectedPositionVersion, position.Version, "A posição do pino mudou. Atualize o mapa.");
                db.Remove(position);
                await db.SaveChangesAsync(cancellationToken);
                var reserveSnapshot = ToSnapshot(campaignId, battlemap);
                await notifier.BattlemapInvalidatedAsync(campaignId, encounterId, reserveSnapshot.Revision, cancellationToken);
                return reserveSnapshot;
            }
            return ToSnapshot(campaignId, battlemap);
        }
        if (position is null)
        {
            if (!string.IsNullOrWhiteSpace(request.ExpectedPositionVersion))
                throw new AppException(AppErrorKind.Conflict, "A posição do pino mudou. Atualize o mapa.");
            position = participant is not null
                ? new BattlemapTokenPosition(Guid.NewGuid(), battlemap.Id, tokenId, request.Column.Value, request.Row!.Value, now)
                : BattlemapTokenPosition.ForCombatant(Guid.NewGuid(), battlemap.Id, tokenId, request.Column.Value, request.Row!.Value, now);
            db.Add(position);
        }
        else
        {
            EnsureRevision(
                request.ExpectedPositionVersion,
                position.Version,
                "A posição do pino mudou. Atualize o mapa.");
            position.Move(request.Column.Value, request.Row!.Value, now);
        }

        await db.SaveChangesAsync(cancellationToken);
        var snapshot = ToSnapshot(campaignId, battlemap);
        await notifier.BattlemapInvalidatedAsync(
            campaignId, encounterId, snapshot.Revision, cancellationToken);
        return snapshot;
    }
    public async Task<BattlemapSnapshotDto> SetAttackAreaAsync(
        Guid campaignId,
        Guid encounterId,
        Guid tokenId,
        string token,
        SetAttackAreaRequest request,
        CancellationToken cancellationToken)
    {
        var (encounter, battlemap, participant, combatant) = ResolveTokenForMutation(campaignId, encounterId, tokenId, token);

        if (!Enum.TryParse<AttackAreaShape>(request.Shape, ignoreCase: true, out var shape))
            throw new AppException(AppErrorKind.Validation, "Formato de área inválido.");

        var now = timeProvider.GetUtcNow();
        var existing = participant is not null
            ? db.AttackAreas.SingleOrDefault(x => x.BattlemapId == battlemap.Id && x.ParticipantId == tokenId)
            : db.AttackAreas.SingleOrDefault(x => x.BattlemapId == battlemap.Id && x.CombatantId == tokenId);

        try
        {
            if (existing is null)
            {
                var area = participant is not null
                    ? AttackArea.ForParticipant(Guid.NewGuid(), battlemap.Id, tokenId, shape, request.Radius, request.Width, request.DirectionDegrees, now)
                    : AttackArea.ForCombatant(Guid.NewGuid(), battlemap.Id, tokenId, shape, request.Radius, request.Width, request.DirectionDegrees, now);
                db.Add(area);
            }
            else
            {
                existing.Update(shape, request.Radius, request.Width, request.DirectionDegrees, now);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            throw new AppException(AppErrorKind.Validation, exception.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        var snapshot = ToSnapshot(campaignId, battlemap);
        await notifier.BattlemapInvalidatedAsync(campaignId, encounter.Id, snapshot.Revision, cancellationToken);
        return snapshot;
    }

    public async Task<BattlemapSnapshotDto> RemoveAttackAreaAsync(
        Guid campaignId,
        Guid encounterId,
        Guid tokenId,
        string token,
        CancellationToken cancellationToken)
    {
        var (encounter, battlemap, participant, _) = ResolveTokenForMutation(campaignId, encounterId, tokenId, token);

        var existing = participant is not null
            ? db.AttackAreas.SingleOrDefault(x => x.BattlemapId == battlemap.Id && x.ParticipantId == tokenId)
            : db.AttackAreas.SingleOrDefault(x => x.BattlemapId == battlemap.Id && x.CombatantId == tokenId);
        if (existing is null) return ToSnapshot(campaignId, battlemap);

        db.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
        var snapshot = ToSnapshot(campaignId, battlemap);
        await notifier.BattlemapInvalidatedAsync(campaignId, encounter.Id, snapshot.Revision, cancellationToken);
        return snapshot;
    }

    public async Task<BattlemapSnapshotDto> SetTokenAppearanceAsync(
        Guid campaignId,
        Guid encounterId,
        Guid tokenId,
        string token,
        SetTokenAppearanceRequest request,
        CancellationToken cancellationToken)
    {
        var (encounter, battlemap, participant, combatant) = ResolveTokenForMutation(campaignId, encounterId, tokenId, token);

        var conditions = request.StatusConditions.Select(name =>
            Enum.TryParse<StatusCondition>(name, ignoreCase: true, out var parsed)
                ? parsed
                : throw new AppException(AppErrorKind.Validation, $"Condição de status inválida: {name}.")).ToList();

        var existing = participant is not null
            ? db.TokenAppearances.SingleOrDefault(x => x.BattlemapId == battlemap.Id && x.ParticipantId == tokenId)
            : db.TokenAppearances.SingleOrDefault(x => x.BattlemapId == battlemap.Id && x.CombatantId == tokenId);

        try
        {
            var appearance = existing ?? (participant is not null
                ? BattlemapTokenAppearance.ForParticipant(Guid.NewGuid(), battlemap.Id, tokenId)
                : BattlemapTokenAppearance.ForCombatant(Guid.NewGuid(), battlemap.Id, tokenId));
            appearance.SetFootprintSize(request.FootprintSize);
            appearance.SetStatusConditions(conditions);
            if (existing is null) db.Add(appearance);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new AppException(AppErrorKind.Validation, exception.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        var snapshot = ToSnapshot(campaignId, battlemap);
        await notifier.BattlemapInvalidatedAsync(campaignId, encounter.Id, snapshot.Revision, cancellationToken);
        return snapshot;
    }

    private (Encounter Encounter, Battlemap Battlemap, Participant? Participant, Combatant? Combatant) ResolveTokenForMutation(
        Guid campaignId, Guid encounterId, Guid tokenId, string token)
    {
        var actor = authorization.Require(campaignId, token);
        var encounter = RequireEncounter(campaignId, encounterId);
        var participant = db.Participants.SingleOrDefault(x =>
            x.CampaignId == campaignId && x.Id == tokenId && x.Role == ParticipantRole.Player);
        var combatant = participant is null
            ? db.Combatants.SingleOrDefault(x => x.EncounterId == encounter.Id && x.Id == tokenId && x.ParticipantId == null)
            : null;
        if (participant is null && combatant is null)
            throw new AppException(AppErrorKind.NotFound, "O pino não foi encontrado.");
        if (actor.Role != ParticipantRole.Master && participant?.Id != actor.Id)
            throw new AppException(AppErrorKind.Forbidden, "Você só pode configurar a área do próprio pino.");

        var battlemap = db.Battlemaps.SingleOrDefault(x => x.EncounterId == encounterId)
            ?? throw new AppException(AppErrorKind.NotFound, "O encontro ainda não possui battlemap.");
        return (encounter, battlemap, participant, combatant);
    }

    private Encounter RequireEncounter(Guid campaignId, Guid encounterId) =>
        db.Encounters.SingleOrDefault(x => x.Id == encounterId && x.CampaignId == campaignId)
        ?? throw new AppException(AppErrorKind.NotFound, "Encontro não encontrado.");

    private BattlemapSnapshotDto ToSnapshot(Guid campaignId, Battlemap battlemap)
    {
        var positions = db.BattlemapTokenPositions
            .Where(x => x.BattlemapId == battlemap.Id)
            .ToArray();
        var playerPositions = positions.Where(x => x.ParticipantId.HasValue)
            .ToDictionary(x => x.ParticipantId!.Value);
        var creaturePositions = positions.Where(x => x.CombatantId.HasValue)
            .ToDictionary(x => x.CombatantId!.Value);
        var areas = db.AttackAreas
            .Where(x => x.BattlemapId == battlemap.Id)
            .ToArray();
        var playerAreas = areas.Where(x => x.ParticipantId.HasValue)
            .ToDictionary(x => x.ParticipantId!.Value);
        var creatureAreas = areas.Where(x => x.CombatantId.HasValue)
            .ToDictionary(x => x.CombatantId!.Value);
        var appearances = db.TokenAppearances
            .Where(x => x.BattlemapId == battlemap.Id)
            .ToArray();
        var playerAppearances = appearances.Where(x => x.ParticipantId.HasValue)
            .ToDictionary(x => x.ParticipantId!.Value);
        var creatureAppearances = appearances.Where(x => x.CombatantId.HasValue)
            .ToDictionary(x => x.CombatantId!.Value);
        var playerTokens = db.Participants
            .Where(x => x.CampaignId == campaignId && x.Role == ParticipantRole.Player)
            .OrderBy(x => x.CreatedAt)
            .AsEnumerable()
            .Select(participant =>
            {
                playerPositions.TryGetValue(participant.Id, out var position);
                playerAreas.TryGetValue(participant.Id, out var area);
                playerAppearances.TryGetValue(participant.Id, out var appearance);
                return new BattlemapTokenDto(
                    participant.Id,
                    participant.DisplayName,
                    DtoMapper.Initials(participant.DisplayName),
                    DtoMapper.ColorKey(participant.Id),
                    position?.Column,
                    position?.Row,
                    position is null ? null : DtoMapper.Revision(position.Version),
                    AttackArea: area is null ? null : ToDto(area),
                    FootprintSize: appearance?.FootprintSize ?? 1,
                    StatusConditions: appearance?.StatusConditions.Select(x => x.ToString()).ToArray() ?? []);
            });
        var creatureTokens = db.Combatants
            .Where(x => x.EncounterId == battlemap.EncounterId && x.ParticipantId == null)
            .OrderBy(x => x.CreationOrder)
            .AsEnumerable()
            .Select(combatant =>
            {
                creaturePositions.TryGetValue(combatant.Id, out var position);
                creatureAreas.TryGetValue(combatant.Id, out var area);
                creatureAppearances.TryGetValue(combatant.Id, out var appearance);
                return new BattlemapTokenDto(
                    combatant.Id,
                    combatant.Name,
                    DtoMapper.Initials(combatant.Name),
                    DtoMapper.ColorKey(combatant.Id),
                    position?.Column,
                    position?.Row,
                    position is null ? null : DtoMapper.Revision(position.Version),
                    IsCreature: true,
                    AttackArea: area is null ? null : ToDto(area),
                    FootprintSize: appearance?.FootprintSize ?? 1,
                    StatusConditions: appearance?.StatusConditions.Select(x => x.ToString()).ToArray() ?? []);
            });
        var tokens = playerTokens.Concat(creatureTokens).ToArray();
        return new BattlemapSnapshotDto(
            battlemap.Id,
            battlemap.EncounterId,
            battlemap.Columns,
            battlemap.Rows,
            battlemap.IsGridVisible,
            DtoMapper.Revision(battlemap.Version),
            battlemap.ImageRevision,
            $"/api/campaigns/{campaignId}/encounters/{battlemap.EncounterId}/battlemap/image?revision={battlemap.ImageRevision}",
            battlemap.ImageMediaType,
            battlemap.ImageFileName,
            battlemap.UpdatedAt,
            tokens);
    }
    private static AttackAreaDto ToDto(AttackArea area) => new(
        area.Id,
        area.Shape.ToString(),
        area.Radius,
        area.Width,
        area.DirectionDegrees,
        area.UpdatedAt);

    private static void EnsureRevision(string? supplied, byte[] current, string message)
    {
        if (string.IsNullOrWhiteSpace(supplied) ||
            !CryptographicEquals(supplied, DtoMapper.Revision(current)))
            throw new AppException(AppErrorKind.Conflict, message);
    }

    private static bool CryptographicEquals(string left, string right)
    {
        var leftBytes = System.Text.Encoding.UTF8.GetBytes(left);
        var rightBytes = System.Text.Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length &&
            System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static void ValidateImage(byte[] bytes, string mediaType)
    {
        if (bytes.Length is < 1 or > Battlemap.MaxImageBytes)
            throw new AppException(AppErrorKind.Validation, "A imagem deve ter no máximo 20 MB.");
        var valid = mediaType switch
        {
            "image/png" => bytes.Length >= 8 &&
                bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A,
            "image/jpeg" => bytes.Length >= 3 &&
                bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            "image/webp" => bytes.Length >= 12 &&
                bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
        if (!valid)
            throw new AppException(AppErrorKind.Validation, "A imagem deve ser PNG, JPEG ou WebP válido.");
    }
}
