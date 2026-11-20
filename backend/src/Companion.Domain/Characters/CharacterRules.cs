namespace Companion.Domain.Characters;

/// <summary>
/// The single place where a player character's final values are calculated (spec 011,
/// FR-006..FR-010, FR-016, FR-018, FR-030). Pure: the same sheet, items and spells always give
/// the same <see cref="CharacterStats"/>. Base values are read, never written, so equipping
/// and unequipping can never leak a bonus into a base value (FR-008).
/// </summary>
public static class CharacterRules
{
    public const string ProficiencyLabel = "prof";
    public const string WeaponBonusLabel = "arma";
    public const string LimitLabel = "limite";
    public const string LimitSource = "Limite da regra";

    private sealed record ActiveEffect(ItemEntry Item, ItemEffect Effect)
    {
        public string Label => RollLabel.Sanitize(Item.Data.Name);
        public StatPart Part(int value) => new(Label, value, Item.Data.Name, Item.Id);
        public DecimalStatPart DecimalPart(decimal value) => new(Label, value, Item.Data.Name, Item.Id);
    }

    /// <summary>Proficiency bonus by level: +2 at 1-4, +3 at 5-8, +4 at 9-12, +5 at 13-16, +6 at 17-20.</summary>
    public static int ProficiencyBonus(int level) => 2 + (Math.Clamp(level, 1, 20) - 1) / 4;

    public static CharacterStats Compute(
        CharacterSheetData sheet,
        IReadOnlyList<ItemEntry> items,
        IReadOnlyList<SpellEntry> spells)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        items ??= [];
        spells ??= [];

        var equipped = items
            .Where(x => x.Data.Equipped && InventoryItemData.IsEquippable(x.Data.Category))
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .ToArray();
        var (active, suppressed) = ActiveEffects(equipped);
        var warnings = new List<string>();
        var proficiency = ProficiencyBonus(sheet.Level);

        // Abilities
        var abilities = new Dictionary<string, AbilityStat>(StringComparer.Ordinal);
        foreach (var code in Abilities.All)
        {
            var key = Abilities.Key(code);
            var baseScore = sheet.Score(code);
            var related = active.Where(x => x.Effect.Type == EffectType.Ability && x.Effect.Target == key).ToArray();
            var (sum, parts) = Combine(new StatPart(Abilities.Abbreviation(code), baseScore, "Valor-base"), related);
            var final = Math.Clamp(sum, 1, 30);
            abilities[key] = new AbilityStat(code, Abilities.Abbreviation(code), baseScore, final, Abilities.Modifier(final),
                WithLimit(parts, sum, final));
        }

        int Mod(AbilityCode code) => abilities[Abilities.Key(code)].Modifier;
        StatPart ModPart(AbilityCode code) => new(Abilities.Abbreviation(code), Mod(code), $"Modificador de {Abilities.Abbreviation(code)}");
        StatPart ProficiencyPart(int value) => new(ProficiencyLabel, value, "Bônus de proficiência");

        // Saving throws
        var savingThrows = new Dictionary<string, CheckStat>(StringComparer.Ordinal);
        foreach (var code in Abilities.All)
        {
            var key = Abilities.Key(code);
            var proficient = sheet.SavingThrowProficiencies.Contains(code);
            var parts = new List<StatPart> { ModPart(code) };
            if (proficient) parts.Add(ProficiencyPart(proficiency));
            parts.AddRange(active
                .Where(x => x.Effect.Type == EffectType.SaveBonus && (x.Effect.Target == "all" || x.Effect.Target == key))
                .Select(x => x.Part(x.Effect.Value!.Value)));
            savingThrows[key] = new CheckStat(key, $"Salvaguarda de {Abilities.Abbreviation(code)}", code,
                proficient ? SkillTraining.Proficient : SkillTraining.None, parts.Sum(x => x.Value), parts);
        }

        // Skills
        var skills = new Dictionary<string, CheckStat>(StringComparer.Ordinal);
        foreach (var skill in Skills.All)
        {
            var training = sheet.Training(skill.Key);
            var parts = new List<StatPart> { ModPart(skill.Ability) };
            if (training == SkillTraining.Proficient) parts.Add(ProficiencyPart(proficiency));
            if (training == SkillTraining.Expertise) parts.Add(new StatPart(ProficiencyLabel, proficiency * 2, "Especialização"));
            parts.AddRange(active
                .Where(x => x.Effect.Type == EffectType.SkillBonus && (x.Effect.Target == "all" || x.Effect.Target == skill.Key))
                .Select(x => x.Part(x.Effect.Value!.Value)));
            skills[skill.Key] = new CheckStat(skill.Key, skill.Name, skill.Ability, training, parts.Sum(x => x.Value), parts);
        }

