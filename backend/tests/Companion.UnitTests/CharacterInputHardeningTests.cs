using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Companion.Domain.Characters;
using Companion.Domain.Dice;

namespace Companion.UnitTests;

/// <summary>
/// Spec 011, T071/T074/T075 (revisão crítica): o que chega por JSON é validado por inteiro. Enum fora
/// da lista (mandado como número), propriedade de arma fora da lista fechada e textos longos viram
/// <see cref="ArgumentException"/> com mensagem em PT (400), nunca 500 nem lixo gravado.
/// </summary>
public sealed class CharacterInputHardeningTests
{
    // Same options as the API and the JSON columns: string enums that also accept numbers.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static InventoryItemData Sword(Func<WeaponData, WeaponData>? change = null)
    {
        var weapon = new WeaponData("1d8", "cortante", "1d10", WeaponKind.Melee, WeaponClass.Martial, ["versatile"], null, null, null, "sap");
        return new InventoryItemData("Espada Longa", ItemCategory.Weapon, 1, null, null, null, false, true, 0,
            change is null ? weapon : change(weapon), null, null, []);
    }

    [Fact]
    public void Every_property_of_the_closed_list_is_accepted_and_normalized()
    {
        var item = Sword(w => w with { Properties = [.. WeaponData.KnownProperties.Select(x => $" {x.ToUpperInvariant()} ")] }).Validated();
        Assert.Equal(WeaponData.KnownProperties.Order(), item.Weapon!.Properties.Order());
    }

    [Theory]
    [InlineData("magia")]
    [InlineData("<script>")]
    public void Weapon_property_outside_the_closed_list_is_rejected(string property)
    {
        var error = Assert.Throws<ArgumentException>(() => Sword(w => w with { Properties = ["finesse", property] }).Validated());
        Assert.Contains("Propriedade de arma desconhecida", error.Message);
    }

    [Fact]
    public void A_huge_property_list_is_rejected_without_echoing_it()
    {
        var properties = Enumerable.Range(0, 1000).Select(x => $"p{x}{new string('x', 500)}").ToArray();
        var error = Assert.Throws<ArgumentException>(() => Sword(w => w with { Properties = properties }).Validated());
        Assert.True(error.Message.Length < 200, error.Message);
    }

    [Fact]
    public void Mastery_and_damage_type_have_30_characters_at_most()
    {
        Assert.Equal(new string('m', 30), Sword(w => w with { Mastery = new string('m', 30) }).Validated().Weapon!.Mastery);
        Assert.Throws<ArgumentException>(() => Sword(w => w with { Mastery = new string('m', 31) }).Validated());
        Assert.Throws<ArgumentException>(() => Sword(w => w with { DamageType = new string('d', 31) }).Validated());
        Assert.Null(Sword(w => w with { Mastery = "   " }).Validated().Weapon!.Mastery);
    }

    public static TheoryData<string, string> SheetEnums() => new()
    {
        { "spellcastingAbility", "9" },
        { "unarmoredAbility", "9" },
        { "savingThrowProficiencies", "[60]" },
        { "generalProficiencies", """{"armor":[7],"shields":false,"simpleWeapons":false,"martialWeapons":false}""" }
    };

    [Theory]
    [MemberData(nameof(SheetEnums))]
    public void Sheet_enum_outside_the_list_is_rejected(string field, string value)
    {
        var node = JsonSerializer.SerializeToNode(CharacterSheetData.Default, Json)!.AsObject();
        node[field] = JsonNode.Parse(value);
        var sheet = node.Deserialize<CharacterSheetData>(Json)!;
        Assert.Throws<ArgumentException>(() => sheet.Validated());
    }

    public static TheoryData<string> ItemJson() => new()
    {
        """{"name":"X","category":9,"quantity":1,"effects":[]}""",
        """{"name":"X","category":"Weapon","quantity":1,"weapon":{"damageDice":"1d8","kind":5,"class":"Simple","properties":[]},"effects":[]}""",
        """{"name":"X","category":"Weapon","quantity":1,"weapon":{"damageDice":"1d8","kind":"Melee","class":5,"properties":[]},"effects":[]}""",
        """{"name":"X","category":"Weapon","quantity":1,"weapon":{"damageDice":"1d8","kind":"Melee","class":"Simple","properties":[],"abilityOverride":42},"effects":[]}""",
        """{"name":"X","category":"Armor","quantity":1,"armor":{"baseAc":12,"type":99},"effects":[]}""",
        """{"name":"X","category":"Gear","quantity":1,"effects":[{"type":99,"op":"Add","value":1}]}""",
        """{"name":"X","category":"Gear","quantity":1,"effects":[{"type":"MaxHp","op":5,"value":2000000000}]}"""
    };

