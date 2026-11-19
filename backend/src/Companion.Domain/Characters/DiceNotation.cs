using System.Globalization;
using System.Text.RegularExpressions;

namespace Companion.Domain.Characters;

/// <summary>
/// Damage dice written on items and spells ("1d8", "8d6", "2d6+1d4"). Only the dice of the
/// player's dice pad are accepted, so the roll panel can always load them as clickable dice.
/// </summary>
public static partial class DiceNotation
{
    public static readonly IReadOnlyList<int> StandardFaces = [4, 6, 8, 10, 12, 20, 100];

    /// <summary>Parses "NdM" (N optional) into (count, faces) pairs; throws <see cref="ArgumentException"/> in PT-BR.</summary>
    public static IReadOnlyList<(int Count, int Faces)> Terms(string? value, string field)
    {
        var compact = (value ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        if (compact.Length == 0) throw new ArgumentException($"{field} é obrigatório.");
        var terms = new List<(int, int)>();
        foreach (var part in compact.Split('+'))
        {
            var match = TermPattern().Match(part);
            if (!match.Success) throw new ArgumentException($"{field} deve ser escrito como 1d8 ou 2d6+1d4.");
            var count = match.Groups["count"].Value.Length == 0
                ? 1
                : int.Parse(match.Groups["count"].Value, CultureInfo.InvariantCulture);
            var faces = int.Parse(match.Groups["faces"].Value, CultureInfo.InvariantCulture);
            if (count is < 1 or > 20) throw new ArgumentException($"{field}: use de 1 a 20 dados de cada tipo.");
            if (!StandardFaces.Contains(faces)) throw new ArgumentException($"{field}: use d4, d6, d8, d10, d12, d20 ou d100.");
            terms.Add((count, faces));
        }

        return terms;
    }

    /// <summary>One die kind only, e.g. a weapon's "1d8". Returns the normalized "NdM".</summary>
    public static string Single(string? value, string field)
    {
        var terms = Terms(value, field);
        if (terms.Count != 1) throw new ArgumentException($"{field} deve ter um único tipo de dado, como 1d8.");
        return Format(terms);
    }

    /// <summary>A sum of dice, e.g. a spell's "8d6" or "2d6+1d4". Returns the normalized form.</summary>
    public static string Sum(string? value, string field) => Format(Terms(value, field));

    public static string Format(IEnumerable<(int Count, int Faces)> terms) =>
        string.Join('+', terms.Select(x => $"{x.Count}d{x.Faces}"));

    [GeneratedRegex("^(?<count>\\d{0,3})d(?<faces>\\d{1,4})$", RegexOptions.CultureInvariant)]
    private static partial Regex TermPattern();
}
