using System.Globalization;
using System.Text.RegularExpressions;

namespace Companion.Domain.Dice;

public sealed record DiceTerm(int Count, int Faces, int Sign)
{
    public string Normalized => $"{(Sign < 0 ? "-" : string.Empty)}{Count}d{Faces}";
}

public sealed partial record DiceExpression(int Repetitions, IReadOnlyList<DiceTerm> Terms, int Modifier)
{
    public string Normalized
    {
        get
        {
            var terms = string.Concat(Terms.Select((term, index) =>
                $"{(term.Sign > 0 && index > 0 ? "+" : string.Empty)}{term.Normalized}"));
            var modifier = Modifier > 0
                ? $"+{Modifier}"
                : Modifier < 0 ? Modifier.ToString(CultureInfo.InvariantCulture) : string.Empty;
            return $"{(Repetitions > 1 ? $"{Repetitions}#" : string.Empty)}{terms}{modifier}";
        }
    }

    public static DiceExpression Parse(string input)
    {
        var compact = WhitespacePattern().Replace(input?.Trim() ?? string.Empty, string.Empty);
        var repeatMatch = RepeatPattern().Match(compact);
        if (!repeatMatch.Success)
        {
            throw InvalidFormat();
        }

        var repetitions = string.IsNullOrEmpty(repeatMatch.Groups["repeat"].Value)
            ? 1
            : ParsePositive(repeatMatch.Groups["repeat"].Value);
        var body = repeatMatch.Groups["body"].Value;
        var tokens = TokenPattern().Matches(body).Select(x => x.Value).ToArray();
        if (tokens.Length == 0 || string.Concat(tokens) != body)
        {
            throw InvalidFormat();
        }

        var terms = new List<DiceTerm>();
        long modifier = 0;
        foreach (var token in tokens)
        {
            var diceMatch = DiceTermPattern().Match(token);
            if (diceMatch.Success)
            {
                var count = string.IsNullOrEmpty(diceMatch.Groups["count"].Value)
                    ? 1
                    : ParsePositive(diceMatch.Groups["count"].Value);
                var faces = ParsePositive(diceMatch.Groups["faces"].Value);
                var sign = diceMatch.Groups["sign"].Value == "-" ? -1 : 1;
                terms.Add(new DiceTerm(count, faces, sign));
                continue;
            }

            if (!IntegerPattern().IsMatch(token) ||
                !long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
            {
                throw InvalidFormat();
            }

            modifier = checked(modifier + value);
        }

        var dicePerRepetition = terms.Sum(x => (long)x.Count);
        if (terms.Count == 0)
        {
            throw InvalidFormat();
        }

        if (repetitions is < 1 or > 20 ||
            terms.Any(x => x.Count is < 1 or > 100 || x.Faces is < 2 or > 1000) ||
            dicePerRepetition * repetitions > 100 || modifier is < -10000 or > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(input),
                "Use no máximo 20 repetições, 100 dados no total, faces entre 2 e 1000 e modificador entre -10000 e 10000.");
        }

        return new DiceExpression(repetitions, terms, (int)modifier);
    }

    private static int ParsePositive(string value)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "A expressão excede os limites permitidos.");
        }

        return parsed;
    }

    private static FormatException InvalidFormat() =>
        new("Use uma expressão como d20, 2d6+3, 3#3d5+4 ou 2d3+2d8+7.");

    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();

    [GeneratedRegex("^(?:(?<repeat>\\d+)#)?(?<body>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RepeatPattern();

    [GeneratedRegex("[+-]?[^+-]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();

    [GeneratedRegex("^(?<sign>[+-]?)(?<count>\\d*)d(?<faces>\\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DiceTermPattern();

    [GeneratedRegex("^[+-]?\\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex IntegerPattern();
}
