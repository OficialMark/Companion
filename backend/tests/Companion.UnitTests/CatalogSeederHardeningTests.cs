using System.Reflection;
using System.Text;
using Companion.Application.Catalog;
using Companion.Application.Contracts;
using Companion.Domain.Catalog;
using Companion.Domain.Characters;
using Companion.Application.Creatures;
using Companion.Domain.Models;

namespace Companion.UnitTests;

/// <summary>
/// Spec 011, T076 (revisão de migração): uma linha ruim do seed é pulada e reportada, nunca derruba o
/// catálogo inteiro no SaveChanges; slugs são comparados como o índice único (sem maiúsculas nem
/// espaço no fim); a paginação do catálogo não estoura.
/// </summary>
public sealed class CatalogSeederHardeningTests
{
    private const string GoodItem = """{"slug":"rope","name":"Corda","category":"Gear","rarity":null}""";

    [Theory]
    [InlineData("nameEn", 121, "O nome em inglês deve ter até 120 caracteres.")]
    [InlineData("subcategory", 81, "A subcategoria deve ter até 80 caracteres.")]
    [InlineData("rarity", 31, "A raridade deve ter até 30 caracteres.")]
    [InlineData("cost", 41, "O custo deve ter até 40 caracteres.")]
    public void Item_text_longer_than_its_column_is_rejected(string field, int length, string message)
    {
        var fields = new CatalogItemFields(
            new InventoryItemData("Corda", ItemCategory.Gear, 1, null, null, null, false, false, 0, null, null, null, []),
            field == "nameEn" ? new string('n', length) : null,
            field == "subcategory" ? new string('s', length) : null,
            field == "rarity" ? new string('r', length) : null,
            false,
            field == "cost" ? new string('c', length) : null);
        var error = Assert.Throws<ArgumentException>(() => new CatalogItem(Guid.NewGuid(), "rope", fields));
        // Spec 013 (FR-008): the master sees this message too, so it names the field in Portuguese.
        Assert.Equal(message, error.Message);
        // At the limit it fits.
        Assert.Null(Record.Exception(() => new CatalogItem(Guid.NewGuid(), "rope", fields with
        {
            NameEn = fields.NameEn?[..^1], Subcategory = fields.Subcategory?[..^1], Rarity = fields.Rarity?[..^1], Cost = fields.Cost?[..^1]
        })));
    }

    [Fact]
    public void Spell_higher_levels_classes_and_slugs_are_checked_against_their_columns()
    {
        var spell = new CharacterSpellData("Raio", 1, null, null, null, null, null, false, false, null, SpellAttackKind.None, null, null, null, false);
        Assert.Throws<ArgumentException>(() => new CatalogSpell(Guid.NewGuid(), "raio", new CatalogSpellFields(spell, [], new string('h', 4001))));
        var classes = Enumerable.Range(0, 40).Select(x => $"classe{x:00}").ToArray(); // 40 * 12 chars of JSON > 400
        Assert.Throws<ArgumentException>(() => new CatalogSpell(Guid.NewGuid(), "raio", new CatalogSpellFields(spell, classes, null)));
        Assert.Throws<ArgumentException>(() => new CatalogSpell(Guid.NewGuid(), new string('s', 121), new CatalogSpellFields(spell, [], null)));
        Assert.Equal("raio", new CatalogSpell(Guid.NewGuid(), "  Raio ", new CatalogSpellFields(spell, ["Mago"], null)).Slug);
    }

    [Fact]
    public async Task One_bad_item_row_is_skipped_and_the_good_one_is_applied()
    {
        var db = FakeDb.Create();
        var seed = $$"""{"items":[{"slug":"bad","name":"Ruim","category":"Gear","rarity":"{{new string('r', 31)}}"},{{GoodItem}}]}""";
        var seeder = new CatalogSeeder(db, [new Source(CatalogKind.Items, seed)], TimeProvider.System);

        var result = (await seeder.SeedAsync()).Single();

        Assert.Equal(1, result.Upserted);
        Assert.Single(result.Skipped);
        Assert.StartsWith("bad: A raridade", result.Skipped[0]);
        Assert.Equal(["rope"], FakeDb.Of(db).Added.OfType<CatalogItem>().Select(x => x.Slug));
    }

    [Fact]
    public async Task Slugs_differing_only_in_case_or_spaces_are_one_row()
    {
        var db = FakeDb.Create();
        var seed = """{"items":[{"slug":"Rope ","name":"Corda","category":"Gear"},{"slug":"rope","name":"Outra Corda","category":"Gear"}]}""";
        var seeder = new CatalogSeeder(db, [new Source(CatalogKind.Items, seed)], TimeProvider.System);

        var result = (await seeder.SeedAsync()).Single();

        Assert.Equal(1, result.Upserted);
        Assert.Equal(["rope: slug repetido"], result.Skipped);
        var added = Assert.Single(FakeDb.Of(db).Added.OfType<CatalogItem>());
        Assert.Equal(("rope", "Corda"), (added.Slug, added.Name));
    }

    [Fact]
    public async Task One_bad_spell_row_is_skipped_and_the_good_one_is_applied()
    {
        var db = FakeDb.Create();
        var seed = $$"""{"spells":[{"slug":"ruim","name":"Ruim","level":1,"higherLevels":"{{new string('h', 4001)}}"},{"slug":"bom","name":"Bom","level":1}]}""";
        var seeder = new CatalogSeeder(db, [new Source(CatalogKind.Spells, seed)], TimeProvider.System);

        var result = (await seeder.SeedAsync()).Single();

        Assert.Equal(1, result.Upserted);
        Assert.StartsWith("ruim: O texto de círculos superiores", Assert.Single(result.Skipped));
        Assert.Equal(["bom"], FakeDb.Of(db).Added.OfType<CatalogSpell>().Select(x => x.Slug));
    }

    [Theory]
    [InlineData(int.MaxValue, 24, CatalogService.MaxPage, 24)]
    [InlineData(int.MinValue, 0, 1, 1)]
    [InlineData(3, 1000, 3, 50)]
    public void Paging_is_clamped_so_the_offset_never_overflows(int page, int size, int expectedPage, int expectedSize)
    {
        var (clampedPage, clampedSize) = CatalogService.Paging(page, size);
        Assert.Equal((expectedPage, expectedSize), (clampedPage, clampedSize));
        Assert.True((long)(clampedPage - 1) * clampedSize < int.MaxValue);
    }

    private sealed class Source(CatalogKind kind, string json) : ICatalogSeedSource
    {
        public string Key => $"test-{kind}";
        public CatalogKind Kind => kind;
        public byte[]? Read() => Encoding.UTF8.GetBytes(json);
    }

    /// <summary>Just enough of ICompanionDbContext for the seeder: empty tables, Add recorded, SaveChanges counts.</summary>
    public class FakeDb : DispatchProxy
    {
        public List<object> Added { get; } = [];

        public static ICompanionDbContext Create() => Create<ICompanionDbContext, FakeDb>();

        public static FakeDb Of(ICompanionDbContext db) => (FakeDb)(object)db;

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method!.Name)
            {
                case "get_CatalogItems": return Array.Empty<CatalogItem>().AsQueryable();
                case "get_CatalogSpells": return Array.Empty<CatalogSpell>().AsQueryable();
                case "get_SeedVersions": return Array.Empty<SeedVersion>().AsQueryable();
                case "Add": Added.Add(args![0]!); return null;
                case "SaveChangesAsync": return Task.FromResult(Added.Count);
                default: throw new NotSupportedException(method.Name);
            }
        }
    }
}