        var perception = skills[Skills.Perception];
        var passiveParts = new List<StatPart> { new("base", 10, "Base da percepção passiva") };
        passiveParts.AddRange(perception.Parts);
        var passivePerception = new ValueStat(passiveParts.Sum(x => x.Value), passiveParts);

        // Initiative
        var initiativeParts = new List<StatPart> { ModPart(AbilityCode.Dex) };
        initiativeParts.AddRange(active.Where(x => x.Effect.Type == EffectType.Initiative).Select(x => x.Part(x.Effect.Value!.Value)));
        var initiative = new ValueStat(initiativeParts.Sum(x => x.Value), initiativeParts);

        // Armor class (FR-009)
        var armor = equipped.FirstOrDefault(x => x.Data.Category == ItemCategory.Armor && x.Data.Armor is not null);
        var shield = equipped.FirstOrDefault(x => x.Data.Category == ItemCategory.Shield);
        var acParts = new List<StatPart>();
        var dexMod = Mod(AbilityCode.Dex);
        if (armor is not null)
        {
            var data = armor.Data.Armor!;
            acParts.Add(new StatPart("armadura", data.BaseAc, armor.Data.Name, armor.Id));
            var dexPart = data.Type switch
            {
                ArmorType.Light => dexMod,
                ArmorType.Medium => Math.Min(dexMod, 2),
                _ => (int?)null
            };
            if (dexPart is not null) acParts.Add(new StatPart("DES", dexPart.Value, data.Type == ArmorType.Medium ? "Modificador de DES (máx. +2)" : "Modificador de DES"));
            if (armor.Data.MagicBonus > 0) acParts.Add(new StatPart(RollLabel.Sanitize(armor.Data.Name), armor.Data.MagicBonus, $"{armor.Data.Name} (bônus mágico)", armor.Id));
            if (!armor.Data.Proficient) warnings.Add($"Sem proficiência em {armor.Data.Name}: desvantagem em testes, salvaguardas e ataques de FOR e DES.");
            if (data.StrengthMin is { } strengthMin && abilities[Abilities.Key(AbilityCode.Str)].Final < strengthMin)
                warnings.Add($"{armor.Data.Name} pede Força {strengthMin}: o deslocamento cai 3 metros.");
        }
        else
        {
            acParts.Add(new StatPart("base", sheet.UnarmoredBaseArmorClass, "CA sem armadura"));
            acParts.Add(new StatPart("DES", dexMod, "Modificador de DES"));
            if (sheet.UnarmoredAbility is { } extra && extra != AbilityCode.Dex)
                acParts.Add(new StatPart(Abilities.Abbreviation(extra), Mod(extra), $"Defesa sem Armadura ({Abilities.Abbreviation(extra)})"));
        }

        if (shield is not null)
        {
            acParts.Add(new StatPart("escudo", shield.Data.Shield?.AcBonus ?? 2, shield.Data.Name, shield.Id));
            if (shield.Data.MagicBonus > 0) acParts.Add(new StatPart(RollLabel.Sanitize(shield.Data.Name), shield.Data.MagicBonus, $"{shield.Data.Name} (bônus mágico)", shield.Id));
            if (!shield.Data.Proficient) warnings.Add($"Sem proficiência em {shield.Data.Name}: desvantagem em testes, salvaguardas e ataques de FOR e DES.");
        }

        var armorClass = Finish(acParts, active.Where(x => x.Effect.Type == EffectType.ArmorClass), floor: 0);

        // Hit points. 5e: the CON modifier counts once per level, so an item that changes CON (Amuleto
        // da Saúde) changes the maximum by the modifier difference times the level. The base maximum
        // the player typed already includes the base CON modifier.
        var hitPointParts = new List<StatPart> { new("base", sheet.BaseMaxHitPoints, "PV máximos base") };
        var conDifference = Mod(AbilityCode.Con) - Abilities.Modifier(sheet.Constitution);
        if (conDifference != 0)
            hitPointParts.Add(new StatPart("CON", conDifference * sheet.Level, "Constituição alterada por item"));
        var maxHitPoints = Finish(hitPointParts, active.Where(x => x.Effect.Type == EffectType.MaxHp), floor: 1);

