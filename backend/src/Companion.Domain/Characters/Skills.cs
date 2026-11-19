namespace Companion.Domain.Characters;

public enum SkillTraining { None, Proficient, Expertise }

public sealed record SkillDefinition(string Key, string Name, AbilityCode Ability);

/// <summary>The 18 skills of D&amp;D 5e with their PT-BR names (spec 011).</summary>
public static class Skills
{
    public static readonly IReadOnlyList<SkillDefinition> All =
    [
        new("acrobacia", "Acrobacia", AbilityCode.Dex),
        new("adestrarAnimais", "Adestrar Animais", AbilityCode.Wis),
        new("arcanismo", "Arcanismo", AbilityCode.Int),
        new("atletismo", "Atletismo", AbilityCode.Str),
        new("atuacao", "Atuação", AbilityCode.Cha),
        new("enganacao", "Enganação", AbilityCode.Cha),
        new("furtividade", "Furtividade", AbilityCode.Dex),
        new("historia", "História", AbilityCode.Int),
        new("intimidacao", "Intimidação", AbilityCode.Cha),
        new("intuicao", "Intuição", AbilityCode.Wis),
        new("investigacao", "Investigação", AbilityCode.Int),
        new("medicina", "Medicina", AbilityCode.Wis),
        new("natureza", "Natureza", AbilityCode.Int),
        new("percepcao", "Percepção", AbilityCode.Wis),
        new("persuasao", "Persuasão", AbilityCode.Cha),
        new("prestidigitacao", "Prestidigitação", AbilityCode.Dex),
        new("religiao", "Religião", AbilityCode.Int),
        new("sobrevivencia", "Sobrevivência", AbilityCode.Wis)
    ];

    private static readonly Dictionary<string, SkillDefinition> ByKey =
        All.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);

    public static bool TryGet(string? key, out SkillDefinition skill)
    {
        if (key is not null && ByKey.TryGetValue(key.Trim(), out var found))
        {
            skill = found;
            return true;
        }

        skill = null!;
        return false;
    }

    public const string Perception = "percepcao";
}
