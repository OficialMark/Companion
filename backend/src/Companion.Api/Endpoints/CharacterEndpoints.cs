using Companion.Application.Characters;

namespace Companion.Api.Endpoints;

/// <summary>
/// Spec 011, contracts/character-api.md. Character routes live under participants/me: the owner is
/// the session's participant, never an id from the request. Catalogs are open to any participant.
/// </summary>
public static class CharacterEndpoints
{
    public static IEndpointRouteBuilder MapCharacterEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var me = endpoints.MapGroup("/api/campaigns/{campaignId:guid}/participants/me/character").WithTags("Character");

        me.MapGet("/", async (Guid campaignId, bool? ifExists, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(campaignId, Token(request), ct, ifExists ?? false)));
        me.MapPut("/", async (Guid campaignId, SaveSheetRequest body, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.SaveSheetAsync(campaignId, Token(request), body, ct)));

        me.MapPost("/items", async (Guid campaignId, SaveItemRequest body, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Json(await service.AddItemAsync(campaignId, Token(request), body, ct), statusCode: StatusCodes.Status201Created));
        me.MapPost("/items/from-catalog", async (Guid campaignId, AddFromCatalogRequest body, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Json(await service.AddItemFromCatalogAsync(campaignId, Token(request), body, ct), statusCode: StatusCodes.Status201Created));
        me.MapPut("/items/{itemId:guid}", async (Guid campaignId, Guid itemId, SaveItemRequest body, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.SaveItemAsync(campaignId, Token(request), itemId, body, ct)));
        me.MapPost("/items/{itemId:guid}/equip", async (Guid campaignId, Guid itemId, EquipItemRequest body, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.EquipAsync(campaignId, Token(request), itemId, body, ct)));
        me.MapDelete("/items/{itemId:guid}", async (Guid campaignId, Guid itemId, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.RemoveItemAsync(campaignId, Token(request), itemId, ct)));

        me.MapPost("/spells", async (Guid campaignId, SaveSpellRequest body, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Json(await service.AddSpellAsync(campaignId, Token(request), body, ct), statusCode: StatusCodes.Status201Created));
        me.MapPost("/spells/from-catalog", async (Guid campaignId, AddSpellFromCatalogRequest body, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Json(await service.AddSpellFromCatalogAsync(campaignId, Token(request), body, ct), statusCode: StatusCodes.Status201Created));
        me.MapPut("/spells/{spellId:guid}", async (Guid campaignId, Guid spellId, SaveSpellRequest body, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.SaveSpellAsync(campaignId, Token(request), spellId, body, ct)));
        me.MapDelete("/spells/{spellId:guid}", async (Guid campaignId, Guid spellId, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.RemoveSpellAsync(campaignId, Token(request), spellId, ct)));

        me.MapPost("/rolls", async (Guid campaignId, SaveRollRequest body, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Json(await service.AddRollAsync(campaignId, Token(request), body, ct), statusCode: StatusCodes.Status201Created));
        me.MapPut("/rolls/{rollId:guid}", async (Guid campaignId, Guid rollId, SaveRollRequest body, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.SaveRollAsync(campaignId, Token(request), rollId, body, ct)));
        me.MapPost("/rolls/{rollId:guid}/duplicate", async (Guid campaignId, Guid rollId, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Json(await service.DuplicateRollAsync(campaignId, Token(request), rollId, ct), statusCode: StatusCodes.Status201Created));
        me.MapDelete("/rolls/{rollId:guid}", async (Guid campaignId, Guid rollId, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.RemoveRollAsync(campaignId, Token(request), rollId, ct)));

        return endpoints;
    }

    private static string Token(HttpRequest request) => request.Headers["X-Participant-Token"].ToString();
}