        // Speed
        var speed = new Dictionary<string, DecimalStat>(StringComparer.Ordinal);
        foreach (var mode in new[] { "walk", "fly", "swim", "climb" })
        {
            var related = active.Where(x => x.Effect.Type == EffectType.Speed && x.Effect.Target == mode).ToArray();
            if (mode != "walk" && related.Length == 0) continue;
            var parts = new List<DecimalStatPart>();
            decimal value = 0;
            if (mode == "walk")
            {
                value = sheet.BaseSpeed;
                parts.Add(new DecimalStatPart("base", sheet.BaseSpeed, "Deslocamento base"));
                if (armor?.Data.Armor?.StrengthMin is { } strengthMin && abilities[Abilities.Key(AbilityCode.Str)].Final < strengthMin)
                {
                    value -= 3;
                    parts.Add(new DecimalStatPart("armadura", -3, $"{armor.Data.Name}: Força abaixo de {strengthMin}", armor.Id));
                }
            }

            foreach (var add in related.Where(x => x.Effect.Op == EffectOp.Add))
            {
                value += add.Effect.Value!.Value;
                parts.Add(add.DecimalPart(add.Effect.Value.Value));
            }

            var minimum = related.Where(x => x.Effect.Op == EffectOp.Min).OrderByDescending(x => x.Effect.Value).FirstOrDefault();
            if (minimum is not null && minimum.Effect.Value!.Value > value)
            {
                parts.Add(minimum.DecimalPart(minimum.Effect.Value.Value - value));
                value = minimum.Effect.Value.Value;
            }

            if (value < 0) parts.Add(new DecimalStatPart(LimitLabel, -value, LimitSource));
            speed[mode] = new DecimalStat(Math.Max(0, value), parts);
        }

        // Senses
        var senses = new Dictionary<string, DecimalStat>(StringComparer.Ordinal);
        foreach (var sense in ItemEffect.VisionTargets.Order(StringComparer.Ordinal))
        {
            var related = active.Where(x => x.Effect.Type == EffectType.Vision && x.Effect.Target == sense).ToArray();
            var hasBase = sheet.BaseSenses.TryGetValue(sense, out var baseRange);
            if (!hasBase && related.Length == 0) continue;
            var parts = new List<DecimalStatPart>();
            decimal value = hasBase ? baseRange : 0;
            if (hasBase) parts.Add(new DecimalStatPart("base", baseRange, "Sentido base"));
            foreach (var add in related.Where(x => x.Effect.Op == EffectOp.Add))
            {
                value += add.Effect.Value!.Value;
                parts.Add(add.DecimalPart(add.Effect.Value.Value));
            }

            var minimum = related.Where(x => x.Effect.Op == EffectOp.Min).OrderByDescending(x => x.Effect.Value).FirstOrDefault();
            if (minimum is not null && minimum.Effect.Value!.Value > value)
            {
                parts.Add(minimum.DecimalPart(minimum.Effect.Value.Value - value));
                value = minimum.Effect.Value.Value;
            }

            if (value > 0) senses[sense] = new DecimalStat(value, parts);
        }

