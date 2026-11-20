using System.Net.Http.Json;
using Companion.Application.Catalog;
using Companion.Application.Contracts;
using Companion.Domain.Catalog;
using Companion.Domain.Characters;
using Companion.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Companion.IntegrationTests;

/// <summary>
/// Spec 012, FR-003: item and spell search matches the Portuguese name, the English name and the
/// description; name matches come first; a term under 3 characters only matches names.
/// </summary>
public sealed class CatalogSearchTests(CompanionApiFactory factory) : CharacterTestBase(factory)
{
    private async Task<CatalogPageDto<T>> Page<T>(SessionResponse session, string query)
    {
        var response = await Send(HttpMethod.Get, $"/api/campaigns/{session.CampaignId}/catalog/{query}", session);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CatalogPageDto<T>>(JsonOptions))!;
    }

    private static CatalogItem Gear(string name, string? nameEn, string? description) =>
        new(Guid.NewGuid(), $"gear-{Guid.NewGuid():N}", new CatalogItemFields(
            new InventoryItemData(name, ItemCategory.Gear, 1, null, description, null, false, false, 0, null, null, null, []),
            nameEn, null, null, false, null));

    private static CatalogSpell Spell(string name, string? nameEn, string? description) =>
        new(Guid.NewGuid(), $"spell-{Guid.NewGuid():N}", new CatalogSpellFields(
            new CharacterSpellData(name, 1, "evocação", null, null, null, null, false, false, description,
                SpellAttackKind.None, null, null, null, false), [], null, nameEn));

    private async Task Seed(params object[] rows)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        foreach (var row in rows) db.Add(row);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Items_match_the_english_name_and_the_description_ignoring_accents()
    {
        var tag = Suffix().ToLowerInvariant();
        await Seed(Gear($"Lâmina {tag}", $"Blade{tag}", "Uma lâmina simples."),
                   Gear($"Bússola {tag}", null, $"<p>Aponta sempre para o <strong>nortê{tag}</strong>.</p>"));
        var (_, player) = await Table();

        var english = await Page<CatalogItemSummaryDto>(player, $"items?q=BLADE{tag}");
        var description = await Page<CatalogItemSummaryDto>(player, $"items?q=norte{tag}");

        Assert.Equal($"Lâmina {tag}", Assert.Single(english.Items).Name);
        Assert.Equal($"Bússola {tag}", Assert.Single(description.Items).Name);
    }

    [Fact]
    public async Task Name_matches_come_before_description_matches()
    {
        var tag = Suffix().ToLowerInvariant();
        // Alphabetically "Aaa" would come first; the name match must still lead.
        await Seed(Gear($"Aaa {tag}", null, $"Feito com pena de corvo{tag}."),
                   Gear($"Zzz Corvo{tag}", null, "Uma ave."));
        var (_, player) = await Table();

        var page = await Page<CatalogItemSummaryDto>(player, $"items?q=corvo{tag}");

        Assert.Equal([$"Zzz Corvo{tag}", $"Aaa {tag}"], page.Items.Select(x => x.Name));
        Assert.Equal(2, page.Total);
    }

    [Fact]
    public async Task Spells_match_the_english_name_and_the_description_and_return_it()
    {
        var tag = Suffix().ToLowerInvariant();
        await Seed(Spell($"Chama {tag}", $"Flare{tag}", "Uma chama."),
                   Spell($"Véu {tag}", null, $"Você fica invisível{tag} por 1 hora."));
        var (_, player) = await Table();

        var english = await Page<CatalogSpellSummaryDto>(player, $"spells?q=flare{tag}");
        var description = await Page<CatalogSpellSummaryDto>(player, $"spells?q=invisivel{tag}");

        var hit = Assert.Single(english.Items);
        Assert.Equal($"Flare{tag}", hit.NameEn);
        Assert.Equal($"Véu {tag}", Assert.Single(description.Items).Name);
    }

    [Fact]
    public async Task A_term_shorter_than_three_characters_only_matches_names()
    {
        Assert.Equal(3, CatalogService.MinDescriptionTermLength);
        var tag = Suffix().ToLowerInvariant();
        await Seed(Gear($"Qx {tag}", null, "Nada."), Gear($"Outro {tag}", null, "qx no texto"));
        var (_, player) = await Table();

        var page = await Page<CatalogItemSummaryDto>(player, "items?q=qx&pageSize=50");

        Assert.Contains(page.Items, x => x.Name == $"Qx {tag}");
        Assert.DoesNotContain(page.Items, x => x.Name == $"Outro {tag}");
    }
}
