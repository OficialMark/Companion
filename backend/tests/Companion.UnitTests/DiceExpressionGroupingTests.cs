using System.Text.Json;
using Companion.Domain.Dice;

namespace Companion.UnitTests;

/// <summary>
/// Spec 011 (T006): guiado por <c>backend/tests/Fixtures/dice-expressions.cases.json</c>, o mesmo arquivo que o
/// gerador do frontend lê (<c>frontend/tests/roll-expression.spec.ts</c>).
/// </summary>
public sealed class DiceExpressionGroupingTests
{
    private static readonly IReadOnlyList<DiceCase> Cases = LoadCases();

    public static TheoryData<string> ValidInputs() => [.. Cases.Where(x => x.Error is null).Select(x => x.Input)];

    public static TheoryData<string> InvalidInputs() => [.. Cases.Where(x => x.Error is not null).Select(x => x.Input)];

    [Fact]
    public void Cases_file_covers_the_contract()
    {
        Assert.True(Cases.Count >= 40, $"O arquivo de casos tem {Cases.Count} casos; o mínimo é 40.");
        Assert.All(Cases, x => Assert.True((x.Canonical is null) != (x.Error is null), $"'{x.Input}' precisa de canonical ou error."));
        Assert.All(Cases.Where(x => x.RoundTrip), x => Assert.NotNull(x.Build));
        Assert.Equal(Cases.Count, Cases.Select(x => x.Input).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(Cases, x => x.Canonical?.Length == DiceExpression.MaxLength);
    }

    [Theory]
    [MemberData(nameof(ValidInputs))]
    public void Valid_input_normalizes_to_the_canonical_form(string input)
    {
        var expected = Cases.Single(x => x.Input == input).Canonical;
        Assert.Equal(expected, DiceExpression.Parse(input).Normalized);
    }

    [Theory]
    [MemberData(nameof(ValidInputs))]
    public void Canonical_form_is_a_fixed_point(string input)
    {
        var canonical = DiceExpression.Parse(input).Normalized;
        Assert.Equal(canonical, DiceExpression.Parse(canonical).Normalized);
    }

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public void Invalid_input_is_rejected_with_the_expected_message(string input)
    {
        var expected = Cases.Single(x => x.Input == input).Error!;
        var exception = Assert.ThrowsAny<Exception>(() => DiceExpression.Parse(input));
        Assert.True(exception is FormatException or ArgumentOutOfRangeException,
            $"'{input}' lançou {exception.GetType().Name}; o serviço só traduz FormatException e ArgumentOutOfRangeException.");
        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Selection_and_repetition_are_modelled_for_execution()
    {
        var expression = DiceExpression.Parse("3#[2#(d20 + 2d6 + (FOR) 3 - (Anel) 1 + 2) MENOR]");

        Assert.Equal(3, expression.Repetitions);
        Assert.Equal(DiceSelection.Lowest, expression.Selection);
        Assert.Equal(2, expression.ExecutionsPerRepetition);
        Assert.True(expression.Bracketed);
        Assert.True(expression.UsesExtendedSyntax);
        Assert.Equal([new DiceTerm(1, 20, 1), new DiceTerm(2, 6, 1)], expression.Terms);
        Assert.Equal([new DiceConstant("FOR", 3), new DiceConstant("Anel", -1), new DiceConstant(null, 2)], expression.Constants);
        Assert.Equal(4, expression.Modifier);
    }

    [Fact]
    public void Legacy_expression_keeps_unlabeled_constants_and_no_extended_syntax()
    {
        var expression = DiceExpression.Parse("2d3+4+2d8+3");

        Assert.False(expression.UsesExtendedSyntax);
        Assert.Equal(DiceSelection.None, expression.Selection);
        Assert.Equal([new DiceConstant(null, 4), new DiceConstant(null, 3)], expression.Constants);
    }

    private static List<DiceCase> LoadCases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "dice-expressions.cases.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("cases").EnumerateArray().Select(x => new DiceCase(
            x.GetProperty("input").GetString()!,
            x.GetProperty("canonical").GetString(),
            x.GetProperty("error").GetString(),
            x.GetProperty("roundTrip").GetBoolean(),
            x.TryGetProperty("build", out var build) ? build.Clone() : null)).ToList();
    }

    private sealed record DiceCase(string Input, string? Canonical, string? Error, bool RoundTrip, JsonElement? Build);
}