    [Theory]
    [MemberData(nameof(ItemJson))]
    public void Item_enum_outside_the_list_is_rejected(string json)
    {
        var item = JsonSerializer.Deserialize<InventoryItemData>(json, Json)!;
        Assert.Throws<ArgumentException>(() => item.Validated());
    }

    [Theory]
    [InlineData("""{"name":"Raio","level":1,"attackKind":9}""")]
    [InlineData("""{"name":"Raio","level":1,"attackKind":"Save","saveAbility":9}""")]
    public void Spell_enum_outside_the_list_is_rejected(string json)
    {
        var spell = JsonSerializer.Deserialize<CharacterSpellData>(json, Json)!;
        Assert.Throws<ArgumentException>(() => spell.Validated());
    }

    [Theory]
    [InlineData("kind", "9")]
    [InlineData("advantage", "9")]
    [InlineData("ability", "9")]
    [InlineData("savingThrow", "60")]
    public void Saved_roll_enum_outside_the_list_is_rejected(string field, string value)
    {
        var node = JsonNode.Parse("""{"name":"Teste","icon":"lucide:dices","kind":"Check","dice":{"d20":1},"extraModifiers":[],"advantage":"None","repetitions":1}""")!.AsObject();
        node[field] = JsonNode.Parse(value);
        var roll = node.Deserialize<SavedRollData>(Json)!;
        Assert.Throws<ArgumentException>(() => roll.Validated());
    }

    [Fact]
    public void Saved_roll_without_the_versatile_field_reads_as_one_handed()
    {
        // Rows saved before T074 have no "versatile" in the JSON column.
        var roll = JsonSerializer.Deserialize<SavedRollData>(
            """{"name":"Dano","icon":"lucide:sword","kind":"Damage","dice":{},"extraModifiers":[],"advantage":"None","repetitions":1,"inventoryItemId":"8b0e3c35-7b55-4a3d-9a51-0f9d2b1c6e11"}""", Json)!;
        Assert.False(roll.Validated().Versatile);
        Assert.Contains("\"versatile\":true", JsonSerializer.Serialize(roll with { Versatile = true }, Json));
    }

    [Fact]
    public void Versatile_only_for_weapon_damage()
    {
        var damage = new SavedRollData("Dano", "lucide:sword", RollKind.Damage, new Dictionary<string, int>(), null, null, null,
            Guid.NewGuid(), null, [], AdvantageMode.None, 1, Versatile: true);
        Assert.True(damage.Validated().Versatile);
        Assert.Throws<ArgumentException>(() => (damage with { Kind = RollKind.Attack }).Validated());
        Assert.Throws<ArgumentException>(() => (damage with { InventoryItemId = null }).Validated());
    }

    [Theory]
    [InlineData("Anel de Proteção", "Anel de Proteção")]
    [InlineData("Anel ٣", "Anel")]
    [InlineData("Espada ²", "Espada")]
    [InlineData("Arco Ⅻ", "Arco")]
    [InlineData("Cinto (FOR) +2", "Cinto FOR +2")]
    [InlineData("Lâmina d'Água 3", "Lâmina d'Água 3")]
    public void Labels_keep_letters_and_ascii_digits_only(string name, string expected)
    {
        Assert.Equal(expected, RollLabel.Sanitize(name));
    }

    [Fact]
    public void Decomposed_accents_are_kept_as_one_letter()
    {
        var nfd = "Anel de Proteção".Normalize(NormalizationForm.FormD);
        Assert.NotEqual("Anel de Proteção", nfd);
        Assert.Equal("Anel de Proteção", RollLabel.Sanitize(nfd));
    }

    public static TheoryData<string> TrickyNames() => new()
    {
        "Anel de Proteção".Normalize(NormalizationForm.FormD),
        "Anel ٣",
        "Escudo ۴ do Norte",
        "Arco Ⅻ",
        "Espada ²³",
        "Manto 𝟙",
        "\ud800 Anel",
        "Machado​+1",
        "Cota (de) Malha [3]",
        "á́́́́́́́́́́́́́́́b",
        "日本刀 ２",
        "   ",
        "...'''+++",
        "Ελληνικό ξίφος 7"
    };

    [Theory]
    [MemberData(nameof(TrickyNames))]
    public void Every_sanitized_label_parses_as_a_dice_constant_label(string name)
    {
        var label = RollLabel.Sanitize(name);
        var expression = DiceExpression.Parse($"d20 + ({label}) 1");
        Assert.Contains(expression.Constants, x => x.Label is not null && x.Value == 1);
    }
}
