namespace Companion.Domain.Characters;

public enum ArmorType { Light, Medium, Heavy }
public enum ItemCategory { Weapon, Armor, Shield, Gear, Misc, Potion }
public enum WeaponKind { Melee, Ranged }
public enum WeaponClass { Simple, Martial }
public enum SpellAttackKind { None, Attack, Save }

public sealed record GeneralProficiencies(
    IReadOnlyList<ArmorType> Armor,
    bool Shields,
    bool SimpleWeapons,
    bool MartialWeapons,
    string? Other)
{
    public static readonly GeneralProficiencies None = new([], false, false, false, null);
}

/// <summary>
/// Every editable field of a player's sheet (spec 011, FR-005). Base values only: nothing
/// here is a final value. Final values come from <see cref="CharacterRules.Compute"/>.
/// </summary>
public sealed record CharacterSheetData(
    string? CharacterName,
    string? ClassName,
    string? Subclass,
    string? Species,
    string? Background,
    string? Alignment,
    int Level,
    int Experience,
    int Strength, int Dexterity, int Constitution, int Intelligence, int Wisdom, int Charisma,
    IReadOnlyList<AbilityCode> SavingThrowProficiencies,
    IReadOnlyDictionary<string, SkillTraining> Skills,
    GeneralProficiencies GeneralProficiencies,
    int BaseMaxHitPoints,
    int CurrentHitPoints,
    int TemporaryHitPoints,
    int UnarmoredBaseArmorClass,
    AbilityCode? UnarmoredAbility,
    decimal BaseSpeed,
    IReadOnlyDictionary<string, decimal> BaseSenses,
    AbilityCode? SpellcastingAbility,
    string? Traits,
    string? Notes)
{
    public const int MaxShortText = 60;
    public const int MaxLongText = 4000;

    public static readonly CharacterSheetData Default = new(
        null, null, null, null, null, null, 1, 0, 10, 10, 10, 10, 10, 10,
        [], new Dictionary<string, SkillTraining>(), GeneralProficiencies.None,
        10, 10, 0, 10, null, 9m, new Dictionary<string, decimal>(), null, null, null);

    public int Score(AbilityCode code) => code switch
    {
        AbilityCode.Str => Strength,
        AbilityCode.Dex => Dexterity,
        AbilityCode.Con => Constitution,
        AbilityCode.Int => Intelligence,
        AbilityCode.Wis => Wisdom,
        AbilityCode.Cha => Charisma,
        _ => throw new ArgumentOutOfRangeException(nameof(code))
    };

    public SkillTraining Training(string skillKey) =>
        Skills.TryGetValue(skillKey, out var training) ? training : SkillTraining.None;

    /// <summary>Normalizes text and collections, or throws <see cref="ArgumentException"/> with a PT-BR message.</summary>
    public CharacterSheetData Validated()
    {
        static string? Text(string? value, int max, string field)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed)) return null;
            if (trimmed.Length > max) throw new ArgumentException($"{field} deve ter até {max} caracteres.");
            return trimmed;
        }

        static void Range(int value, int min, int max, string field)
        {
            if (value < min || value > max) throw new ArgumentException($"{field} deve ficar entre {min} e {max}.");
        }

        Range(Level, 1, 20, "O nível");
        Range(Experience, 0, 999_999, "A experiência");
        foreach (var (score, name) in new[]
                 {
                     (Strength, "Força"), (Dexterity, "Destreza"), (Constitution, "Constituição"),
                     (Intelligence, "Inteligência"), (Wisdom, "Sabedoria"), (Charisma, "Carisma")
                 })
            Range(score, 1, 30, name);
        Range(BaseMaxHitPoints, 1, 9_999, "Os PV máximos base");
        Range(CurrentHitPoints, 0, 99_999, "Os PV atuais");
        Range(TemporaryHitPoints, 0, 9_999, "Os PV temporários");
        Range(UnarmoredBaseArmorClass, 0, 30, "A CA base sem armadura");
        if (BaseSpeed is < 0 or > 300) throw new ArgumentException("O deslocamento base deve ficar entre 0 e 300 metros.");

        var skills = new Dictionary<string, SkillTraining>(StringComparer.Ordinal);
        foreach (var (key, training) in Skills ?? new Dictionary<string, SkillTraining>())
        {
            if (!Characters.Skills.TryGet(key, out var skill)) throw new ArgumentException($"Perícia desconhecida: {key}.");
            if (!Enum.IsDefined(training)) throw new ArgumentException($"Treinamento inválido em {skill.Name}.");
            if (training != SkillTraining.None) skills[skill.Key] = training;
        }

        var senses = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var (key, range) in BaseSenses ?? new Dictionary<string, decimal>())
        {
            var normalized = key.Trim().ToLowerInvariant();
            if (!ItemEffect.VisionTargets.Contains(normalized)) throw new ArgumentException($"Sentido desconhecido: {key}.");
            if (range is < 0 or > 300) throw new ArgumentException("O alcance de um sentido deve ficar entre 0 e 300 metros.");
            if (range > 0) senses[normalized] = range;
        }

        foreach (var code in SavingThrowProficiencies ?? [])
            if (!Enum.IsDefined(code)) throw new ArgumentException("Salvaguarda inválida na ficha.");
        if (UnarmoredAbility is { } unarmored && !Enum.IsDefined(unarmored))
            throw new ArgumentException("Atributo da Defesa sem Armadura inválido.");
        if (SpellcastingAbility is { } casting && !Enum.IsDefined(casting))
            throw new ArgumentException("Atributo de conjuração inválido.");

        var general = GeneralProficiencies ?? GeneralProficiencies.None;
        foreach (var type in general.Armor ?? [])
            if (!Enum.IsDefined(type)) throw new ArgumentException("Tipo de armadura inválido nas proficiências.");
        general = general with
        {
            Armor = (general.Armor ?? []).Distinct().Order().ToArray(),
            Other = Text(general.Other, 500, "As outras proficiências")
        };

        return this with
        {
            CharacterName = Text(CharacterName, MaxShortText, "O nome do personagem"),
            ClassName = Text(ClassName, MaxShortText, "A classe"),
            Subclass = Text(Subclass, MaxShortText, "A subclasse"),
            Species = Text(Species, MaxShortText, "A espécie"),
            Background = Text(Background, MaxShortText, "O antecedente"),
            Alignment = Text(Alignment, MaxShortText, "O alinhamento"),
            SavingThrowProficiencies = (SavingThrowProficiencies ?? []).Distinct().Order().ToArray(),
            Skills = skills,
            GeneralProficiencies = general,
            BaseSenses = senses,
            Traits = Text(Traits, MaxLongText, "Os traços"),
            Notes = Text(Notes, MaxLongText, "As anotações")
        };
    }
}

