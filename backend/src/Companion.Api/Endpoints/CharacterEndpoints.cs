using Companion.Application.Catalog;
using Companion.Application.Characters;
using Companion.Domain.Catalog;
using Companion.Domain.Characters;

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
            Results.Ok(await service.AddItemAsync(campaignId, Token(request), body, ct)));
        me.MapPost("/items/from-catalog", async (Guid campaignId, AddFromCatalogRequest body, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.AddItemFromCatalogAsync(campaignId, Token(request), body, ct)));
        me.MapPut("/items/{itemId:guid}", async (Guid campaignId, Guid itemId, SaveItemRequest body, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.SaveItemAsync(campaignId, Token(request), itemId, body, ct)));
        me.MapPost("/items/{itemId:guid}/equip", async (Guid campaignId, Guid itemId, EquipItemRequest body, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.EquipAsync(campaignId, Token(request), itemId, body, ct)));
        me.MapDelete("/items/{itemId:guid}", async (Guid campaignId, Guid itemId, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.RemoveItemAsync(campaignId, Token(request), itemId, ct)));

        me.MapPost("/spells", async (Guid campaignId, SaveSpellRequest body, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.AddSpellAsync(campaignId, Token(request), body, ct)));
        me.MapPost("/spells/from-catalog", async (Guid campaignId, AddSpellFromCatalogRequest body, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.AddSpellFromCatalogAsync(campaignId, Token(request), body, ct)));
        me.MapPut("/spells/{spellId:guid}", async (Guid campaignId, Guid spellId, SaveSpellRequest body, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.SaveSpellAsync(campaignId, Token(request), spellId, body, ct)));
        me.MapDelete("/spells/{spellId:guid}", async (Guid campaignId, Guid spellId, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.RemoveSpellAsync(campaignId, Token(request), spellId, ct)));

        me.MapPost("/rolls", async (Guid campaignId, SaveRollRequest body, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.AddRollAsync(campaignId, Token(request), body, ct)));
        me.MapPut("/rolls/{rollId:guid}", async (Guid campaignId, Guid rollId, SaveRollRequest body, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.SaveRollAsync(campaignId, Token(request), rollId, body, ct)));
        me.MapPost("/rolls/{rollId:guid}/duplicate", async (Guid campaignId, Guid rollId, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.DuplicateRollAsync(campaignId, Token(request), rollId, ct)));
        me.MapDelete("/rolls/{rollId:guid}", async (Guid campaignId, Guid rollId, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.RemoveRollAsync(campaignId, Token(request), rollId, ct)));

        // Spec 013, US2: features with a use counter. Every answer is the whole recalculated character.
        me.MapPost("/features", async (Guid campaignId, SaveFeatureRequest body, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.AddFeatureAsync(campaignId, Token(request), body, ct)));
        me.MapPut("/features/{featureId:guid}", async (Guid campaignId, Guid featureId, SaveFeatureRequest body, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.SaveFeatureAsync(campaignId, Token(request), featureId, body, ct)));
        me.MapDelete("/features/{featureId:guid}", async (Guid campaignId, Guid featureId, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.RemoveFeatureAsync(campaignId, Token(request), featureId, ct)));
        me.MapPost("/features/{featureId:guid}/uses", async (Guid campaignId, Guid featureId, FeatureUsesRequest body, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.SpendFeatureAsync(campaignId, Token(request), featureId, body, ct)));
        me.MapPost("/features/{featureId:guid}/restore", async (Guid campaignId, Guid featureId, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.RestoreFeatureAsync(campaignId, Token(request), featureId, ct)));
        me.MapPost("/features/{featureId:guid}/move", async (Guid campaignId, Guid featureId, MoveFeatureRequest body, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.MoveFeatureAsync(campaignId, Token(request), featureId, body, ct)));
        me.MapPost("/rest", async (Guid campaignId, RestRequest body, HttpRequest request, CharacterService service, CancellationToken ct) =>
            Results.Ok(await service.RestAsync(campaignId, Token(request), body, ct)));

        var catalog = endpoints.MapGroup("/api/campaigns/{campaignId:guid}/catalog").WithTags("Catalog");
        catalog.MapGet("/items", (Guid campaignId, string? q, ItemCategory? category, string? rarity, int? page, int? pageSize, bool? mundane,
                CatalogOrigin? origin, HttpRequest request, CatalogService service) =>
            Results.Ok(service.ListItems(campaignId, Token(request),
                new CatalogItemQuery(q, category, rarity, page ?? 1, pageSize ?? 24, mundane ?? false, origin))));
        // Spec 013, US1: the master's campaign entries. Writes are master-only (checked in the service).
        catalog.MapPost("/items", async (Guid campaignId, SaveCatalogItemRequest body, HttpRequest request, CatalogService service, CancellationToken ct) =>
            Results.Ok(await service.CreateItemAsync(campaignId, Token(request), body, ct)));
        catalog.MapPut("/items/{itemId:guid}", async (Guid campaignId, Guid itemId, SaveCatalogItemRequest body, HttpRequest request, CatalogService service, CancellationToken ct) =>
            Results.Ok(await service.SaveItemAsync(campaignId, Token(request), itemId, body, ct)));
        catalog.MapDelete("/items/{itemId:guid}", async (Guid campaignId, Guid itemId, HttpRequest request, CatalogService service, CancellationToken ct) =>
            await service.DeleteItemAsync(campaignId, Token(request), itemId, ct) is { } original ? Results.Ok(original) : Results.NoContent());
        catalog.MapPost("/spells", async (Guid campaignId, SaveCatalogSpellRequest body, HttpRequest request, CatalogService service, CancellationToken ct) =>
            Results.Ok(await service.CreateSpellAsync(campaignId, Token(request), body, ct)));
        catalog.MapPut("/spells/{spellId:guid}", async (Guid campaignId, Guid spellId, SaveCatalogSpellRequest body, HttpRequest request, CatalogService service, CancellationToken ct) =>
            Results.Ok(await service.SaveSpellAsync(campaignId, Token(request), spellId, body, ct)));
        catalog.MapDelete("/spells/{spellId:guid}", async (Guid campaignId, Guid spellId, HttpRequest request, CatalogService service, CancellationToken ct) =>
            await service.DeleteSpellAsync(campaignId, Token(request), spellId, ct) is { } original ? Results.Ok(original) : Results.NoContent());
        catalog.MapGet("/items/{itemId:guid}", (Guid campaignId, Guid itemId, HttpRequest request, CatalogService service) =>
            Results.Ok(service.GetItem(campaignId, Token(request), itemId)));
        catalog.MapGet("/spells", (Guid campaignId, string? q, int? level, string? school, string? @class, bool? ritual, bool? concentration,
                int? page, int? pageSize, CatalogOrigin? origin, HttpRequest request, CatalogService service) =>
            Results.Ok(service.ListSpells(campaignId, Token(request),
                new CatalogSpellQuery(q, level, school, @class, ritual, concentration, page ?? 1, pageSize ?? 24, origin))));
        catalog.MapGet("/spells/{spellId:guid}", (Guid campaignId, Guid spellId, HttpRequest request, CatalogService service) =>
            Results.Ok(service.GetSpell(campaignId, Token(request), spellId)));

        return endpoints;
    }

    private static string Token(HttpRequest request) => request.Headers["X-Participant-Token"].ToString();
}
