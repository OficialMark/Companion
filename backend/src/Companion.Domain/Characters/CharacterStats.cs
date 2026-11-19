namespace Companion.Domain.Characters;

/// <summary>An inventory item as seen by the rules: identity, order and data.</summary>
public sealed record ItemEntry(Guid Id, int SortOrder, InventoryItemData Data);

/// <summary>A known spell as seen by the rules.</summary>
public sealed record SpellEntry(Guid Id, CharacterSpellData Data);

/// <summary>
/// One contribution to a final value. <see cref="Label"/> is what the roll expression shows
/// inside parentheses ("FOR", "prof", "arma", an item name); <see cref="Source"/> names the item
/// or rule for the sheet's breakdown popover; <see cref="ItemId"/> links back to the item.
/// </summary>
public sealed record StatPart(string Label, int Value, string Source, Guid? ItemId = null);

public sealed record ValueStat(int Value, IReadOnlyList<StatPart> Parts);

public sealed record DecimalStat(decimal Value, IReadOnlyList<StatPart> Parts);

public sealed record AbilityStat(AbilityCode Code, string Abbreviation, int Base, int Final, int Modifier, IReadOnlyList<StatPart> Parts);

public sealed record CheckStat(string Key, string Name, AbilityCode Ability, SkillTraining Training, int Bonus, IReadOnlyList<StatPart> Parts);

/// <summary>Damage of a weapon or spell: dice plus labeled constants.</summary>
public sealed record DamageRoll(string Dice, string? DamageType, IReadOnlyList<StatPart> Parts);

public sealed record WeaponRoll(
    Guid ItemId,
    string Name,
    bool Equipped,
    AbilityCode Ability,
    int AttackBonus,
    IReadOnlyList<StatPart> Attack,
    DamageRoll Damage,
    DamageRoll? Versatile);

public sealed record SpellRoll(
    Guid SpellId,
    string Name,
    int? AttackBonus,
    IReadOnlyList<StatPart>? Attack,
    int? SaveDc,
    AbilityCode? SaveAbility,
    DamageRoll? Damage);

/// <summary>An effect that was not applied because an identical one already counts (FR-016).</summary>
public sealed record SuppressedEffect(Guid ItemId, string ItemName, string EffectKey, Guid KeptItemId, string KeptItemName);

public sealed record RollDefaults(bool DisadvantageOnStrDex, string? Reason);

/// <summary>Everything the sheet shows that is calculated. Never persisted (data-model.md).</summary>
public sealed record CharacterStats(
    IReadOnlyDictionary<string, AbilityStat> Abilities,
    int ProficiencyBonus,
    IReadOnlyDictionary<string, CheckStat> SavingThrows,
    IReadOnlyDictionary<string, CheckStat> Skills,
    ValueStat PassivePerception,
    ValueStat Initiative,
    ValueStat ArmorClass,
    ValueStat MaxHitPoints,
    IReadOnlyDictionary<string, DecimalStat> Speed,
    IReadOnlyDictionary<string, DecimalStat> Senses,
    IReadOnlyList<string> Resistances,
    IReadOnlyList<string> Immunities,
    IReadOnlyList<string> Conditions,
    IReadOnlyList<string> TextEffects,
    ValueStat? SpellSaveDc,
    ValueStat? SpellAttackBonus,
    IReadOnlyList<WeaponRoll> WeaponRolls,
    IReadOnlyList<SpellRoll> SpellRolls,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<SuppressedEffect> SuppressedEffects,
    RollDefaults RollDefaults);