public sealed record WeaponData(
    string DamageDice,
    string? DamageType,
    string? VersatileDice,
    WeaponKind Kind,
    WeaponClass Class,
    IReadOnlyList<string> Properties,
    decimal? RangeNormal,
    decimal? RangeLong,
    AbilityCode? AbilityOverride,
    string? Mastery,
    IReadOnlyList<string>? DamageTypes = null)
{
    public const string Finesse = "finesse";
    public const int MaxProperties = 10;
    public const int MaxTagLength = 30;

    /// <summary>
    /// The closed list of weapon properties (data-model.md, WeaponProperty). Same keys as the item
    /// seed and frontend/app/components/character/ItemEditorModal.vue (WEAPON_PROPERTIES).
    /// </summary>
    public static readonly IReadOnlySet<string> KnownProperties = new HashSet<string>(StringComparer.Ordinal)
    {
        "finesse", "light", "heavy", "two-handed", "thrown", "ammunition", "loading", "reach", "versatile", "range"
    };

    public bool Has(string property) => Properties.Contains(property, StringComparer.OrdinalIgnoreCase);
}

public sealed record ArmorData(int BaseAc, ArmorType Type, int? StrengthMin, bool StealthDisadvantage);

public sealed record ShieldData(int AcBonus);

/// <summary>An inventory entry (spec 011, FR-011..FR-015).</summary>
public sealed record InventoryItemData(
    string Name,
    ItemCategory Category,
    int Quantity,
    decimal? Weight,
    string? Description,
    string? Notes,
    bool Equipped,
    bool Proficient,
    int MagicBonus,
    WeaponData? Weapon,
    ArmorData? Armor,
    ShieldData? Shield,
    IReadOnlyList<ItemEffect> Effects)
{
    public const int MaxNameLength = 80;
    public const int MaxDescriptionLength = 12000;

    public static bool IsEquippable(ItemCategory category) =>
        category is ItemCategory.Weapon or ItemCategory.Armor or ItemCategory.Shield or ItemCategory.Gear;

    public static bool TakesProficiency(ItemCategory category) =>
        category is ItemCategory.Weapon or ItemCategory.Armor or ItemCategory.Shield;

    /// <summary>Normalizes and checks category blocks, dice and effects; throws <see cref="ArgumentException"/> in PT-BR.</summary>
    public InventoryItemData Validated()
    {
        var name = Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength)
            throw new ArgumentException($"O nome do item deve ter entre 1 e {MaxNameLength} caracteres.");
        if (!Enum.IsDefined(Category)) throw new ArgumentException("Categoria de item inválida.");
        if (Quantity is < 0 or > 9_999) throw new ArgumentException("A quantidade deve ficar entre 0 e 9999.");
        if (Weight is < 0 or > 99_999) throw new ArgumentException("O peso deve ficar entre 0 e 99999 kg.");
        if (MagicBonus is < 0 or > 3) throw new ArgumentException("O bônus mágico deve ficar entre 0 e +3.");
        if (Description is { Length: > MaxDescriptionLength } || Notes is { Length: > 4000 })
            throw new ArgumentException($"A descrição deve ter até {MaxDescriptionLength} caracteres e as anotações até 4000.");

        WeaponData? weapon = null;
        ArmorData? armor = null;
        ShieldData? shield = null;
        switch (Category)
        {
            case ItemCategory.Weapon:
                weapon = Weapon ?? throw new ArgumentException("Informe os dados da arma (dano e tipo).");
                if (!Enum.IsDefined(weapon.Kind) || !Enum.IsDefined(weapon.Class))
                    throw new ArgumentException("Tipo ou categoria de arma inválido.");
                if (weapon.AbilityOverride is { } overrideAbility && !Enum.IsDefined(overrideAbility))
                    throw new ArgumentException("Atributo da arma inválido.");
                var properties = (weapon.Properties ?? []).Select(x => (x ?? string.Empty).Trim().ToLowerInvariant())
                    .Where(x => x.Length > 0).Distinct().ToArray();
                if (properties.Length > WeaponData.MaxProperties)
                    throw new ArgumentException($"Uma arma pode ter até {WeaponData.MaxProperties} propriedades.");
                if (properties.FirstOrDefault(x => !WeaponData.KnownProperties.Contains(x)) is { } unknown)
                    throw new ArgumentException($"Propriedade de arma desconhecida: {(unknown.Length > WeaponData.MaxTagLength ? unknown[..WeaponData.MaxTagLength] + "..." : unknown)}.");
                var mastery = string.IsNullOrWhiteSpace(weapon.Mastery) ? null : weapon.Mastery.Trim();
                if (mastery is { Length: > WeaponData.MaxTagLength })
                    throw new ArgumentException($"A maestria da arma deve ter até {WeaponData.MaxTagLength} caracteres.");
                var damageType = string.IsNullOrWhiteSpace(weapon.DamageType) ? null : weapon.DamageType.Trim();
                if (damageType is { Length: > WeaponData.MaxTagLength })
                    throw new ArgumentException($"O tipo de dano deve ter até {WeaponData.MaxTagLength} caracteres.");
                var damageTypes = (weapon.DamageTypes ?? (damageType is null ? [] : [damageType]))
                    .Select(DamageTypeCatalog.Normalize)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (damageTypes.Length > DamageTypeCatalog.MaxWeaponTypes)
                    throw new ArgumentException($"Uma arma pode ter até {DamageTypeCatalog.MaxWeaponTypes} tipos de dano.");
                weapon = weapon with
                {
                    DamageDice = DiceNotation.Single(weapon.DamageDice, "O dano da arma"),
                    VersatileDice = string.IsNullOrWhiteSpace(weapon.VersatileDice)
                        ? null
                        : DiceNotation.Single(weapon.VersatileDice, "O dano versátil"),
                    DamageType = damageTypes.FirstOrDefault() ?? damageType,
                    DamageTypes = damageTypes,
                    Properties = properties,
                    Mastery = mastery
                };
                if (weapon.RangeNormal is < 0 or > 3000 || weapon.RangeLong is < 0 or > 3000)
                    throw new ArgumentException("O alcance da arma deve ficar entre 0 e 3000 metros.");
                break;
            case ItemCategory.Armor:
                armor = Armor ?? throw new ArgumentException("Informe a CA e o tipo da armadura.");
                if (!Enum.IsDefined(armor.Type)) throw new ArgumentException("Tipo de armadura inválido.");
                if (armor.BaseAc is < 0 or > 30) throw new ArgumentException("A CA da armadura deve ficar entre 0 e 30.");
                if (armor.StrengthMin is < 1 or > 30) throw new ArgumentException("A Força mínima deve ficar entre 1 e 30.");
                break;
            case ItemCategory.Shield:
                shield = Shield ?? new ShieldData(2);
                if (shield.AcBonus is < 0 or > 10) throw new ArgumentException("O bônus de CA do escudo deve ficar entre 0 e 10.");
                break;
        }

        var effects = (Effects ?? []).Select(x => x.Validated()).ToArray();
        if (effects.Length > ItemEffect.MaxEffectsPerItem)
            throw new ArgumentException($"Um item pode ter até {ItemEffect.MaxEffectsPerItem} efeitos.");

        return this with
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            Equipped = Equipped && IsEquippable(Category),
            Proficient = Proficient && TakesProficiency(Category),
            MagicBonus = Category is ItemCategory.Weapon or ItemCategory.Armor or ItemCategory.Shield ? MagicBonus : 0,
            Weapon = weapon,
            Armor = armor,
            Shield = shield,
            Effects = effects
        };
    }
}

