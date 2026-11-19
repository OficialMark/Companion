using System.Text;

namespace Companion.Domain.Characters;

/// <summary>
/// Makes a text fit the label slot of a dice expression, "(ROTULO) N": letters (accents
/// allowed), digits, space, '+', '.' and apostrophe, at most 16 characters
/// (contracts/dice-expression.md). Item names become labels, so they go through here.
/// </summary>
public static class RollLabel
{
    public const int MaxLength = 16;

    public static string Sanitize(string? text, string fallback = "item")
    {
        var builder = new StringBuilder();
        var lastWasSpace = true;
        foreach (var character in text ?? string.Empty)
        {
            var allowed = char.IsLetterOrDigit(character) || character is '+' or '.' or '\'';
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
}
