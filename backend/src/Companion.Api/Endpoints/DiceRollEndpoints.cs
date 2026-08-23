using Companion.Application.Contracts;
using Companion.Application.DiceRolls;

namespace Companion.Api.Endpoints;

public static class DiceRollEndpoints
{
    public static IEndpointRouteBuilder MapDiceRollEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/campaigns/{campaignId:guid}/dice-rolls", async (
            Guid campaignId,
            RollDiceRequest request,
            HttpRequest httpRequest,
            DiceRollService service,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(httpRequest.Headers["Idempotency-Key"], out var requestId))
            {
                throw new AppException(AppErrorKind.Validation, "O cabeçalho Idempotency-Key deve ser um UUID válido.");
            }

            var result = await service.RollAsync(
                campaignId,
                httpRequest.Headers["X-Participant-Token"].ToString(),
                requestId,
                request,
                ct);
            return result.Created
                ? Results.Created($"/api/campaigns/{campaignId}/dice-rolls/{result.Roll.Id}", result.Roll)
                : Results.Ok(result.Roll);
        }).WithTags("Dice Rolls");

        endpoints.MapPost("/api/campaigns/{campaignId:guid}/dice-rolls/{rollId:guid}/reveal", async (
            Guid campaignId,
            Guid rollId,
            HttpRequest httpRequest,
            DiceRollService service,
            CancellationToken ct) => Results.Ok(await service.RevealAsync(
                campaignId,
                rollId,
                httpRequest.Headers["X-Participant-Token"].ToString(),
                ct))).WithTags("Dice Rolls");

        return endpoints;
    }
}
