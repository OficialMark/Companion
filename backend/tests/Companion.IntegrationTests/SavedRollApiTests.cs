using System.Net;
using Companion.Application.Characters;
using Companion.Domain.Characters;

namespace Companion.IntegrationTests;

/// <summary>Spec 011, US6: saved rolls keep references and are rebuilt from the current sheet.</summary>
public sealed class SavedRollApiTests(CompanionApiFactory factory) : CharacterTestBase(factory)
{
    private static SavedRollData Roll(Guid? itemId, string name = "Ataque com espada", string icon = "lucide:sword") =>
        new(name, icon, RollKind.Attack, new Dictionary<string, int> { ["d20"] = 1 }, null, null, null, itemId, null,
            [new RollModifier("Bênção", 2)], AdvantageMode.Advantage, 2);

    [Fact]
    public async Task Create_edit_duplicate_and_delete()
    {
        var (_, player) = await Table();
        var character = await AddItem(player, Weapon("Espada Longa"));
        var sword = character.Items.Single().Id;

        character = await Ok(HttpMethod.Post, $"{Base(player)}/rolls", player, new SaveRollRequest(Roll(sword)));
        var roll = character.SavedRolls.Single();
        Assert.Equal("lucide:sword", roll.Data.Icon);
        Assert.False(roll.Incomplete);

        character = await Ok(HttpMethod.Put, $"{Base(player)}/rolls/{roll.Id}", player, new SaveRollRequest(Roll(sword, "Golpe", "lucide:axe")));
        Assert.Equal("Golpe", character.SavedRolls.Single().Data.Name);

        character = await Ok(HttpMethod.Post, $"{Base(player)}/rolls/{roll.Id}/duplicate", player);
        Assert.Equal(["Golpe", "Golpe (cópia)"], character.SavedRolls.Select(x => x.Data.Name));

        character = await Ok(HttpMethod.Delete, $"{Base(player)}/rolls/{roll.Id}", player);
        Assert.Equal("Golpe (cópia)", character.SavedRolls.Single().Data.Name);
    }

    [Fact]
    public async Task Icon_outside_the_list_is_rejected()
    {
        var (_, player) = await Table();
        var response = await Send(HttpMethod.Post, $"{Base(player)}/rolls", player, new SaveRollRequest(Roll(null, icon: "lucide:car")));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Removing_the_weapon_marks_the_roll_incomplete_without_deleting_it()
    {
        var (_, player) = await Table();
        var character = await AddItem(player, Weapon("Espada Longa"));
        var sword = character.Items.Single().Id;
        await Ok(HttpMethod.Post, $"{Base(player)}/rolls", player, new SaveRollRequest(Roll(sword)));

        character = await Ok(HttpMethod.Delete, $"{Base(player)}/items/{sword}", player);

        Assert.True(character.SavedRolls.Single().Incomplete);
        Assert.Equal("Ataque com espada", character.SavedRolls.Single().Data.Name);
    }

    [Fact]
    public async Task Stats_used_by_a_saved_roll_follow_the_current_sheet()
    {
        var (_, player) = await Table();
        var character = await AddItem(player, Weapon("Espada Longa", magicBonus: 1));
        var sword = character.Items.Single().Id;
        character = await Ok(HttpMethod.Post, $"{Base(player)}/rolls", player, new SaveRollRequest(Roll(sword)));
        character = await SaveSheet(player, character, x => x with { Strength = 16, Level = 5 });
        Assert.Equal(7, character.Stats.WeaponRolls.Single().AttackBonus);

        character = await SaveSheet(player, character, x => x with { Strength = 18 });

        Assert.Equal(8, character.Stats.WeaponRolls.Single().AttackBonus);
        Assert.Equal(sword, character.SavedRolls.Single().Data.InventoryItemId);
    }
}
