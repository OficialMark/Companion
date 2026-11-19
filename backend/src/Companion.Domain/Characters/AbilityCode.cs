namespace Companion.Domain.Characters;

/// <summary>The six ability scores of a player character (spec 011).</summary>
public enum AbilityCode { Str, Dex, Con, Int, Wis, Cha }

public static class Abilities
{
    public static readonly IReadOnlyList<AbilityCode> All =
        [AbilityCode.Str, AbilityCode.Dex, AbilityCode.Con, AbilityCode.Int, AbilityCode.Wis, AbilityCode.Cha];

    /// <summary>Portuguese abbreviation shown in the sheet and inside roll expressions, e.g. "(FOR) 3".</summary>
    public static string Abbreviation(AbilityCode code) => code switch
    {
        AbilityCode.Str => "FOR",
        AbilityCode.Dex => "DES",
        AbilityCode.Con => "CON",
        AbilityCode.Int => "INT",
        AbilityCode.Wis => "SAB",
        AbilityCode.Cha => "CAR",
        _ => throw new ArgumentOutOfRangeException(nameof(code))
    };

    /// <summary>Lowercase key used in JSON maps and in effect targets ("str", "dex"...).</summary>
    public static string Key(AbilityCode code) => code.ToString().ToLowerInvariant();

    /// <summary>Parses an effect or request target: accepts "str"/"dex"... and the PT abbreviations "for"/"des"...</summary>
    public static bool TryParse(string? value, out AbilityCode code)
    {
        code = default;
        switch (value?.Trim().ToLowerInvariant())
        {
            case "str" or "for": code = AbilityCode.Str; return true;
            case "dex" or "des": code = AbilityCode.Dex; return true;
            case "con": code = AbilityCode.Con; return true;
            case "int": code = AbilityCode.Int; return true;
            case "wis" or "sab": code = AbilityCode.Wis; return true;
            case "cha" or "car": code = AbilityCode.Cha; return true;
            default: return false;
        }
    }

    /// <summary>D&amp;D 5e modifier: floor((score - 10) / 2).</summary>
    public static int Modifier(int score) => (int)Math.Floor((score - 10) / 2.0);
}
