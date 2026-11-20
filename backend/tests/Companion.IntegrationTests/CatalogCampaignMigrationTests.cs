using System.Text.Json;
using System.Text.Json.Serialization;
using Companion.Domain.Catalog;
using Companion.Domain.Characters;
using Companion.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Companion.IntegrationTests;

/// <summary>
/// Spec 013: the migration that scopes the catalog by campaign. The global catalog already in the database
/// (the compendium, the one thing that must survive this testing phase) stays global and readable; the new
/// indexes are the ones the queries need; rolling back removes the campaign entries instead of turning
/// them into global rows. Own database on the Testcontainers server, as in MigrationBackfillTests.
/// </summary>
[Collection(CompanionCollection.Name)]
public sealed class CatalogCampaignMigrationTests(CompanionApiFactory factory)
{
    private const string MigrationBefore = "20261120084627_AddCatalogSearchText";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public async Task Existing_global_catalog_survives_up_and_rolling_back_drops_only_campaign_entries()
    {
        var connection = new SqlConnectionStringBuilder(factory.ConnectionString) { InitialCatalog = $"Catalog013_{Guid.NewGuid():N}" }.ConnectionString;
        var options = new DbContextOptionsBuilder<CompanionDbContext>().UseSqlServer(connection).Options;
        var globalId = Guid.NewGuid();
        var template = JsonSerializer.Serialize(new CharacterSpellData("Bola de Fogo", 3, "evocação", "1 ação", null, null, null, false, false,
            "Explode.", SpellAttackKind.None, null, null, null, false), Json);
        try
        {
            await using (var db = new CompanionDbContext(options))
            {
                await db.GetService<IMigrator>().MigrateAsync(MigrationBefore);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO [CatalogSpells] ([Id], [Slug], [Name], [NameEn], [Level], [School], [Ritual], [Concentration], [Classes], [HigherLevels], [Template], [SearchKey], [SearchText], [IsRemovedFromCatalog])
                    VALUES ({globalId}, {"bola-de-fogo"}, {"Bola de Fogo"}, {"Fireball"}, 3, {"evocação"}, 0, 0, {"[\"mago\"]"}, {null as string}, {template}, {"bola de fogo"}, {"bola de fogo | fireball"}, 0);
                    """);
            }

            await using (var db = new CompanionDbContext(options))
            {
                await db.Database.MigrateAsync();
                var global = await db.CatalogSpells.SingleAsync(x => x.Id == globalId);
                Assert.Null(global.CampaignId);
                Assert.Equal(CatalogOrigin.Compendium, global.Origin);
                Assert.Equal("Bola de Fogo", global.Template.Name);

                // Critic round 2: an unfiltered CampaignId index for "WHERE CampaignId = @c"; the slug is unique among globals only.
                foreach (var table in new[] { "CatalogItems", "CatalogSpells" })
                {
                    var campaignIndex = await db.Database.SqlQuery<bool>($"""
                        SELECT CAST(i.has_filter AS bit) AS [Value] FROM sys.indexes i
                        WHERE i.object_id = OBJECT_ID({table}) AND i.name = {$"IX_{table}_CampaignId"}
                        """).SingleAsync();
                    Assert.False(campaignIndex);
                    var slugFilter = await db.Database.SqlQuery<string>($"""
                        SELECT i.filter_definition AS [Value] FROM sys.indexes i
                        WHERE i.object_id = OBJECT_ID({table}) AND i.name = {$"IX_{table}_Slug"} AND i.is_unique = 1
                        """).SingleAsync();
                    Assert.Equal("([CampaignId] IS NULL)", slugFilter);
                }

                // A campaign with an edited copy of the global spell.
                var campaign = new Companion.Domain.Models.Campaign(Guid.NewGuid(), "Mesa", DateTimeOffset.UtcNow);
                db.Add(campaign);
                db.Add(CatalogSpell.ForCampaign(Guid.NewGuid(), campaign.Id, globalId, new CatalogSpellFields(global.Template, ["mago"], null)));
                await db.SaveChangesAsync();
            }

            await using (var db = new CompanionDbContext(options))
            {
                await db.GetService<IMigrator>().MigrateAsync(MigrationBefore);
                var slugs = await db.Database.SqlQuery<string>($"SELECT [Slug] AS [Value] FROM [CatalogSpells]").ToListAsync();
                Assert.Equal(["bola-de-fogo"], slugs);
            }

            await using (var db = new CompanionDbContext(options))
            {
                await db.Database.MigrateAsync();
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            }
        }
        finally
        {
            await using var db = new CompanionDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
