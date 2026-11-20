using Companion.Domain.Catalog;
using Companion.Domain.Characters;

namespace Companion.UnitTests;

/// <summary>Spec 013, US1: origin of a catalog row and the shape of a campaign entry.</summary>
public sealed class CatalogCampaignTests
{
    private static CatalogSpellFields SpellFields(string name = "Bola de Fogo") => new(
        new CharacterSpellData(name, 3, "evocação", "1 ação", null, null, null, false, false, null, SpellAttackKind.None, null, null, null, false),
        ["Mago", "mago", " Feiticeiro "], null);

    private static CatalogItemFields ItemFields() => new(
        new InventoryItemData("Arma +1", ItemCategory.Weapon, 1, null, null, null, true, true, 1, null, null, null, []),
        null, null, "Incomum", false, null);

    [Fact]
    public void Global_custom_and_edited_rows_report_their_origin()
    {
        var global = new CatalogSpell(Guid.NewGuid(), "bola-de-fogo", SpellFields());
        var campaign = Guid.NewGuid();
        var custom = CatalogSpell.ForCampaign(Guid.NewGuid(), campaign, null, SpellFields("Minha"));
        var edited = CatalogSpell.ForCampaign(Guid.NewGuid(), campaign, global.Id, SpellFields());

        Assert.Equal(CatalogOrigin.Compendium, global.Origin);
        Assert.Null(global.CampaignId);
        Assert.Equal(CatalogOrigin.Custom, custom.Origin);
        Assert.Equal(campaign, custom.CampaignId);
        Assert.Equal(CatalogOrigin.Edited, edited.Origin);
        Assert.Equal(global.Id, edited.BaseId);
    }

    [Fact]
    public void Campaign_rows_get_a_generated_slug_and_the_same_validation_as_the_seed()
    {
        var id = Guid.NewGuid();
        var spell = CatalogSpell.ForCampaign(id, Guid.NewGuid(), null, SpellFields());
        Assert.Equal($"campanha-{id:N}", spell.Slug);
        Assert.Equal(["feiticeiro", "mago"], spell.Classes);
        Assert.Throws<ArgumentException>(() => CatalogSpell.ForCampaign(Guid.NewGuid(), Guid.NewGuid(), null, SpellFields("  ")));
    }

    [Fact]
    public void A_campaign_generic_weapon_keeps_asking_for_a_base_item_and_never_starts_equipped()
    {
        var item = CatalogItem.ForCampaign(Guid.NewGuid(), Guid.NewGuid(), null, ItemFields());
        Assert.True(item.RequiresBaseItem);
        Assert.Null(item.Template.Weapon);
        Assert.False(item.Template.Equipped);
        Assert.False(item.Template.Proficient);
        Assert.Equal(CatalogOrigin.Custom, item.Origin);
    }
}
