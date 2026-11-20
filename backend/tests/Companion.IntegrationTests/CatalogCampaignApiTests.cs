using System.Net;
using System.Net.Http.Json;
using System.Text;
using Companion.Application.Catalog;
using Companion.Application.Characters;
using Companion.Application.Contracts;
using Companion.Domain.Catalog;
using Companion.Domain.Characters;
using Companion.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Companion.IntegrationTests;

/// <summary>
/// Spec 013, US1: the master creates and edits catalog items and spells for one campaign. Global rows stay
/// read-only; an edit is a campaign copy that replaces the original there only; the seeder never touches it.
/// </summary>
public sealed class CatalogCampaignApiTests(CompanionApiFactory factory) : CharacterTestBase(factory)
{
    private static string Catalog(SessionResponse session, string path) => $"/api/campaigns/{session.CampaignId}/catalog/{path}";

    private async Task<T> Read<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) Assert.Fail($"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private async Task<CatalogPageDto<T>> Page<T>(SessionResponse session, string query) =>
        await Read<CatalogPageDto<T>>(await Send(HttpMethod.Get, Catalog(session, query), session));

    /// <summary>A global spell and item of this test only (tag in the name), as the seeder would create them.</summary>
    private async Task<(Guid Spell, Guid Item, string Tag)> SeedGlobal()
    {
        var tag = Suffix();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        var spell = new CatalogSpell(Guid.NewGuid(), $"spell-bola-{tag}", new CatalogSpellFields(
            Spell($"Bola Flamejante {tag}", "8d6"), ["mago"], null, $"Flame Ball {tag}"));
        var item = new CatalogItem(Guid.NewGuid(), $"item-espada-{tag}", new CatalogItemFields(
            Sword($"Espada Rúnica {tag}", "1d8"), $"Rune Sword {tag}", "Arma marcial", null, false, "15 PO"));
        db.CatalogSpells.Add(spell);
        db.CatalogItems.Add(item);
        await db.SaveChangesAsync();
        return (spell.Id, item.Id, tag);
    }

    private static CharacterSpellData Spell(string name, string dice) =>
        new(name, 3, "evocação", "1 ação", "45 metros", "V, S, M", "instantânea", false, false, "Explode.",
            SpellAttackKind.Save, AbilityCode.Dex, dice, "fogo", false);

    private static InventoryItemData Sword(string name, string dice) =>
        new(name, ItemCategory.Weapon, 1, 1.5m, "Uma espada.", null, false, false, 0,
            new WeaponData(dice, "cortante", null, WeaponKind.Melee, WeaponClass.Martial, [], null, null, null, null), null, null, []);

    private static SaveCatalogSpellRequest SpellRequest(CharacterSpellData template) => new(template, ["mago", "feiticeiro"], "Mais 1d6 por nível.", null);

    private static SaveCatalogItemRequest ItemRequest(InventoryItemData template) => new(template, "Phoenix Plate", "Armadura pesada", "Raro", true, "5000 PO");

    [Fact]
    public async Task Master_creates_custom_item_and_spell_that_players_of_the_campaign_find_and_add()
    {
        var (master, player) = await Table();
        var tag = Suffix();
        var plate = new InventoryItemData($"Corpo Fênix {tag}", ItemCategory.Armor, 1, 32.5m, "Placas de fênix.", null, false, false, 1,
            null, new ArmorData(18, ArmorType.Heavy, 15, true), null, []);

        var item = await Read<CatalogItemDto>(await Send(HttpMethod.Post, Catalog(master, "items"), master, ItemRequest(plate)));
        var spell = await Read<CatalogSpellDto>(await Send(HttpMethod.Post, Catalog(master, "spells"), master,
            SpellRequest(Spell($"Chama da Mesa {tag}", "3d6"))));

        Assert.Equal(CatalogOrigin.Custom, item.Origin);
        Assert.Null(item.BaseId);
        Assert.Equal("Raro", item.Rarity);
        Assert.True(item.RequiresAttunement);
        Assert.Equal(CatalogOrigin.Custom, spell.Origin);
        Assert.Equal(["feiticeiro", "mago"], spell.Classes);

        // The player finds them by name, English name and description, with the origin badge.
        var byName = await Page<CatalogItemSummaryDto>(player, $"items?q=fenix%20{tag}");
        var byEnglish = await Page<CatalogItemSummaryDto>(player, $"items?q=phoenix&origin=Custom");
        Assert.Equal(CatalogOrigin.Custom, Assert.Single(byName.Items).Origin);
        Assert.Contains(byEnglish.Items, x => x.Id == item.Id);

        var sheet = await Ok(HttpMethod.Post, $"{Base(player)}/items/from-catalog", player, new AddFromCatalogRequest(item.Id, 1));
        var copied = Assert.Single(sheet.Items);
        Assert.Equal(item.Id, copied.CatalogItemId);
        Assert.Equal(18, copied.Data.Armor!.BaseAc);
        sheet = await Ok(HttpMethod.Post, $"{Base(player)}/spells/from-catalog", player, new AddSpellFromCatalogRequest(spell.Id));
        Assert.Equal("3d6", Assert.Single(sheet.Spells).Data.DamageDice);
    }

    [Fact]
    public async Task Editing_a_global_entry_replaces_it_in_this_campaign_only_and_deleting_restores_it()
    {
        var (global, _, tag) = await SeedGlobal();
        var (master, player) = await Table();
        var (_, otherPlayer) = await Table();

        var edited = await Read<CatalogSpellDto>(await Send(HttpMethod.Put, Catalog(master, $"spells/{global}"), master,
            SpellRequest(Spell($"Bola Flamejante {tag}", "10d6"))));
        Assert.Equal(CatalogOrigin.Edited, edited.Origin);
        Assert.Equal(global, edited.BaseId);
        Assert.NotEqual(global, edited.Id);

        // Saving the global id again updates the same copy (no second copy), and so does saving the copy.
        var again = await Read<CatalogSpellDto>(await Send(HttpMethod.Put, Catalog(master, $"spells/{global}"), master,
            SpellRequest(Spell($"Bola Flamejante {tag}", "11d6"))));
        Assert.Equal(edited.Id, again.Id);

        // This campaign lists one entry, the edited one; another campaign still sees the original.
        var mine = await Page<CatalogSpellSummaryDto>(player, $"spells?q=flamejante%20{tag}");
        var theirs = await Page<CatalogSpellSummaryDto>(otherPlayer, $"spells?q=flamejante%20{tag}");
        Assert.Equal((edited.Id, CatalogOrigin.Edited), Assert.Single(mine.Items) is var m ? (m.Id, m.Origin) : default);
        Assert.Equal((global, CatalogOrigin.Compendium), Assert.Single(theirs.Items) is var t ? (t.Id, t.Origin) : default);
        Assert.Single((await Page<CatalogSpellSummaryDto>(player, $"spells?q={tag}&origin=Edited")).Items);
        Assert.Empty((await Page<CatalogSpellSummaryDto>(player, $"spells?q={tag}&origin=Compendium")).Items);

        // The detail by the old global id resolves to the copy; adding by the old id copies the table's version.
        Assert.Equal("11d6", (await Read<CatalogSpellDto>(await Send(HttpMethod.Get, Catalog(player, $"spells/{global}"), player))).Template.DamageDice);
        var sheet = await Ok(HttpMethod.Post, $"{Base(player)}/spells/from-catalog", player, new AddSpellFromCatalogRequest(global));
        Assert.Equal("11d6", Assert.Single(sheet.Spells).Data.DamageDice);
        var other = await Ok(HttpMethod.Post, $"{Base(otherPlayer)}/spells/from-catalog", otherPlayer, new AddSpellFromCatalogRequest(global));
        Assert.Equal("8d6", Assert.Single(other.Spells).Data.DamageDice);

        // Deleting the copy brings the original back; the player's sheet keeps its copy.
        var restored = await Read<CatalogSpellDto>(await Send(HttpMethod.Delete, Catalog(master, $"spells/{edited.Id}"), master));
        Assert.Equal(global, restored.Id);
        Assert.Equal(CatalogOrigin.Compendium, restored.Origin);
        Assert.Equal(global, Assert.Single((await Page<CatalogSpellSummaryDto>(player, $"spells?q=flamejante%20{tag}")).Items).Id);
        Assert.Equal("11d6", Assert.Single((await Get(player)).Spells).Data.DamageDice);

        // Critic round 2 (D1): the copy is gone, yet its spell is still the original's; adding it again is a 409.
        var twice = await Send(HttpMethod.Post, $"{Base(player)}/spells/from-catalog", player, new AddSpellFromCatalogRequest(global));
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
        Assert.Equal(global, Assert.Single((await Get(player)).Spells).CatalogSpellId);
    }

    [Fact]
    public async Task A_spell_already_on_the_sheet_in_another_version_is_not_added_twice()
    {
        // Critic finding 1: the global spell, then the table's edit of it (the sheet is global per name).
        var (global, _, tag) = await SeedGlobal();
        var name = $"Vash {Suffix()}";
        var (master, player) = await Table(name);
        await Ok(HttpMethod.Post, $"{Base(player)}/spells/from-catalog", player, new AddSpellFromCatalogRequest(global));

        var edited = await Read<CatalogSpellDto>(await Send(HttpMethod.Put, Catalog(master, $"spells/{global}"), master,
            SpellRequest(Spell($"Bola Flamejante {tag}", "10d6"))));
        var again = await Send(HttpMethod.Post, $"{Base(player)}/spells/from-catalog", player, new AddSpellFromCatalogRequest(edited.Id));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains("outra versão", await again.Content.ReadAsStringAsync());

        // The same name in a campaign whose master edited it too: still the same spell.
        var (otherMaster, otherPlayer) = await Table(name);
        var otherEdit = await Read<CatalogSpellDto>(await Send(HttpMethod.Put, Catalog(otherMaster, $"spells/{global}"), otherMaster,
            SpellRequest(Spell($"Bola Flamejante {tag}", "12d6"))));
        Assert.Equal(HttpStatusCode.Conflict,
            (await Send(HttpMethod.Post, $"{Base(otherPlayer)}/spells/from-catalog", otherPlayer, new AddSpellFromCatalogRequest(otherEdit.Id))).StatusCode);
        Assert.Single((await Get(player)).Spells);
    }

    [Fact]
    public async Task Two_saves_of_the_same_global_entry_at_once_leave_one_copy()
    {
        var (global, _, tag) = await SeedGlobal();
        var (master, _) = await Table();
        var body = SpellRequest(Spell($"Bola Flamejante {tag}", "9d6"));

        var responses = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => Send(HttpMethod.Put, Catalog(master, $"spells/{global}"), master, body)));

        // Serialized saves update the copy (200); a save that lost the race on the unique index gets 409, never a 500.
        Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);
        using var scope = Services.CreateScope();
        Assert.Equal(1, scope.ServiceProvider.GetRequiredService<CompanionDbContext>().CatalogSpells
            .Count(x => x.CampaignId == master.CampaignId && x.BaseId == global));
    }

    [Fact]
    public async Task Racing_saves_and_deletes_of_a_custom_entry_never_answer_500()
    {
        // Critic round 2 (D3): two tabs of the master, one deleting while others save or delete the same entry.
        var (master, _) = await Table();
        var tag = Suffix();
        for (var round = 0; round < 3; round++)
        {
            var custom = await Read<CatalogSpellDto>(await Send(HttpMethod.Post, Catalog(master, "spells"), master,
                SpellRequest(Spell($"Disputada {tag} {round}", "2d6"))));
            var path = Catalog(master, $"spells/{custom.Id}");
            var responses = await Task.WhenAll(
                Send(HttpMethod.Delete, path, master),
                Send(HttpMethod.Delete, path, master),
                Send(HttpMethod.Put, path, master, SpellRequest(Spell($"Disputada {tag} {round}", "3d6"))),
                Send(HttpMethod.Put, path, master, SpellRequest(Spell($"Disputada {tag} {round}", "4d6"))));
            Assert.All(responses, r => Assert.Contains(r.StatusCode,
                new[] { HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound, HttpStatusCode.Conflict }));
        }
    }

    [Fact]
    public async Task A_table_item_with_a_bonus_and_no_rarity_is_never_offered_as_a_base()
    {
        // Critic finding 3: the base list must match what WithBaseItem accepts (no bonus).
        var (master, player) = await Table();
        var tag = Suffix();
        var bonus = Sword($"Espada Bônus {tag}", "1d8") with { MagicBonus = 1 };
        var plain = Sword($"Espada Lisa {tag}", "1d8");
        foreach (var template in new[] { bonus, plain })
            await Read<CatalogItemDto>(await Send(HttpMethod.Post, Catalog(master, "items"), master,
                new SaveCatalogItemRequest(template, null, null, null, false, null)));

        var bases = await Page<CatalogItemSummaryDto>(player, $"items?q={tag}&category=Weapon&mundane=true&pageSize=50");
        Assert.Equal([$"Espada Lisa {tag}"], bases.Items.Select(x => x.Name));
        Assert.Equal(1, bases.Total);
    }

    [Fact]
    public async Task Edited_item_keeps_one_row_and_custom_delete_removes_it()
    {
        var (_, global, tag) = await SeedGlobal();
        var (master, player) = await Table();

        var edited = await Read<CatalogItemDto>(await Send(HttpMethod.Put, Catalog(master, $"items/{global}"), master,
            new SaveCatalogItemRequest(Sword($"Espada Rúnica {tag}", "1d10"), $"Rune Sword {tag}", null, "Incomum", false, null)));
        Assert.Equal(CatalogOrigin.Edited, edited.Origin);
        Assert.Equal("1d10", edited.Template.Weapon!.DamageDice);
        Assert.Equal(edited.Id, Assert.Single((await Page<CatalogItemSummaryDto>(player, $"items?q=runica%20{tag}")).Items).Id);

        var custom = await Read<CatalogItemDto>(await Send(HttpMethod.Post, Catalog(master, "items"), master,
            new SaveCatalogItemRequest(Sword($"Lâmina Própria {tag}", "1d6"), null, null, null, false, null)));
        var deleted = await Send(HttpMethod.Delete, Catalog(master, $"items/{custom.Id}"), master);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty((await Page<CatalogItemSummaryDto>(player, $"items?q=propria%20{tag}")).Items);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(HttpMethod.Get, Catalog(player, $"items/{custom.Id}"), player)).StatusCode);
    }

    [Fact]
    public async Task Global_entries_cannot_be_deleted_and_players_cannot_write()
    {
        var (spell, item, tag) = await SeedGlobal();
        var (master, player) = await Table();

        var deleteGlobal = await Send(HttpMethod.Delete, Catalog(master, $"spells/{spell}"), master);
        Assert.Equal(HttpStatusCode.BadRequest, deleteGlobal.StatusCode);
        Assert.Contains("não podem ser excluídas", await deleteGlobal.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Forbidden,
            (await Send(HttpMethod.Post, Catalog(player, "spells"), player, SpellRequest(Spell($"Hack {tag}", "1d4")))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Send(HttpMethod.Put, Catalog(player, $"items/{item}"), player, ItemRequest(Sword("Hack", "1d4")))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Delete, Catalog(player, $"items/{item}"), player)).StatusCode);
    }

    [Fact]
    public async Task Invalid_forms_are_rejected_in_portuguese_and_nothing_is_saved()
    {
        var (master, player) = await Table();
        var tag = Suffix();

        var badDice = await Send(HttpMethod.Post, Catalog(master, "items"), master,
            ItemRequest(Sword($"Ruim {tag}", "dois dados")));
        var noName = await Send(HttpMethod.Post, Catalog(master, "spells"), master, SpellRequest(Spell("  ", "1d6")));
        var noTemplate = await Send(HttpMethod.Post, Catalog(master, "items"), master, new SaveCatalogItemRequest(null, null, null, null, false, null));

        Assert.Equal(HttpStatusCode.BadRequest, badDice.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noName.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noTemplate.StatusCode);
        Assert.Contains("nome da magia", await noName.Content.ReadAsStringAsync());
        Assert.Empty((await Page<CatalogItemSummaryDto>(player, $"items?q=ruim%20{tag}")).Items);
    }

    [Fact]
    public async Task Another_campaigns_entries_are_invisible_and_unreachable()
    {
        var (master, _) = await Table();
        var (otherMaster, otherPlayer) = await Table();
        var tag = Suffix();
        var mine = await Read<CatalogItemDto>(await Send(HttpMethod.Post, Catalog(master, "items"), master,
            new SaveCatalogItemRequest(Sword($"Segredo {tag}", "1d6"), null, null, null, false, null)));

        Assert.Empty((await Page<CatalogItemSummaryDto>(otherPlayer, $"items?q=segredo%20{tag}")).Items);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(HttpMethod.Get, Catalog(otherPlayer, $"items/{mine.Id}"), otherPlayer)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(HttpMethod.Post, $"{Base(otherPlayer)}/items/from-catalog", otherPlayer, new AddFromCatalogRequest(mine.Id, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(HttpMethod.Delete, Catalog(otherMaster, $"items/{mine.Id}"), otherMaster)).StatusCode);
    }

    [Fact]
    public async Task Generic_campaign_item_still_asks_for_a_base_item()
    {
        var (master, player) = await Table();
        var (_, swordId, tag) = await SeedGlobal();
        var generic = new InventoryItemData($"Arma da Mesa +2 {tag}", ItemCategory.Weapon, 1, null, "Bônus de +2.", null, false, false, 2,
            null, null, null, []);
        var created = await Read<CatalogItemDto>(await Send(HttpMethod.Post, Catalog(master, "items"), master,
            new SaveCatalogItemRequest(generic, null, null, "Raro", false, null)));
        Assert.True(created.RequiresBaseItem);

        var missingBase = await Send(HttpMethod.Post, $"{Base(player)}/items/from-catalog", player, new AddFromCatalogRequest(created.Id, 1));
        Assert.Equal(HttpStatusCode.BadRequest, missingBase.StatusCode);
        var sheet = await Ok(HttpMethod.Post, $"{Base(player)}/items/from-catalog", player, new AddFromCatalogRequest(created.Id, 1, swordId));
        var copied = Assert.Single(sheet.Items);
        Assert.Equal($"Arma da Mesa +2 {tag} (Espada Rúnica {tag})", copied.Data.Name);
        Assert.Equal(2, copied.Data.MagicBonus);
    }

    [Fact]
    public async Task Seeder_never_touches_campaign_entries_and_campaign_delete_removes_them()
    {
        var (master, player) = await Table();
        var (global, _, tag) = await SeedGlobal();
        var edited = await Read<CatalogSpellDto>(await Send(HttpMethod.Put, Catalog(master, $"spells/{global}"), master,
            SpellRequest(Spell($"Bola Flamejante {tag}", "10d6"))));
        var custom = await Read<CatalogSpellDto>(await Send(HttpMethod.Post, Catalog(master, "spells"), master,
            SpellRequest(Spell($"Própria {tag}", "2d6"))));

        // The partial seed below marks every other global spell removed; put them back afterwards so the
        // tests that run later in this shared database still see the real catalog.
        List<Guid> liveGlobals;
        using (var scope = Services.CreateScope())
            liveGlobals = scope.ServiceProvider.GetRequiredService<CompanionDbContext>().CatalogSpells
                .Where(x => x.CampaignId == null && !x.IsRemovedFromCatalog).Select(x => x.Id).ToList();
        try
        {
            using (var scope = Services.CreateScope())
            {
                // A seed run that mentions none of these rows: globals not in it are marked removed, campaign rows are untouched.
                var seed = "{\"spells\":[{\"slug\":\"spell-seed-only-" + tag + "\",\"name\":\"Só do Seed " + tag + "\",\"level\":1,\"school\":\"evocação\",\"classes\":[\"mago\"]}]}";
                var seeder = new CatalogSeeder(scope.ServiceProvider.GetRequiredService<ICompanionDbContext>(),
                    [new FakeSource($"test.spells.{tag}", CatalogKind.Spells, Encoding.UTF8.GetBytes(seed))], TimeProvider.System);
                await seeder.SeedAsync();
            }

            using (var scope = Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
                var rows = db.CatalogSpells.Where(x => x.Id == edited.Id || x.Id == custom.Id).ToList();
                Assert.Equal(2, rows.Count);
                Assert.All(rows, x => Assert.False(x.IsRemovedFromCatalog));
                Assert.Equal("10d6", rows.Single(x => x.Id == edited.Id).Template.DamageDice);
                Assert.True(db.CatalogSpells.Single(x => x.Id == global).IsRemovedFromCatalog);
            }

            // Even with its original removed from the seed, the edited copy keeps showing in its campaign, and
            // it says so (critic finding 4): deleting it now restores nothing, so the UI offers "Excluir".
            Assert.Contains((await Page<CatalogSpellSummaryDto>(player, $"spells?q={tag}")).Items, x => x.Id == edited.Id);
            Assert.False(edited.BaseRemoved);
            Assert.True((await Read<CatalogSpellDto>(await Send(HttpMethod.Get, Catalog(master, $"spells/{edited.Id}"), master))).BaseRemoved);
        }
        finally
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<CompanionDbContext>().CatalogSpells
                .Where(x => liveGlobals.Contains(x.Id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsRemovedFromCatalog, false));
        }

        var admin = new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/campaigns/{master.CampaignId}");
        admin.Headers.Add("X-Admin-Key", "integration-admin-secret");
        Assert.Equal(HttpStatusCode.NoContent, (await Client.SendAsync(admin)).StatusCode);
        using (var scope = Services.CreateScope())
            Assert.False(scope.ServiceProvider.GetRequiredService<CompanionDbContext>().CatalogSpells.Any(x => x.CampaignId == master.CampaignId));
    }

    private sealed class FakeSource(string key, CatalogKind kind, byte[]? bytes) : ICatalogSeedSource
    {
        public string Key => key;
        public CatalogKind Kind => kind;
        public byte[]? Read() => bytes;
    }
}
