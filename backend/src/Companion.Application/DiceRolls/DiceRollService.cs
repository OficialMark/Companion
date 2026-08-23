using System.Text.Json;
using Companion.Application.Campaigns;
using Companion.Application.Contracts;
using Companion.Domain.Dice;
using Companion.Domain.Models;

namespace Companion.Application.DiceRolls;

public sealed class DiceRollService(
    ICompanionDbContext db,
    ParticipantAuthorization authorization,
    IDiceRandomizer randomizer,
    ICampaignNotifier notifier,
    TimeProvider timeProvider)
{
    public async Task<(DiceRollDto Roll, bool Created)> RollAsync(
        Guid campaignId,
        string token,
        Guid requestId,
        RollDiceRequest request,
        CancellationToken cancellationToken)
    {
        var participant = authorization.Require(campaignId, token);
        var existing = db.DiceRolls.FirstOrDefault(x =>
            x.ParticipantId == participant.Id && x.ClientRequestId == requestId);
        if (existing is not null)
        {
            var existingDto = CampaignService.ToDto(existing, participant.DisplayName);
            if (request.Publish && existing.RevealedAt is null)
            {
                existing.Reveal(timeProvider.GetUtcNow());
                await db.SaveChangesAsync(cancellationToken);
                await notifier.NotifyAsync(campaignId, "DiceRolled", existingDto, cancellationToken);
            }
            return (existingDto, false);
        }

        DiceExpression expression;
        try
        {
            expression = DiceExpression.Parse(request.Expression);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException or OverflowException)
        {
            throw new AppException(AppErrorKind.Validation, exception.Message);
        }

        var results = Enumerable.Range(1, expression.Repetitions).Select(sequence =>
        {
            var terms = expression.Terms.Select(term =>
            {
                var values = Enumerable.Range(0, term.Count).Select(_ => randomizer.Roll(term.Faces)).ToArray();
                return new DiceTermResultDto(term.Normalized, values, checked(term.Sign * values.Sum()));
            }).ToArray();
            return new DiceRollResultDto(sequence, terms, expression.Modifier,
                checked(terms.Sum(x => x.Subtotal) + expression.Modifier));
        }).ToArray();
        var first = results[0];
        var roll = new DiceRoll(
            Guid.NewGuid(), campaignId, participant.Id, requestId, expression.Normalized,
            JsonSerializer.Serialize(results), expression.Modifier, first.Total, timeProvider.GetUtcNow(),
            request.Publish ? timeProvider.GetUtcNow() : null);
        db.Add(roll);
        await db.SaveChangesAsync(cancellationToken);

        var dto = CampaignService.ToDto(roll, participant.DisplayName);
        if (request.Publish)
        {
            await notifier.NotifyAsync(campaignId, "DiceRolled", dto, cancellationToken);
        }
        return (dto, true);
    }

    public async Task<DiceRollDto> RevealAsync(
        Guid campaignId,
        Guid rollId,
        string token,
        CancellationToken cancellationToken)
    {
        var participant = authorization.Require(campaignId, token);
        var roll = db.DiceRolls.FirstOrDefault(x => x.CampaignId == campaignId && x.Id == rollId)
            ?? throw new AppException(AppErrorKind.NotFound, "A rolagem não foi encontrada.");
        if (roll.ParticipantId != participant.Id)
        {
            throw new AppException(AppErrorKind.Forbidden, "Somente quem rolou os dados pode revelar o resultado.");
        }

        var dto = CampaignService.ToDto(roll, participant.DisplayName);
        if (roll.RevealedAt is null)
        {
            roll.Reveal(timeProvider.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
            await notifier.NotifyAsync(campaignId, "DiceRolled", dto, cancellationToken);
        }

        return dto;
    }
}