        IReadOnlyList<string> Texts(EffectType type) => active
            .Where(x => x.Effect.Type == type)
            .Select(x => x.Effect.Target!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Spellcasting (FR-006)
        ValueStat? spellSaveDc = null;
        ValueStat? spellAttack = null;
        if (sheet.SpellcastingAbility is { } casting)
        {
            var dcParts = new List<StatPart> { new("base", 8, "Base da CD"), ProficiencyPart(proficiency), ModPart(casting) };
            spellSaveDc = new ValueStat(dcParts.Sum(x => x.Value), dcParts);
            var attackParts = new List<StatPart> { ModPart(casting), ProficiencyPart(proficiency) };
            attackParts.AddRange(AttackEffects(active, EffectType.AttackBonus, "spell"));
            spellAttack = new ValueStat(attackParts.Sum(x => x.Value), attackParts);
        }
        else if (spells.Count > 0)
        {
            warnings.Add("Escolha o atributo de conjuração na ficha para rolar ataques e calcular a CD das magias.");
        }

        // Weapon rolls (FR-030: proficiency from the item's own flag, never twice)
        var weaponRolls = new List<WeaponRoll>();
        foreach (var item in items.Where(x => x.Data.Category == ItemCategory.Weapon && x.Data.Weapon is not null)
                     .OrderBy(x => x.SortOrder).ThenBy(x => x.Id))
        {
            var weapon = item.Data.Weapon!;
            // Finesse first: a ranged finesse weapon (Dardo) uses the higher of FOR and DES, like any finesse weapon.
            var ability = weapon.AbilityOverride ?? (weapon.Has(WeaponData.Finesse)
                ? Mod(AbilityCode.Dex) > Mod(AbilityCode.Str) ? AbilityCode.Dex : AbilityCode.Str
                : weapon.Kind == WeaponKind.Ranged ? AbilityCode.Dex : AbilityCode.Str);
            var range = weapon.Kind == WeaponKind.Ranged ? "ranged" : "melee";
            var attack = new List<StatPart> { ModPart(ability) };
            if (item.Data.Proficient) attack.Add(ProficiencyPart(proficiency));
            if (item.Data.MagicBonus > 0) attack.Add(new StatPart(WeaponBonusLabel, item.Data.MagicBonus, $"{item.Data.Name} (bônus mágico)", item.Id));
            attack.AddRange(AttackEffects(active, EffectType.AttackBonus, range));

            var damageParts = new List<StatPart> { ModPart(ability) };
            if (item.Data.MagicBonus > 0) damageParts.Add(new StatPart(WeaponBonusLabel, item.Data.MagicBonus, $"{item.Data.Name} (bônus mágico)", item.Id));
            damageParts.AddRange(AttackEffects(active, EffectType.DamageBonus, range));

            weaponRolls.Add(new WeaponRoll(item.Id, item.Data.Name, item.Data.Equipped, ability, attack.Sum(x => x.Value), attack,
                new DamageRoll(weapon.DamageDice, weapon.DamageType, damageParts),
                weapon.VersatileDice is null ? null : new DamageRoll(weapon.VersatileDice, weapon.DamageType, damageParts)));
        }

        // Spell rolls
        var spellRolls = new List<SpellRoll>();
        foreach (var spell in spells)
        {
            var data = spell.Data;
            var spellAbility = sheet.SpellcastingAbility;
            IReadOnlyList<StatPart>? attack = null;
            int? attackBonus = null;
            if (data.AttackKind == SpellAttackKind.Attack && spellAttack is not null)
            {
                attack = spellAttack.Parts;
                attackBonus = spellAttack.Value;
            }

            int? saveDc = data.AttackKind == SpellAttackKind.Save ? spellSaveDc?.Value : null;
            DamageRoll? damage = null;
            if (data.DamageDice is not null)
            {
                var parts = new List<StatPart>();
                if (data.AddAbilityToDamage && spellAbility is not null) parts.Add(ModPart(spellAbility.Value));
                parts.AddRange(AttackEffects(active, EffectType.DamageBonus, "spell"));
                damage = new DamageRoll(data.DamageDice, data.DamageType, parts);
            }

            spellRolls.Add(new SpellRoll(spell.Id, data.Name, attackBonus, attack, saveDc,
                data.AttackKind == SpellAttackKind.Save ? data.SaveAbility : null, damage));
        }

        var unproficientArmor = (armor is not null && !armor.Data.Proficient) || (shield is not null && !shield.Data.Proficient);
        var rollDefaults = new RollDefaults(unproficientArmor,
            unproficientArmor ? "Armadura ou escudo sem proficiência: desvantagem em FOR e DES." : null);

        return new CharacterStats(abilities, proficiency, savingThrows, skills, passivePerception, initiative,
            armorClass, maxHitPoints, speed, senses,
            Texts(EffectType.Resistance), Texts(EffectType.Immunity), Texts(EffectType.Condition), Texts(EffectType.Text),
            spellSaveDc, spellAttack, weaponRolls, spellRolls, warnings, suppressed, rollDefaults);
    }

