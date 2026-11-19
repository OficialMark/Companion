using System.Net;
using System.Net.Http.Json;
using System.Text;
using Companion.Application.Catalog;
using Companion.Application.Characters;
using Companion.Application.Contracts;
using Companion.Domain.Catalog;
using Companion.Domain.Characters;
using Companion.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Companion.IntegrationTests;

/// <summary>Spec 011, US7: catalogs seeded at startup, search, copies into the sheet, base items, missing sources.</summary>
public sealed class CatalogApiTests(CompanionApiFactory factory) : CharacterTestBase(factory)
{
    private async Task<CatalogPageDto<T>> Page<T>(SessionResponse session, string query)
    {
        var response = await Send(HttpMethod.Get, $"/api/campaigns/{session.CampaignId}/catalog/{query}", session);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CatalogPageDto<T>>(JsonOptions))!;
    }

    private async Task<(Guid Sword, Guid Generic, string Tag)> SeedItems()
    {
        var tag = Suffix();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        var sword = new CatalogItem(Guid.NewGuid(), $"longsword-{tag}", new CatalogItemFields(
            new InventoryItemData($"Espada Longa {tag}", ItemCategory.Weapon, 1, 1.5m, "Uma espada.", null, false, false, 0,
                new WeaponData("1d8", "cortante", "1d10", WeaponKind.Melee, WeaponClass.Martial, ["versatile"], null, null, null, "sap"),
                null, null, []), "Longsword", "Arma marcial corpo a corpo", null, false, "15 PO"));
        var generic = new CatalogItem(Guid.NewGuid(), $"weapon-1-{tag}", new CatalogItemFields(
            new InventoryItemData($"Arma Mágica {tag}", ItemCategory.Weapon, 1, null, "Bônus de +1.", null, false, false, 1, null, null, null, []),
            "Weapon +1", "Arma mágica", "Incomum", false, null));
        db.CatalogItems.AddRange(sword, generic);
        await db.SaveChangesAsync();
        return (sword.Id, generic.Id, tag);
    }

    [Fact]
    public async Task Spell_catalog_is_seeded_at_startup_and_searchable_without_accents()
    {
        var (_, player) = await Table();

        var all = await Page<CatalogSpellSummaryDto>(player, "spells?pageSize=1");
        var fireball = await Page<CatalogSpellSummaryDto>(player, "spells?q=bola%20de%20fogo");
        var level3Mage = await Page<CatalogSpellSummaryDto>(player, "spells?level=3&class=mago&pageSize=50");
        var accentless = await Page<CatalogSpellSummaryDto>(player, "spells?q=evocacao");

        Assert.Equal(361, all.Total);
        Assert.Null(all.Attribution);
        Assert.Contains(fireball.Items, x => x.Name == "Bola de Fogo" && x.Level == 3);
        Assert.All(level3Mage.Items, x => Assert.Contains("mago", x.Classes));
        Assert.True(level3Mage.Total > 10);
        Assert.Equal(0, accentless.Total); // the name is searched, not the school
    }

    [Fact]
    public async Task Adding_a_catalog_spell_copies_it_once()
    {
        var (_, player) = await Table();
        var id = (await Page<CatalogSpellSummaryDto>(player, "spells?q=bola%20de%20fogo")).Items.Single(x => x.Name == "Bola de Fogo").Id;

        var character = await Ok(HttpMethod.Post, $"{Base(player)}/spells/from-catalog", player, new AddSpellFromCatalogRequest(id));
        var again = await Send(HttpMethod.Post, $"{Base(player)}/spells/from-catalog", player, new AddSpellFromCatalogRequest(id));

        var spell = character.Spells.Single();
        Assert.Equal(id, spell.CatalogSpellId);
        Assert.Equal("8d6", spell.Data.DamageDice);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Item_search_ignores_accents_and_adding_creates_an_independent_copy()
    {
        var (_, player) = await Table();
        var (swordId, _, tag) = await SeedItems();

        var found = await Page<CatalogItemSummaryDto>(player, $"items?q=espada%20longa%20{tag}&category=Weapon");
        Assert.Equal(CatalogService.ItemAttribution, found.Attribution);
        Assert.Equal(swordId, found.Items.Single().Id);

        var sheet = await SaveSheet(player, await Get(player), x => x with
        {
            GeneralProficiencies = new GeneralProficiencies([], false, true, true, null)
        });
        var character = await Ok(HttpMethod.Post, $"{Base(player)}/items/from-catalog", player, new AddFromCatalogRequest(swordId, 1));
        var copy = character.Items.Single();
        Assert.True(copy.Data.Proficient);
        Assert.Equal("1d8", copy.Data.Weapon!.DamageDice);
        Assert.Equal(swordId, copy.CatalogItemId);

        await Ok(HttpMethod.Put, $"{Base(player)}/items/{copy.Id}", player,
            new SaveItemRequest(copy.Data with { Name = "Minha Espada" }, copy.Version));
        var catalogItem = await Send(HttpMethod.Get, $"/api/campaigns/{player.CampaignId}/catalog/items/{swordId}", player);
        Assert.Contains($"Espada Longa {tag}", await catalogItem.Content.ReadAsStringAsync());
        Assert.NotNull(sheet);
    }

    [Fact]
    public async Task Generic_magic_weapon_needs_a_mundane_base_and_takes_its_block()
    {
        var (_, player) = await Table();
        var (swordId, genericId, tag) = await SeedItems();

        var withoutBase = await Send(HttpMethod.Post, $"{Base(player)}/items/from-catalog", player, new AddFromCatalogRequest(genericId, 1));
        Assert.Equal(HttpStatusCode.BadRequest, withoutBase.StatusCode);
        var magicBase = await Send(HttpMethod.Post, $"{Base(player)}/items/from-catalog", player, new AddFromCatalogRequest(genericId, 1, genericId));
        Assert.Equal(HttpStatusCode.BadRequest, magicBase.StatusCode);

        var mundane = await Page<CatalogItemSummaryDto>(player, $"items?category=Weapon&mundane=true&q={tag}");
        Assert.Equal([swordId], mundane.Items.Select(x => x.Id));

        var character = await Ok(HttpMethod.Post, $"{Base(player)}/items/from-catalog", player, new AddFromCatalogRequest(genericId, 1, swordId));
        var item = character.Items.Single();
        Assert.Equal($"Arma Mágica {tag} (Espada Longa {tag})", item.Data.Name);
        Assert.Equal(1, item.Data.MagicBonus);
        Assert.Equal("1d8", item.Data.Weapon!.DamageDice);
    }

    [Fact]
    public async Task Missing_or_broken_seeds_never_break_the_seeder_and_custom_items_survive()
    {
        var (_, player) = await Table();
        await AddItem(player, Gear("Amuleto da avó", new ItemEffect(EffectType.Ability, "sab", EffectOp.Add, 1)));

        using var scope = Services.CreateScope();
        var rope = $"rope-{Suffix()}";
        var partial = "{\"items\":[{\"slug\":\"bad\",\"name\":\"\",\"category\":\"Weapon\"},{\"slug\":\"" + rope +
                      "\",\"name\":\"Corda\",\"category\":\"Gear\",\"magicBonus\":0}]}";
        var seeder = new CatalogSeeder(scope.ServiceProvider.GetRequiredService<ICompanionDbContext>(),
            [new FakeSource("test.missing", CatalogKind.Items, null),
             new FakeSource($"test.partial.{Suffix()}", CatalogKind.Items, Encoding.UTF8.GetBytes(partial))],
            TimeProvider.System);

        var results = await seeder.SeedAsync();

        Assert.False(results[0].Applied);
        Assert.Equal(1, results[1].Upserted);
        Assert.Single(results[1].Skipped);
        Assert.Equal("Amuleto da avó", (await Get(player)).Items.Single().Data.Name);
    }

    private sealed class FakeSource(string key, CatalogKind kind, byte[]? bytes) : ICatalogSeedSource
    {
        public string Key => key;
        public CatalogKind Kind => kind;
        public byte[]? Read() => bytes;
    }
}
