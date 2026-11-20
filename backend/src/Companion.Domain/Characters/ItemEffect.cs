using System.Globalization;

namespace Companion.Domain.Characters;

public enum EffectType
{
    Ability, MaxHp, ArmorClass, Speed, Vision, Resistance, Immunity, Condition,
    AttackBonus, DamageBonus, SaveBonus, SkillBonus, Initiative, Text
}

/// <summary><c>Add</c> sums the value; <c>Min</c> means "passa a ser pelo menos": the larger of the current value and this one wins.</summary>
public enum EffectOp { Add, Min }

/// <summary>
/// One configurable property of an item (spec 011, FR-014). New mechanics are new
/// <see cref="EffectType"/> values handled once in <see cref="CharacterRules"/>, never code per item.
/// </summary>
public sealed record ItemEffect(EffectType Type, string? Target, EffectOp Op, int? Value)
{
    public const int MaxTargetLength = 80;
    public const int MaxEffectsPerItem = 20;

    public static readonly IReadOnlySet<string> SpeedTargets = new HashSet<string>(StringComparer.Ordinal)
        { "walk", "fly", "swim", "climb" };
    public static readonly IReadOnlySet<string> VisionTargets = new HashSet<string>(StringComparer.Ordinal)
        { "darkvision", "blindsight", "tremorsense", "truesight" };
    /// <summary>
    /// <see cref="MeleeOneHanded"/> (spec 014, Duelismo): a melee weapon without "two-handed", and never the
    /// versatile damage, which is the two-handed grip.
    /// </summary>
    public const string MeleeOneHanded = "melee-one-handed";

    public static readonly IReadOnlySet<string> AttackTargets = new HashSet<string>(StringComparer.Ordinal)
        { "all", "melee", "ranged", "spell", MeleeOneHanded };

    private static bool IsTextual(EffectType type) =>
        type is EffectType.Resistance or EffectType.Immunity or EffectType.Condition or EffectType.Text;

    /// <summary>
    /// Identity used to decide that two effects are exactly the same (FR-016):
    /// type, normalized target, operation and value.
    /// </summary>
    public string DuplicateKey =>
        string.Join('|', Type.ToString(), NormalizeTarget(Target) ?? string.Empty, Op.ToString(),
            Value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

    private static string? NormalizeTarget(string? target)
    {
        if (target is null) return null;
        var trimmed = string.Join(' ', target.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return trimmed.Length == 0 ? null : trimmed.ToLowerInvariant();
    }

    /// <summary>Returns the effect with a normalized target, or throws <see cref="ArgumentException"/> in PT-BR.</summary>
    public ItemEffect Validated()
    {
        if (!Enum.IsDefined(Type)) throw new ArgumentException("Tipo de efeito desconhecido.");
        if (!Enum.IsDefined(Op)) throw new ArgumentException("Operação do efeito inválida: use somar ou valor mínimo.");
        var target = Target is null ? null : string.Join(' ', Target.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrEmpty(target)) target = null;
        if (target is { Length: > MaxTargetLength })
            throw new ArgumentException($"O alvo do efeito deve ter até {MaxTargetLength} caracteres.");

        if (IsTextual(Type))
        {
            if (target is null) throw new ArgumentException("Informe o texto do efeito (ex.: tipo de dano ou condição).");
            if (Value is not null) throw new ArgumentException("Este tipo de efeito não tem valor numérico.");
            return this with { Target = target, Op = EffectOp.Add };
        }

        if (Value is null) throw new ArgumentException("Informe o valor do efeito.");
        if (Op == EffectOp.Add && Value is < -30 or > 30)
            throw new ArgumentException("Um efeito de soma deve ficar entre -30 e +30.");
        if (Op == EffectOp.Min && Value is < 0 or > 300)
            throw new ArgumentException("Um efeito de valor mínimo deve ficar entre 0 e 300.");

        var lower = target?.ToLowerInvariant();
        switch (Type)
        {
            case EffectType.Ability:
                if (!Abilities.TryParse(lower, out var ability))
                    throw new ArgumentException("Escolha o atributo do efeito (FOR, DES, CON, INT, SAB ou CAR).");
                return this with { Target = Abilities.Key(ability) };
            case EffectType.Speed:
                lower ??= "walk";
                if (!SpeedTargets.Contains(lower)) throw new ArgumentException("Deslocamento inválido: use andar, voo, natação ou escalada.");
                return this with { Target = lower };
            case EffectType.Vision:
                if (lower is null || !VisionTargets.Contains(lower))
                    throw new ArgumentException("Visão inválida: use visão no escuro, percepção às cegas, sentido sísmico ou visão verdadeira.");
                return this with { Target = lower };
            case EffectType.AttackBonus or EffectType.DamageBonus:
                lower ??= "all";
                if (!AttackTargets.Contains(lower))
                    throw new ArgumentException("Alvo inválido: use todos, corpo a corpo, corpo a corpo de uma mão, à distância ou magia.");
                if (Op != EffectOp.Add) throw new ArgumentException("Bônus de ataque e de dano só somam.");
                return this with { Target = lower };
            case EffectType.SaveBonus:
                lower ??= "all";
                if (lower != "all")
                {
                    if (!Abilities.TryParse(lower, out var save)) throw new ArgumentException("Salvaguarda inválida.");
                    lower = Abilities.Key(save);
                }
                if (Op != EffectOp.Add) throw new ArgumentException("Bônus de salvaguarda só soma.");
                return this with { Target = lower };
            case EffectType.SkillBonus:
                lower ??= "all";
                if (lower != "all")
                {
                    if (!Skills.TryGet(target, out var skill)) throw new ArgumentException("Perícia inválida.");
                    lower = skill.Key;
                }
                if (Op != EffectOp.Add) throw new ArgumentException("Bônus de perícia só soma.");
                return this with { Target = lower };
            case EffectType.MaxHp or EffectType.ArmorClass or EffectType.Initiative:
                if (Type == EffectType.Initiative && Op != EffectOp.Add) throw new ArgumentException("Bônus de iniciativa só soma.");
                return this with { Target = null };
            default:
                throw new ArgumentException("Tipo de efeito desconhecido.");
        }
    }
}
