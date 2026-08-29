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
        try
        {
            battlemap.ValidateCoordinate(request.Column, request.Row);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new AppException(AppErrorKind.Validation, exception.Message);
        }

        var position = participant is not null
            ? db.BattlemapTokenPositions.SingleOrDefault(x =>
                x.BattlemapId == battlemap.Id && x.ParticipantId == tokenId)
            : db.BattlemapTokenPositions.SingleOrDefault(x =>
                x.BattlemapId == battlemap.Id && x.CombatantId == tokenId);
        var now = timeProvider.GetUtcNow();
        if (position is null)
        {
            if (!string.IsNullOrWhiteSpace(request.ExpectedPositionVersion))
                throw new AppException(AppErrorKind.Conflict, "A posição do pino mudou. Atualize o mapa.");
            position = participant is not null
                ? new BattlemapTokenPosition(Guid.NewGuid(), battlemap.Id, tokenId, request.Column, request.Row, now)
                : BattlemapTokenPosition.ForCombatant(Guid.NewGuid(), battlemap.Id, tokenId, request.Column, request.Row, now);
            db.Add(position);
        }
        else
        {
            EnsureRevision(
                request.ExpectedPositionVersion,
                position.Version,
                "A posição do pino mudou. Atualize o mapa.");
            position.Move(request.Column, request.Row, now);
        }

        await db.SaveChangesAsync(cancellationToken);
        var snapshot = ToSnapshot(campaignId, battlemap);
        await notifier.BattlemapInvalidatedAsync(
            campaignId, encounterId, snapshot.Revision, cancellationToken);
        return snapshot;
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
        var playerTokens = db.Participants
            .Where(x => x.CampaignId == campaignId && x.Role == ParticipantRole.Player)
            .OrderBy(x => x.CreatedAt)
            .AsEnumerable()
            .Select(participant =>
            {
                playerPositions.TryGetValue(participant.Id, out var position);
                return new BattlemapTokenDto(
                    participant.Id,
                    participant.DisplayName,
                    DtoMapper.Initials(participant.DisplayName),
                    DtoMapper.ColorKey(participant.Id),
                    position?.Column,
                    position?.Row,
                    position is null ? null : DtoMapper.Revision(position.Version));
            });
        var creatureTokens = db.Combatants
            .Where(x => x.EncounterId == battlemap.EncounterId && x.ParticipantId == null)
            .OrderBy(x => x.CreationOrder)
            .AsEnumerable()
            .Select(combatant =>
            {
                creaturePositions.TryGetValue(combatant.Id, out var position);
                return new BattlemapTokenDto(
                    combatant.Id,
                    combatant.Name,
                    DtoMapper.Initials(combatant.Name),
                    DtoMapper.ColorKey(combatant.Id),
                    position?.Column,
                    position?.Row,
                    position is null ? null : DtoMapper.Revision(position.Version),
                    true);
            });
        var tokens = playerTokens.Concat(creatureTokens).ToArray();
        return new BattlemapSnapshotDto(
            battlemap.Id,
            battlemap.EncounterId,
            battlemap.Columns,
            battlemap.Rows,
            DtoMapper.Revision(battlemap.Version),
            battlemap.ImageRevision,
            $"/api/campaigns/{campaignId}/encounters/{battlemap.EncounterId}/battlemap/image?revision={battlemap.ImageRevision}",
            battlemap.ImageMediaType,
            battlemap.ImageFileName,
            battlemap.UpdatedAt,
            tokens);
    }
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
