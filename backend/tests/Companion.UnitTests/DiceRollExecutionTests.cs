using System.Text.Json;
using Companion.Application.Contracts;
using Companion.Application.DiceRolls;
using Companion.Domain.Dice;

namespace Companion.UnitTests;

/// <summary>Spec 011 (T008): execução de seleção e repetição com um randomizador determinístico.</summary>
public sealed class DiceRollExecutionTests
{
    [Fact]
    public void Advantage_rolls_every_term_twice_and_keeps_the_highest()
    {
        var dice = new ScriptedRandomizer(4, 2, 3, 15, 1, 1);
        var result = Assert.Single(DiceRoller.Roll(DiceExpression.Parse("2#(d20 + 2d6 + (FOR) 3) maior"), dice));

        Assert.Equal([20, 6, 6, 20, 6, 6], dice.Requests);
        Assert.NotNull(result.Executions);
        Assert.Equal(2, result.Executions.Count);
        Assert.Equal([4, 2, 3], result.Executions[0].Terms.SelectMany(x => x.Values));
        Assert.Equal(12, result.Executions[0].Total);
        Assert.Equal([15, 1, 1], result.Executions[1].Terms.SelectMany(x => x.Values));
        Assert.Equal(20, result.Executions[1].Total);
        Assert.All(result.Executions, x => Assert.Equal(3, x.Modifier));
        Assert.Equal("maior", result.Selection);
        Assert.Equal(1, result.SelectedExecution);
        Assert.Equal(20, result.Total);
        Assert.Equal(result.Executions[1].Terms, result.Terms);
        Assert.Equal(3, result.Modifier);
        Assert.Equal([new DiceModifierDto("FOR", 3)], result.Modifiers);
    }

    [Fact]
    public void Disadvantage_keeps_the_lowest()
    {
        var result = Assert.Single(DiceRoller.Roll(DiceExpression.Parse("2#(d20 - (FOR) 1) menor"), new ScriptedRandomizer(18, 7)));

        Assert.Equal("menor", result.Selection);
        Assert.Equal(1, result.SelectedExecution);
        Assert.Equal(6, result.Total);
        Assert.Equal(-1, result.Modifier);
        Assert.Equal([new DiceModifierDto("FOR", -1)], result.Modifiers);
    }

    [Theory]
    [InlineData("2#(d20) maior")]
    [InlineData("2#(d20) menor")]
    public void Tie_keeps_the_first_execution(string input)
    {
        var result = Assert.Single(DiceRoller.Roll(DiceExpression.Parse(input), new ScriptedRandomizer(9, 9)));

        Assert.Equal(0, result.SelectedExecution);
        Assert.Same(result.Executions![0].Terms, result.Terms);
    }

    [Fact]
    public void Repeated_selection_produces_one_result_with_two_executions_per_repetition()
    {
        var results = DiceRoller.Roll(DiceExpression.Parse("3#[2#(d20 + 5) maior]"),
            new ScriptedRandomizer(10, 3, 2, 19, 7, 7));

        Assert.Equal([1, 2, 3], results.Select(x => x.Sequence));
        Assert.All(results, x => Assert.Equal(2, x.Executions!.Count));
        Assert.Equal([15, 24, 12], results.Select(x => x.Total));
        Assert.Equal([0, 1, 0], results.Select(x => x.SelectedExecution!.Value));
    }

    [Fact]
    public void First_repetition_total_is_the_roll_total()
    {
        // DiceRollService grava results[0].Total em DiceRoll.Total (contrato, Execução passo 2).
        var results = DiceRoller.Roll(DiceExpression.Parse("2#[d20 + (prof) 2]"), new ScriptedRandomizer(5, 17));

        Assert.Equal(7, results[0].Total);
        Assert.Equal(19, results[1].Total);
        Assert.All(results, x => Assert.Null(x.Executions));
        Assert.All(results, x => Assert.Equal([new DiceModifierDto("prof", 2)], x.Modifiers));
    }

    [Fact]
    public void Legacy_expression_executes_once_per_repetition_without_new_fields()
    {
        var dice = new ScriptedRandomizer(1, 2, 3, 4, 5, 4);
        var results = DiceRoller.Roll(DiceExpression.Parse("3#d20-1d4+2"), dice);

        Assert.Equal(6, dice.Requests.Count);
        Assert.Equal([1 - 2 + 2, 3 - 4 + 2, 5 - 4 + 2], results.Select(x => x.Total));
        Assert.Equal("-1d4", results[0].Terms[1].Expression);
        Assert.Equal(-2, results[0].Terms[1].Subtotal);
        Assert.All(results, x =>
        {
            Assert.Null(x.Modifiers);
            Assert.Null(x.Selection);
            Assert.Null(x.Executions);
            Assert.Null(x.SelectedExecution);
        });
    }

    [Fact]
    public void Legacy_result_serializes_exactly_as_before()
    {
        var results = DiceRoller.Roll(DiceExpression.Parse("2#d20+5"), new ScriptedRandomizer(7, 11));

        // Formato gravado em DiceRoll.ValuesJson antes da spec 011.
        Assert.Equal(
            """[{"Sequence":1,"Terms":[{"Expression":"1d20","Values":[7],"Subtotal":7}],"Modifier":5,"Total":12},""" +
            """{"Sequence":2,"Terms":[{"Expression":"1d20","Values":[11],"Subtotal":11}],"Modifier":5,"Total":16}]""",
            JsonSerializer.Serialize(results));
        // Formato da API (JsonSerializerDefaults.Web).
        Assert.Equal(
            """{"sequence":1,"terms":[{"expression":"1d20","values":[7],"subtotal":7}],"modifier":5,"total":12}""",
            JsonSerializer.Serialize(results[0], new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void Selection_result_round_trips_through_the_stored_json()
    {
        var results = DiceRoller.Roll(DiceExpression.Parse("2#(d20 + (FOR) 3) maior"), new ScriptedRandomizer(4, 16));
        var json = JsonSerializer.Serialize(results);

        Assert.Contains("\"Executions\":[", json, StringComparison.Ordinal);
        Assert.Contains("\"Modifiers\":[{\"Label\":\"FOR\",\"Value\":3}]", json, StringComparison.Ordinal);
        var restored = Assert.Single(JsonSerializer.Deserialize<DiceRollResultDto[]>(json)!);
        Assert.Equal("maior", restored.Selection);
        Assert.Equal(1, restored.SelectedExecution);
        Assert.Equal([7, 19], restored.Executions!.Select(x => x.Total));
        Assert.Equal(19, restored.Total);
    }

    private sealed class ScriptedRandomizer(params int[] values) : IDiceRandomizer
    {
        private int _next;
        public List<int> Requests { get; } = [];

        public int Roll(int faces)
        {
            Requests.Add(faces);
            if (_next >= values.Length) throw new InvalidOperationException("O roteiro de dados acabou.");
            var value = values[_next++];
            Assert.InRange(value, 1, faces);
            return value;
        }
    }
}
