using System.Text;

namespace Companion.Domain.Characters;

/// <summary>
/// Makes a text fit the label slot of a dice expression, "(ROTULO) N": letters (accents
/// allowed), ASCII digits, space, '+', '.' and apostrophe, at most 16 characters
/// (contracts/dice-expression.md). Item names become labels, so they go through here.
/// </summary>
public static class RollLabel
{
    public const int MaxLength = 16;

    public static string Sanitize(string? text, string fallback = "item")
    {
        var builder = new StringBuilder();
        var lastWasSpace = true;
        // NFC first: a name typed with combining accents (NFD) keeps its letters. Only ASCII digits pass,
        // the same set DiceExpression accepts in a label, so every output parses.
        foreach (var character in Nfc(text ?? string.Empty))
        {
            var allowed = char.IsLetter(character) || char.IsAsciiDigit(character) || character is '+' or '.' or '\'';
            if (allowed)
            {
                builder.Append(character);
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        var label = builder.ToString().Trim();
        if (label.Length > MaxLength) label = label[..MaxLength].TrimEnd();
        return label.Length == 0 ? fallback : label;
    }

    /// <summary>NFC, or the text as is when it holds invalid UTF-16 (a lone surrogate), which Normalize rejects.</summary>
    private static string Nfc(string text)
    {
        try { return text.Normalize(NormalizationForm.FormC); }
        catch (ArgumentException) { return text; }
    }
}
