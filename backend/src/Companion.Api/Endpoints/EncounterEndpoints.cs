using Companion.Application.Contracts;
using Companion.Application.Encounters;

namespace Companion.Api.Endpoints;

public static class EncounterEndpoints
{
    public static IEndpointRouteBuilder MapEncounterEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/campaigns/{campaignId:guid}/encounters").WithTags("Encounters");

        group.MapPost("/", async (Guid campaignId, HttpRequest request, EncounterService service, CancellationToken ct) =>
            Results.Created($"/api/campaigns/{campaignId}/encounters", await service.CreateAsync(campaignId, Token(request), ct)));

        group.MapPost("/{encounterId:guid}/combatants", async (
            Guid campaignId, Guid encounterId, AddCombatantRequest body, HttpRequest request,
            EncounterService service, CancellationToken ct) =>
            Results.Created($"/api/campaigns/{campaignId}/encounters/{encounterId}",
                await service.AddCombatantAsync(campaignId, encounterId, Token(request), body, ct)));

        group.MapPost("/{encounterId:guid}/initiative", async (
            Guid campaignId, Guid encounterId, RollInitiativeRequest? body, HttpRequest request,
            EncounterService service, CancellationToken ct) =>
            Results.Ok(await service.RollInitiativeAsync(campaignId, encounterId, Token(request), body, ct)));

        group.MapPut("/{encounterId:guid}/combatants/{combatantId:guid}", async (
            Guid campaignId, Guid encounterId, Guid combatantId, UpdateCombatantRequest body,
            HttpRequest request, EncounterService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateCombatantAsync(
                campaignId, encounterId, combatantId, Token(request), body, ct)));

        group.MapPut("/{encounterId:guid}/combatants/{combatantId:guid}/hit-points", async (
            Guid campaignId, Guid encounterId, Guid combatantId, UpdateHitPointsRequest body,
            HttpRequest request, EncounterService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateHitPointsAsync(
                campaignId, encounterId, combatantId, Token(request), body, ct)));
        group.MapDelete("/{encounterId:guid}/combatants/{combatantId:guid}", async (
            Guid campaignId, Guid encounterId, Guid combatantId,
            HttpRequest request, EncounterService service, CancellationToken ct) =>
            Results.Ok(await service.RemoveCombatantAsync(
                campaignId, encounterId, combatantId, Token(request), ct)));

        group.MapPost("/{encounterId:guid}/attacks", async (
            Guid campaignId, Guid encounterId, ResolveAttackRequest body, HttpRequest request,
            AttackService service, CancellationToken ct) =>
        {
            if (!Guid.TryParse(request.Headers["Idempotency-Key"], out var requestId))
                throw new AppException(AppErrorKind.Validation, "O cabeçalho Idempotency-Key deve ser um UUID válido.");
            var result = await service.ResolveAsync(campaignId, encounterId, Token(request), requestId, body, ct);
            return result.Created
                ? Results.Created($"/api/campaigns/{campaignId}/encounters/{encounterId}/attacks/{result.Result.Attack.Id}", result.Result)
                : Results.Ok(result.Result);
        });
        group.MapPost("/{encounterId:guid}/start", async (
            Guid campaignId, Guid encounterId, HttpRequest request, EncounterService service, CancellationToken ct) =>
            Results.Ok(await service.StartAsync(campaignId, encounterId, Token(request), ct)));

        group.MapPost("/{encounterId:guid}/next-turn", async (
            Guid campaignId, Guid encounterId, HttpRequest request, EncounterService service, CancellationToken ct) =>
            Results.Ok(await service.AdvanceAsync(campaignId, encounterId, Token(request), ct)));

        group.MapPost("/{encounterId:guid}/end", async (
            Guid campaignId, Guid encounterId, HttpRequest request, EncounterService service, CancellationToken ct) =>
            Results.Ok(await service.EndAsync(campaignId, encounterId, Token(request), ct)));

        return endpoints;
    }

    private static string Token(HttpRequest request) => request.Headers["X-Participant-Token"].ToString();
}
