using Companion.Application.Catalog;
using Companion.Domain.Characters;

namespace Companion.Api.Endpoints;

/// <summary>Read-only item and spell catalogs available to every authenticated campaign participant.</summary>
public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var catalog = endpoints.MapGroup("/api/campaigns/{campaignId:guid}/catalog").WithTags("Catalog");
        catalog.MapGet("/items", (Guid campaignId, string? q, ItemCategory? category, string? rarity, int? page, int? pageSize, bool? mundane,
                HttpRequest request, CatalogService service) =>
            Results.Ok(service.ListItems(campaignId, Token(request),
                new CatalogItemQuery(q, category, rarity, page ?? 1, pageSize ?? 24, mundane ?? false))));
        catalog.MapGet("/items/{itemId:guid}", (Guid campaignId, Guid itemId, HttpRequest request, CatalogService service) =>
            Results.Ok(service.GetItem(campaignId, Token(request), itemId)));
        catalog.MapGet("/spells", (Guid campaignId, string? q, int? level, string? school, string? @class, bool? ritual, bool? concentration,
                int? page, int? pageSize, HttpRequest request, CatalogService service) =>
            Results.Ok(service.ListSpells(campaignId, Token(request),
                new CatalogSpellQuery(q, level, school, @class, ritual, concentration, page ?? 1, pageSize ?? 24))));
        catalog.MapGet("/spells/{spellId:guid}", (Guid campaignId, Guid spellId, HttpRequest request, CatalogService service) =>
            Results.Ok(service.GetSpell(campaignId, Token(request), spellId)));

        return endpoints;
    }

    private static string Token(HttpRequest request) => request.Headers["X-Participant-Token"].ToString();
}
