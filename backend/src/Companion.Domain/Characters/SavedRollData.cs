namespace Companion.Domain.Characters;

public enum RollKind { Check, Attack, Damage, Free }
public enum AdvantageMode { None, Advantage, Disadvantage }

public sealed record RollModifier(string? Label, int Value);
public sealed record DamageComponent(string? Label, IReadOnlyDictionary<string, int> Dice, string DamageType);

/// <summary>
/// The closed list of icons a saved roll can use (spec 011, FR-033). Same lucide set as the
/// rest of the app; mirrored in frontend/app/utils/savedRollIcons.ts and compared by a test.
/// </summary>
public static class SavedRollIcons
{
    public static readonly IReadOnlyList<string> All =
    [
        "lucide:sword", "lucide:swords", "lucide:axe", "lucide:crosshair", "lucide:shield",
        "lucide:wand-sparkles", "lucide:sparkles", "lucide:flame", "lucide:snowflake", "lucide:zap",
        "lucide:skull", "lucide:heart-pulse", "lucide:footprints", "lucide:eye", "lucide:brain", "lucide:dices"
    ];
}

/// <summary>
/// A saved roll keeps references (ability, skill, weapon, spell), never computed numbers
/// (FR-034): it is rebuilt from the current sheet every time it is used.
/// </summary>
public sealed record SavedRollData(
    string Name,
    string Icon,
    RollKind Kind,
    IReadOnlyDictionary<string, int> Dice,
    AbilityCode? Ability,
    string? Skill,
    AbilityCode? SavingThrow,
    Guid? InventoryItemId,
    Guid? CharacterSpellId,
    IReadOnlyList<RollModifier> ExtraModifiers,
    AdvantageMode Advantage,
    int Repetitions,
    bool Versatile = false,
    IReadOnlyList<DamageComponent>? DamageComponents = null,
    string? BaseDamageType = null)
{
    public const int MaxNameLength = 40;
    public const int MaxExtraModifiers = 5;
    public const int MaxDamageComponents = 8;
    public static readonly IReadOnlyList<string> DiceKeys = ["d4", "d6", "d8", "d10", "d12", "d20", "d100"];

    public SavedRollData Validated()
    {
        var name = Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength)
            throw new ArgumentException($"O nome da rolagem deve ter entre 1 e {MaxNameLength} caracteres.");
        if (!SavedRollIcons.All.Contains(Icon)) throw new ArgumentException("Escolha um ícone da lista.");
        if (!Enum.IsDefined(Kind) || !Enum.IsDefined(Advantage)) throw new ArgumentException("Configuração de rolagem inválida.");
        if ((Ability is { } ability && !Enum.IsDefined(ability)) || (SavingThrow is { } save && !Enum.IsDefined(save)))
            throw new ArgumentException("Atributo da rolagem inválido.");
        if (Versatile && (Kind != RollKind.Damage || InventoryItemId is null))
            throw new ArgumentException("Duas mãos (versátil) só vale para dano de uma arma.");
        if (Repetitions is < 1 or > 20) throw new ArgumentException("As repetições devem ficar entre 1 e 20.");

        var dice = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, count) in Dice ?? new Dictionary<string, int>())
        {
            var normalized = key.Trim().ToLowerInvariant();
            if (!DiceKeys.Contains(normalized)) throw new ArgumentException($"Dado desconhecido: {key}.");
            if (count is < 0 or > 20) throw new ArgumentException("Use de 0 a 20 dados de cada tipo.");
            if (count > 0) dice[normalized] = count;
        }

        var extras = (ExtraModifiers ?? []).ToArray();
        if (extras.Length > MaxExtraModifiers) throw new ArgumentException($"Use até {MaxExtraModifiers} modificadores adicionais.");
        extras = extras.Select(x =>
        {
            if (x.Value is < -100 or > 100) throw new ArgumentException("Cada adicional deve ficar entre -100 e +100.");
            var label = string.IsNullOrWhiteSpace(x.Label) ? null : RollLabel.Sanitize(x.Label);
            return new RollModifier(label, x.Value);
        }).ToArray();

        var components = (DamageComponents ?? []).ToArray();
        if (components.Length > MaxDamageComponents)
            throw new ArgumentException($"Use até {MaxDamageComponents} componentes de dano.");
        if (components.Length > 0 && Kind is not (RollKind.Attack or RollKind.Damage))
            throw new ArgumentException("Componentes de dano só valem para ataques e danos.");
        components = components.Select(component =>
        {
            var label = string.IsNullOrWhiteSpace(component.Label) ? null : RollLabel.Sanitize(component.Label);
            if (label is { Length: > MaxNameLength }) throw new ArgumentException($"O nome do componente deve ter até {MaxNameLength} caracteres.");
            var componentDice = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (key, count) in component.Dice ?? new Dictionary<string, int>())
            {
                var normalized = key.Trim().ToLowerInvariant();
                if (!DiceKeys.Contains(normalized)) throw new ArgumentException($"Dado desconhecido: {key}.");
                if (count is < 0 or > 20) throw new ArgumentException("Use de 0 a 20 dados de cada tipo por componente.");
                if (count > 0) componentDice[normalized] = count;
            }
            if (componentDice.Count == 0) throw new ArgumentException("Cada componente precisa ter pelo menos um dado.");
            if (component.DamageType is null) throw new ArgumentException("Escolha o tipo de dano de cada componente.");
            return new DamageComponent(label, componentDice, DamageTypeCatalog.Normalize(component.DamageType));
        }).ToArray();
        var baseDamageType = string.IsNullOrWhiteSpace(BaseDamageType) ? null : DamageTypeCatalog.Normalize(BaseDamageType);
        if (baseDamageType is not null && Kind is not (RollKind.Attack or RollKind.Damage))
            throw new ArgumentException("Tipo de dano-base só vale para ataques e danos.");

        string? skill = null;
        if (Skill is not null)
        {
            if (!Skills.TryGet(Skill, out var definition)) throw new ArgumentException("Perícia inválida.");
            skill = definition.Key;
        }

        if (skill is not null && SavingThrow is not null)
            throw new ArgumentException("Escolha perícia ou salvaguarda, não as duas.");
        if (Kind != RollKind.Check && (skill is not null || SavingThrow is not null))
            throw new ArgumentException("Perícia e salvaguarda só valem para testes.");
        if (InventoryItemId is not null && CharacterSpellId is not null)
            throw new ArgumentException("Escolha arma ou magia, não as duas.");

        return this with { Name = name, Dice = dice, ExtraModifiers = extras, Skill = skill, DamageComponents = components, BaseDamageType = baseDamageType };
    }
}
