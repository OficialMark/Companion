using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Companion.Domain.Characters;

namespace Companion.UnitTests;

/// <summary>
/// Spec 011, SC-003: the rules are checked against the shared case file
/// backend/tests/Fixtures/character-rules.cases.json, so every expected number lives in one place.
/// </summary>
public sealed class CharacterRulesTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static TheoryData<string> CaseNames()
    {
        var data = new TheoryData<string>();
        foreach (var item in LoadCases()) data.Add(item["name"]!.GetValue<string>());
        return data;
    }

    [Fact]
    public void Case_file_has_at_least_30_cases()
    {
        Assert.True(LoadCases().Count >= 30, "SC-003 pede pelo menos 30 casos de cálculo.");
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Case_matches_expected_values(string name)
    {
        var testCase = LoadCases().Single(x => x["name"]!.GetValue<string>() == name);
        var stats = Run(testCase);
        var root = JsonSerializer.SerializeToNode(stats, Json)!;
        foreach (var (path, expected) in testCase["expect"]!.AsObject())
        {
            var actual = Navigate(root, path);
            Assert.True(JsonNode.DeepEquals(Normalize(expected), Normalize(actual)),
                $"[{name}] {path}: esperado {expected?.ToJsonString()}, obtido {actual?.ToJsonString()}");
        }
    }

    /// <summary>FR-007 (T075): the breakdown always adds up to the value shown, limits included.</summary>
    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Parts_always_add_up_to_the_final_value(string name)
    {
        var stats = Run(LoadCases().Single(x => x["name"]!.GetValue<string>() == name));
        static void Sums(string what, int value, IEnumerable<StatPart> parts) =>
            Assert.True(parts.Sum(x => x.Value) == value, $"{what}: partes somam {parts.Sum(x => x.Value)}, valor {value}");

        foreach (var (key, ability) in stats.Abilities) Sums($"atributo {key}", ability.Final, ability.Parts);
        foreach (var (key, save) in stats.SavingThrows) Sums($"salvaguarda {key}", save.Bonus, save.Parts);
        foreach (var (key, skill) in stats.Skills) Sums($"perícia {key}", skill.Bonus, skill.Parts);
        Sums("percepção passiva", stats.PassivePerception.Value, stats.PassivePerception.Parts);
        Sums("iniciativa", stats.Initiative.Value, stats.Initiative.Parts);
        Sums("CA", stats.ArmorClass.Value, stats.ArmorClass.Parts);
        Sums("PV máximos", stats.MaxHitPoints.Value, stats.MaxHitPoints.Parts);
        if (stats.SpellSaveDc is { } dc) Sums("CD", dc.Value, dc.Parts);
        if (stats.SpellAttackBonus is { } spellAttack) Sums("ataque de magia", spellAttack.Value, spellAttack.Parts);
        foreach (var weapon in stats.WeaponRolls) Sums($"ataque {weapon.Name}", weapon.AttackBonus, weapon.Attack);
        foreach (var (mode, speed) in stats.Speed)
            Assert.Equal(speed.Value, speed.Parts.Sum(x => x.Value));
        foreach (var (sense, range) in stats.Senses)
            Assert.Equal(range.Value, range.Parts.Sum(x => x.Value));
    }

    [Fact]
    public void Decimal_movement_parts_preserve_one_and_a_half_meter_increments()
    {
        var sheet = CharacterSheetData.Default with { BaseSpeed = 1.5m, Strength = 8 };
        var armor = new InventoryItemData("Armadura", ItemCategory.Armor, 1, null, null, null, true, true, 0, null,
            new ArmorData(16, ArmorType.Heavy, 13, false), null, []).Validated();

        var walk = CharacterRules.Compute(sheet, [new ItemEntry(Guid.NewGuid(), 0, armor)], []).Speed["walk"];

        Assert.Equal(0, walk.Value);
        Assert.Equal([1.5m, -3m, 1.5m], walk.Parts.Select(x => x.Value));
        Assert.Equal(walk.Value, walk.Parts.Sum(x => x.Value));
    }

    [Fact]
    public void Compute_never_changes_base_values_after_equipping_sequences()
    {
        var sheet = CharacterSheetData.Default with { Strength = 16, Dexterity = 14 };
        var belt = new InventoryItemData("Cinto", ItemCategory.Gear, 1, null, null, null, true, false, 0, null, null, null,
            [new ItemEffect(EffectType.Ability, "str", EffectOp.Add, 2)]).Validated();
        var withBelt = CharacterRules.Compute(sheet, [new ItemEntry(Guid.NewGuid(), 0, belt)], []);
        var without = CharacterRules.Compute(sheet, [new ItemEntry(Guid.NewGuid(), 0, belt with { Equipped = false })], []);

        Assert.Equal(18, withBelt.Abilities["str"].Final);
        Assert.Equal(16, without.Abilities["str"].Final);
        Assert.Equal(16, sheet.Strength);
    }

    [Fact]
    public void Item_names_become_valid_roll_labels()
    {
        Assert.Equal("Anel de Proteção", RollLabel.Sanitize("Anel de Proteção"));
        Assert.Equal("Espada Longa +1", RollLabel.Sanitize("Espada Longa +1"));
        Assert.Equal("Arco Longo do Ca", RollLabel.Sanitize("Arco Longo do Caçador (élfico)"));
        Assert.Equal("item", RollLabel.Sanitize("()#"));
    }

    private static CharacterStats Run(JsonNode testCase)
    {
        var sheetNode = JsonSerializer.SerializeToNode(CharacterSheetData.Default, Json)!.AsObject();
        if (testCase["sheet"] is JsonObject overrides)
            foreach (var (key, value) in overrides) sheetNode[key] = value?.DeepClone();
        var sheet = sheetNode.Deserialize<CharacterSheetData>(Json)!.Validated();

        var items = new List<ItemEntry>();
        if (testCase["items"] is JsonArray itemNodes)
        {
            for (var index = 0; index < itemNodes.Count; index++)
            {
                var node = itemNodes[index]!.AsObject();
                var id = node["id"]?.GetValue<string>() ?? $"{index}";
                var order = node["sortOrder"]?.GetValue<int>() ?? index;
                var data = node.Deserialize<InventoryItemData>(Json)!.Validated();
                items.Add(new ItemEntry(StableGuid(id), order, data));
            }
        }

        var spells = new List<SpellEntry>();
        if (testCase["spells"] is JsonArray spellNodes)
            for (var index = 0; index < spellNodes.Count; index++)
                spells.Add(new SpellEntry(StableGuid($"spell-{index}"), spellNodes[index]!.Deserialize<CharacterSpellData>(Json)!.Validated()));

        return CharacterRules.Compute(sheet, items, spells);
    }

    private static JsonNode? Navigate(JsonNode root, string path)
    {
        JsonNode? current = root;
        foreach (var segment in path.Split('.'))
        {
            current = current switch
            {
                JsonArray array when segment == "length" => JsonValue.Create(array.Count),
                JsonArray array when int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                    => index < array.Count ? array[index] : throw new Xunit.Sdk.XunitException($"{path}: índice {index} fora da lista"),
                JsonObject obj => obj.TryGetPropertyValue(segment, out var next)
                    ? next
                    : throw new Xunit.Sdk.XunitException($"{path}: '{segment}' não existe"),
                _ => throw new Xunit.Sdk.XunitException($"{path}: não dá para navegar em '{segment}'")
            };
        }

        return current;
    }

    private static JsonNode? Normalize(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<decimal>(out var number) ? JsonValue.Create(number) : node;

    private static Guid StableGuid(string text) => new(MD5.HashData(Encoding.UTF8.GetBytes(text)));

    private static List<JsonNode> LoadCases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "character-rules.cases.json");
        var root = JsonNode.Parse(File.ReadAllText(path))!;
        return root["cases"]!.AsArray().Select(x => x!).ToList();
    }
}
