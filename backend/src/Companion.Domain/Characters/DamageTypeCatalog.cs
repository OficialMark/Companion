namespace Companion.Domain.Characters;

/// <summary>Known translated damage names plus bounded custom labels. Values are serialized labels for compatibility.</summary>
public static class DamageTypeCatalog
{
    public const int MaxWeaponTypes = 8;
    private static readonly Dictionary<string, string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["acid"] = "ácido", ["ácido"] = "ácido",
        ["bludgeoning"] = "contundente", ["contundente"] = "contundente",
        ["cold"] = "gélido", ["gélido"] = "gélido",
        ["fire"] = "fogo", ["ígneo"] = "fogo", ["fogo"] = "fogo",
        ["force"] = "força", ["força"] = "força",
        ["lightning"] = "elétrico", ["elétrico"] = "elétrico",
        ["necrotic"] = "necrótico", ["necrótico"] = "necrótico",
        ["piercing"] = "perfurante", ["perfurante"] = "perfurante",
        ["poison"] = "veneno", ["veneno"] = "veneno",
        ["psychic"] = "psíquico", ["psíquico"] = "psíquico",
        ["radiant"] = "radiante", ["radiante"] = "radiante",
        ["slashing"] = "cortante", ["cortante"] = "cortante",
        ["thunder"] = "trovejante", ["trovejante"] = "trovejante"
    };

    public static IReadOnlyList<string> All { get; } =
        ["ácido", "contundente", "elétrico", "força", "gélido", "fogo", "necrótico", "perfurante", "psíquico", "radiante", "trovejante", "veneno", "cortante"];

    public static string Normalize(string value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 30)
            throw new ArgumentException("O tipo de dano deve ter entre 1 e 30 caracteres.");
        return Known.TryGetValue(normalized, out var known) ? known : normalized;
    }
}