/// <summary>A spell known by the character (spec 011, FR-019/FR-024).</summary>
public sealed record CharacterSpellData(
    string Name,
    int Level,
    string? School,
    string? CastingTime,
    string? Range,
    string? Components,
    string? Duration,
    bool Ritual,
    bool Concentration,
    string? Description,
    SpellAttackKind AttackKind,
    AbilityCode? SaveAbility,
    string? DamageDice,
    string? DamageType,
    bool AddAbilityToDamage)
{
    public CharacterSpellData Validated()
    {
        static string? Text(string? value, int max, string field)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed)) return null;
            if (trimmed.Length > max) throw new ArgumentException($"{field} deve ter até {max} caracteres.");
            return trimmed;
        }

        var name = Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 80) throw new ArgumentException("O nome da magia deve ter entre 1 e 80 caracteres.");
        if (Level is < 0 or > 9) throw new ArgumentException("O nível da magia deve ficar entre 0 e 9.");
        if (!Enum.IsDefined(AttackKind)) throw new ArgumentException("Tipo de rolagem da magia inválido.");
        if (SaveAbility is { } saveAbility && !Enum.IsDefined(saveAbility))
            throw new ArgumentException("Atributo da salvaguarda da magia inválido.");
        if (AttackKind == SpellAttackKind.Save && SaveAbility is null)
            throw new ArgumentException("Escolha o atributo da salvaguarda da magia.");

        return this with
        {
            Name = name,
            School = Text(School, 80, "A escola"),
            CastingTime = Text(CastingTime, 80, "O tempo de conjuração"),
            Range = Text(Range, 80, "O alcance"),
            Components = Text(Components, 200, "Os componentes"),
            Duration = Text(Duration, 80, "A duração"),
            Description = Text(Description, 8000, "A descrição"),
            SaveAbility = AttackKind == SpellAttackKind.Save ? SaveAbility : null,
            DamageDice = string.IsNullOrWhiteSpace(DamageDice) ? null : DiceNotation.Sum(DamageDice, "O dano da magia"),
            DamageType = Text(DamageType, 30, "O tipo de dano")
        };
    }
}