    /// <summary>
    /// Collects effects of equipped items in order. An effect identical to one already counted
    /// (same <see cref="ItemEffect.DuplicateKey"/>) from another item is reported, not applied.
    /// The list is rebuilt on every call, so unequipping the kept item lets the other one count.
    /// </summary>
    private static (List<ActiveEffect> Active, List<SuppressedEffect> Suppressed) ActiveEffects(IEnumerable<ItemEntry> equipped)
    {
        var active = new List<ActiveEffect>();
        var suppressed = new List<SuppressedEffect>();
        var owners = new Dictionary<string, ItemEntry>(StringComparer.Ordinal);
        foreach (var item in equipped)
        {
            foreach (var effect in item.Data.Effects)
            {
                var key = DuplicateKey(item.Data, effect);
                if (owners.TryGetValue(key, out var kept))
                {
                    // Also when the repeat is inside the same item: it is reported, never silently dropped.
                    suppressed.Add(new SuppressedEffect(item.Id, item.Data.Name, key, kept.Id, kept.Data.Name));
                    continue;
                }

                owners[key] = item;
                active.Add(new ActiveEffect(item, effect));
            }
        }

        return (active, suppressed);
    }

    /// <summary>
    /// Identity of an effect for the "no identical effect twice" rule (FR-016). It is the
    /// effect's definition (type, target, operation, value) together with the item it comes from,
    /// by name, following the D&amp;D 5e rule that effects of the same name do not combine: two
    /// "Anel de Proteção" give +1 CA once, while an "Anel de Proteção" and a "Manto de Proteção"
    /// (+1 CA each) add up to +2.
    /// </summary>
    public static string DuplicateKey(InventoryItemData item, ItemEffect effect) =>
        $"{NormalizeName(item.Name)}|{effect.DuplicateKey}";

    private static string NormalizeName(string name) =>
        string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private static IEnumerable<StatPart> AttackEffects(IEnumerable<ActiveEffect> active, EffectType type, string target) =>
        active.Where(x => x.Effect.Type == type && (x.Effect.Target == "all" || x.Effect.Target == target))
            .Select(x => x.Part(x.Effect.Value!.Value));

    /// <summary>Base part plus "add" effects, then the highest "min" effect when it beats the sum.</summary>
    private static (int Value, IReadOnlyList<StatPart> Parts) Combine(StatPart basePart, IEnumerable<ActiveEffect> effects)
    {
        var list = effects.ToArray();
        var parts = new List<StatPart> { basePart };
        var value = basePart.Value;
        foreach (var add in list.Where(x => x.Effect.Op == EffectOp.Add))
        {
            value += add.Effect.Value!.Value;
            parts.Add(add.Part(add.Effect.Value.Value));
        }

        var minimum = list.Where(x => x.Effect.Op == EffectOp.Min).OrderByDescending(x => x.Effect.Value).FirstOrDefault();
        if (minimum is not null && minimum.Effect.Value!.Value > value)
        {
            parts.Add(minimum.Part(minimum.Effect.Value.Value - value));
            value = minimum.Effect.Value.Value;
        }

        return (value, parts);
    }

    private static ValueStat Finish(List<StatPart> parts, IEnumerable<ActiveEffect> effects, int floor)
    {
        var list = effects.ToArray();
        var value = parts.Sum(x => x.Value);
        foreach (var add in list.Where(x => x.Effect.Op == EffectOp.Add))
        {
            value += add.Effect.Value!.Value;
            parts.Add(add.Part(add.Effect.Value.Value));
        }

        var minimum = list.Where(x => x.Effect.Op == EffectOp.Min).OrderByDescending(x => x.Effect.Value).FirstOrDefault();
        if (minimum is not null && minimum.Effect.Value!.Value > value)
        {
            parts.Add(minimum.Part(minimum.Effect.Value.Value - value));
            value = minimum.Effect.Value.Value;
        }

        var final = Math.Max(floor, value);
        return new ValueStat(final, WithLimit(parts, value, final));
    }

    /// <summary>When a rule limit moved the value (atributo até 30, PV mínimo 1, CA mínima 0), a "limite" part makes the parts add up to it (FR-007).</summary>
    private static IReadOnlyList<StatPart> WithLimit(IReadOnlyList<StatPart> parts, int sum, int final) =>
        sum == final ? parts : [.. parts, new StatPart(LimitLabel, final - sum, LimitSource)];
}
