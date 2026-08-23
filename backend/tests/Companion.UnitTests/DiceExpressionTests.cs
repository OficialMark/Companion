using Companion.Domain.Dice;

namespace Companion.UnitTests;

public sealed class DiceExpressionTests
{
    [Theory]
    [InlineData("d20", 1, 1, 0, "1d20")]
    [InlineData("2D6+3", 1, 1, 3, "2d6+3")]
    [InlineData(" 1d8-1 ", 1, 1, -1, "1d8-1")]
    [InlineData("3#3d5+4", 3, 1, 4, "3#3d5+4")]
    [InlineData("2d3+4+2d8+3", 1, 2, 7, "2d3+2d8+7")]
    public void Parse_normalizes_valid_expressions(
        string input, int repetitions, int termCount, int modifier, string normalized)
    {
        var expression = DiceExpression.Parse(input);
        Assert.Equal(repetitions, expression.Repetitions);
        Assert.Equal(termCount, expression.Terms.Count);
        Assert.Equal(modifier, expression.Modifier);
        Assert.Equal(normalized, expression.Normalized);
    }

    [Theory]
    [InlineData("0d6")]
    [InlineData("101d6")]
    [InlineData("1d1")]
    [InlineData("d20+10001")]
    [InlineData("21#d6")]
    [InlineData("20#6d6")]
    public void Parse_rejects_out_of_range_expressions(string input) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => DiceExpression.Parse(input));

    [Theory]
    [InlineData("20")]
    [InlineData("2x6")]
    [InlineData("d")]
    [InlineData("2##d6")]
    [InlineData("3#")]
    [InlineData("d20+")]
    public void Parse_rejects_invalid_syntax(string input) =>
        Assert.Throws<FormatException>(() => DiceExpression.Parse(input));
}
