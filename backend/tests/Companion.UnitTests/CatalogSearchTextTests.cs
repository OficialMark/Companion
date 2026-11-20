using Companion.Domain.Catalog;
using Companion.Domain.Characters;

namespace Companion.UnitTests;

/// <summary>Spec 012, FR-003: the text the catalog search matches.</summary>
public sealed class CatalogSearchTextTests
{
    [Fact]
    public void Joins_name_english_name_and_description_without_html_accents_or_case()
    {
        var text = CatalogText.SearchText("Bola de Fogo", "Fireball",
            "<p>Uma <strong>explosão</strong> de chamas&nbsp;rugindo.</p>\n\n<p>Fim.</p>");

        Assert.Equal("bola de fogo | fireball | uma explosao de chamas rugindo. fim.", text);
    }

    [Fact]
    public void Missing_parts_are_skipped()
    {
        Assert.Equal("corda", CatalogText.SearchText("Corda", null, "  "));
    }

    [Fact]
    public void Spell_and_item_derive_search_columns_from_their_fields()
    {
        var spell = new CatalogSpell(Guid.NewGuid(), "bola-de-fogo", new CatalogSpellFields(
            new CharacterSpellData("Bola de Fogo", 3, "evocação", null, null, null, null, false, false, "Explosão.",
                SpellAttackKind.None, null, null, null, false), [], null, "Fireball"));
        var item = new CatalogItem(Guid.NewGuid(), "rope", new CatalogItemFields(
            new InventoryItemData("Corda de Cânhamo", ItemCategory.Gear, 1, null, "Quinze metros.", null, false, false, 0, null, null, null, []),
            "Hempen Rope", null, null, false, null));

        Assert.Equal("Fireball", spell.NameEn);
        Assert.Equal("bola de fogo fireball", spell.SearchKey);
        Assert.Equal("bola de fogo | fireball | explosao.", spell.SearchText);
        Assert.Equal("corda de canhamo | hempen rope | quinze metros.", item.SearchText);
    }
}
