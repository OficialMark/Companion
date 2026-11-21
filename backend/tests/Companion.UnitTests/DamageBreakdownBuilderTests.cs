using Companion.Application.Contracts;
using Companion.Application.DiceRolls;
using Companion.Domain.Dice;

namespace Companion.UnitTests;

public sealed class DamageBreakdownBuilderTests
{
    [Fact]
    public void Assigns_server_rolled_subtotals_to_base_and_additional_damage_types()
    {
        var expression = DiceExpression.Parse("d8+d4+2");
        var result = Assert.Single(DiceRoller.Roll(expression, new ScriptedRandomizer(5, 3)));
        var request = new DiceDamageBreakdownRequest("cortante", 1,
            [new DiceDamageComponentRequest("Encantamento", "cold", 1)]);

        var detailed = Assert.Single(DamageBreakdownBuilder.Attach(expression, [result], request));

        Assert.Equal(10, detailed.Total);
        Assert.Equal("cortante", detailed.DamageBreakdown!.BaseType);
        Assert.Equal(7, detailed.DamageBreakdown.BaseSubtotal);
        var component = Assert.Single(detailed.DamageBreakdown.Components);
        Assert.Equal("Encantamento", component.Label);
        Assert.Equal("gélido", component.DamageType);
        Assert.Equal(3, component.Subtotal);
    }

    [Fact]
    public void Additional_dice_are_rolled_in_both_executions_and_the_breakdown_uses_the_selected_result()
    {
        var expression = DiceExpression.Parse("2#(d8 + 2d8 + (FOR) 3) maior");
        var results = DiceRoller.Roll(expression, new ScriptedRandomizer(1, 2, 3, 4, 5, 6));
        var request = new DiceDamageBreakdownRequest("cortante", 1,
            [new DiceDamageComponentRequest("Encantamento", "gélido", 1)]);

        var detailed = Assert.Single(DamageBreakdownBuilder.Attach(expression, results, request));

        Assert.Equal(18, detailed.Total);
        Assert.Equal(7, detailed.DamageBreakdown!.BaseSubtotal);
        Assert.Equal(11, Assert.Single(detailed.DamageBreakdown.Components).Subtotal);
    }

    [Fact]
    public void Rejects_a_breakdown_that_does_not_match_the_rolled_terms()
    {
        var expression = DiceExpression.Parse("d8+d4");
        var results = DiceRoller.Roll(expression, new ScriptedRandomizer(5, 3));
        var request = new DiceDamageBreakdownRequest("cortante", 1, []);

        Assert.Throws<ArgumentException>(() => DamageBreakdownBuilder.Attach(expression, results, request));
    }

    private sealed class ScriptedRandomizer(params int[] values) : IDiceRandomizer
    {
        private int _index;
        public int Roll(int faces) => values[_index++];
    }
}
