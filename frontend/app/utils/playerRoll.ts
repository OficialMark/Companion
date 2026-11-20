/**
 * Player roll configuration (spec 011, US4 to US6): turns what the player picked on the roll panel
 * (or what a saved roll references) into the canonical expression the server runs.
 *
 * Pure. Every number comes from `stats`, calculated by the server; this file never adds proficiency,
 * ability modifiers or bonuses by itself (FR-018, FR-030). It only picks which server parts belong to the
 * roll and appends the dice of the pad and the player's extra modifiers. The string is produced by
 * `buildExpression` only, so the screen shows exactly what the server will execute (FR-027, FR-036).
 */
import type {
  AbilityCode, AdvantageMode, Character, CharacterStats, DieKey, RollKind, RollModifier, SavedRollData, StatPart
} from '~/types/character'
import { ABILITY_LABEL, ABILITY_NAME, abilityKey } from '~/types/character'
import type { DiceRollResult } from '~/types/campaign'
import type { RollExpressionInput, RollPart, RollSelection } from '~/utils/rollExpression'
import { DIE_ORDER, buildExpression, expressionLimits } from '~/utils/rollExpression'

export type RollSource =
  | { type: 'ability'; ability: AbilityCode }
  | { type: 'skill'; key: string }
  | { type: 'save'; ability: AbilityCode }
  | { type: 'weapon'; itemId: string; versatile?: boolean }
  | { type: 'spell'; spellId: string }
  | null

export type DiceCounts = Partial<Record<DieKey, number>>

export interface RollConfig {
  kind: RollKind
  source: RollSource
  dice: DiceCounts
  extraModifiers: RollModifier[]
  advantage: AdvantageMode
  repetitions: number
}

export interface ResolvedRoll {
  /** Canonical expression, '' when there is no die to roll. */
  expression: string
  parts: RollPart[]
  dice: DiceCounts
  /** Descriptive label for the table history (max 80 characters, the server limit). */
  label: string
  selection: RollSelection
  /** PT-BR reason the roll cannot be sent, or null. */
  error: string | null
}

/** Same limits as SavedRollData.cs. */
export const MAX_DICE_PER_TYPE = 20
export const MAX_EXTRAS = 5
export const MIN_EXTRA = -100
export const MAX_EXTRA = 100
export const MAX_ROLL_NAME = 40
export const MAX_ROLL_LABEL = 80

export const KIND_ORDER: RollKind[] = ['Check', 'Attack', 'Damage', 'Free']
export const KIND_LABEL: Record<RollKind, string> = { Check: 'Teste', Attack: 'Ataque', Damage: 'Dano', Free: 'Livre' }
export const ADVANTAGE_LABEL: Record<AdvantageMode, string> = { None: 'Normal', Advantage: 'Vantagem', Disadvantage: 'Desvantagem' }

export const MISSING_WEAPON = 'Arma ausente: escolha outra arma ou edite a rolagem.'
export const MISSING_SPELL = 'Magia ausente: escolha outra magia ou edite a rolagem.'
export const NO_SPELLCASTING = 'Escolha o atributo de conjuração na ficha para rolar o ataque desta magia.'
export const SPELL_WITHOUT_ATTACK = 'Esta magia não faz jogada de ataque.'

export function emptyConfig(): RollConfig {
  return { kind: 'Free', source: null, dice: {}, extraModifiers: [], advantage: 'None', repetitions: 1 }
}

/** Plain deep copy (safe on Vue proxies, unlike structuredClone). */
export function cloneConfig(config: RollConfig): RollConfig {
  return {
    kind: config.kind,
    source: config.source ? { ...config.source } : null,
    dice: { ...config.dice },
    extraModifiers: config.extraModifiers.map(extra => ({ label: extra.label, value: extra.value })),
    advantage: config.advantage,
    repetitions: config.repetitions
  }
}

/** "2d6+1d4" -> { d6: 2, d4: 1 }. Unknown or malformed terms are ignored (the server validates the item). */
export function diceFromNotation(notation: string | null | undefined): DiceCounts {
  const dice: DiceCounts = {}
  for (const term of (notation ?? '').replace(/\s+/g, '').toLowerCase().split('+')) {
    const match = term.match(/^(\d*)d(\d+)$/)
    if (!match) continue
    const key = `d${Number(match[2])}` as DieKey
    if (!(DIE_ORDER as readonly string[]).includes(key)) continue
    const count = match[1] === '' ? 1 : Number(match[1])
    if (count > 0) dice[key] = (dice[key] ?? 0) + count
  }
  return dice
}

export function addDice(...sets: DiceCounts[]): DiceCounts {
  const total: DiceCounts = {}
  for (const set of sets) {
    for (const key of DIE_ORDER) {
      const count = set[key] ?? 0
      if (count) total[key] = (total[key] ?? 0) + count
    }
  }
  return total
}

export function diceCount(dice: DiceCounts) {
  return DIE_ORDER.reduce((sum, key) => sum + (dice[key] ?? 0), 0)
}

export function sourceKey(source: RollSource): string {
  if (!source) return 'none'
  switch (source.type) {
    case 'ability': return `ability:${source.ability}`
    case 'skill': return `skill:${source.key}`
    case 'save': return `save:${source.ability}`
    case 'weapon': return `weapon:${source.itemId}`
    case 'spell': return `spell:${source.spellId}`
  }
}

export function sourceFromKey(key: string): RollSource {
  const colon = key.indexOf(':')
  if (colon < 0) return null
  const [type, value] = [key.slice(0, colon), key.slice(colon + 1)]
  if (!value) return null
  if (type === 'ability') return { type: 'ability', ability: value as AbilityCode }
  if (type === 'skill') return { type: 'skill', key: value }
  if (type === 'save') return { type: 'save', ability: value as AbilityCode }
  if (type === 'weapon') return { type: 'weapon', itemId: value }
  if (type === 'spell') return { type: 'spell', spellId: value }
  return null
}

/** Kinds that make sense for a source. Skills and saves are only tests (same rule as SavedRollData.cs). */
export function kindsFor(source: RollSource): RollKind[] {
  if (!source) return KIND_ORDER
  if (source.type === 'skill' || source.type === 'save') return ['Check']
  if (source.type === 'weapon' || source.type === 'spell') return ['Attack', 'Damage']
  return KIND_ORDER
}

/** The kind to switch to when the source changes and the current kind no longer fits. */
export function defaultKindFor(source: RollSource, current: RollKind, stats?: CharacterStats | null): RollKind {
  const kinds = kindsFor(source)
  // Picking an ability on a free roll means "test this ability": a free roll has no d20 of its own.
  if (current === 'Free' && source?.type === 'ability') return 'Check'
  if (kinds.includes(current)) return current
  if (source?.type === 'spell') {
    const spell = stats?.spellRolls.find(item => item.spellId === source.spellId)
    if (spell && !spell.attack && spell.damage) return 'Damage'
  }
  return kinds[0]!
}

/** Ability the d20 of this roll uses, when it is a test, a save or an attack. */
export function rollAbility(config: RollConfig, stats: CharacterStats | null | undefined): AbilityCode | null {
  const source = config.source
  if (!source || config.kind === 'Damage' || config.kind === 'Free') return null
  if (source.type === 'ability' || source.type === 'save') return source.ability
  if (source.type === 'skill') return stats?.skills[source.key]?.ability ?? null
  if (source.type === 'weapon' && config.kind === 'Attack') {
    return stats?.weaponRolls.find(item => item.itemId === source.itemId)?.ability ?? null
  }
  return null
}

/** True when the armor rule (rollDefaults.disadvantageOnStrDex) applies to this roll: a d20 roll with FOR or DES. */
export function usesStrDex(config: RollConfig, stats: CharacterStats | null | undefined) {
  const ability = rollAbility(config, stats)
  return ability === 'Str' || ability === 'Dex'
}

function statParts(parts: readonly StatPart[] | null | undefined): RollPart[] {
  return (parts ?? []).map(part => ({ label: part.label, value: part.value }))
}

function truncateLabel(label: string) {
  return label.length > MAX_ROLL_LABEL ? label.slice(0, MAX_ROLL_LABEL).trimEnd() : label
}

function selectionOf(advantage: AdvantageMode): RollSelection {
  return advantage === 'Advantage' ? 'advantage' : advantage === 'Disadvantage' ? 'disadvantage' : 'none'
}

/**
 * Builds the roll: source dice and server parts, plus the pad dice and the extras, wrapped in the
 * advantage selection and the repetitions by `buildExpression`.
 */
export function resolveRoll(config: RollConfig, stats: CharacterStats | null | undefined, character?: Character | null): ResolvedRoll {
  const source = config.source
  const kind = config.kind
  let sourceDice: DiceCounts = {}
  let flatTotal: number | null = null
  let parts: RollPart[] = []
  let label = kind === 'Free' ? 'Rolagem livre' : KIND_LABEL[kind]
  let error: string | null = null
  const d20 = kind === 'Check' || kind === 'Attack'
  if (d20) sourceDice = { d20: 1 }

  if (source && !kindsFor(source).includes(kind)) {
    error = source.type === 'skill' || source.type === 'save'
      ? 'Perícia e salvaguarda só valem para testes.'
      : 'Arma e magia valem para ataque ou dano.'
  } else if (source?.type === 'ability') {
    const ability = stats?.abilities[abilityKey(source.ability)]
    parts = ability ? [{ label: ABILITY_LABEL[source.ability], value: ability.modifier }] : []
    const name = kind === 'Check' ? ABILITY_NAME[source.ability] : ABILITY_LABEL[source.ability]
    label = kind === 'Free' ? `Rolagem: ${ABILITY_LABEL[source.ability]}` : `${KIND_LABEL[kind]}: ${name}`
  } else if (source?.type === 'skill') {
    const skill = stats?.skills[source.key]
    parts = statParts(skill?.parts)
    label = `Teste: ${skill?.name ?? source.key}`
  } else if (source?.type === 'save') {
    parts = statParts(stats?.savingThrows[abilityKey(source.ability)]?.parts)
    label = `Salvaguarda: ${ABILITY_LABEL[source.ability]}`
  } else if (source?.type === 'weapon') {
    const weapon = stats?.weaponRolls.find(item => item.itemId === source.itemId)
    if (!weapon) {
      error = MISSING_WEAPON
      label = `${KIND_LABEL[kind]}: arma ausente`
    } else if (kind === 'Attack') {
      parts = statParts(weapon.attack)
      label = `Ataque: ${weapon.name}`
    } else {
      const damage = source.versatile && weapon.versatile ? weapon.versatile : weapon.damage
      sourceDice = diceFromNotation(damage.dice)
      parts = statParts(damage.parts)
      // Fixed damage (the SRD blowgun deals 1): no dice to roll, it is a constant.
      if (/^\d+$/.test(damage.dice.trim())) {
        const flat = Number(damage.dice.trim())
        parts = [{ label: 'dano', value: flat }, ...parts]
        flatTotal = parts.reduce((sum, part) => sum + part.value, 0)
      }
      label = `Dano: ${weapon.name}${source.versatile && weapon.versatile ? ' (duas mãos)' : ''}`
    }
  } else if (source?.type === 'spell') {
    const spell = stats?.spellRolls.find(item => item.spellId === source.spellId)
    if (!spell) {
      error = MISSING_SPELL
      label = `${KIND_LABEL[kind]}: magia ausente`
    } else if (kind === 'Attack') {
      label = `Ataque: ${spell.name}`
      if (spell.attack) parts = statParts(spell.attack)
      else {
        const data = character?.spells.find(item => item.id === source.spellId)?.data
        error = data && data.attackKind !== 'Attack' ? SPELL_WITHOUT_ATTACK : NO_SPELLCASTING
      }
    } else {
      // A spell without detected damage dice: the player picks them on the pad (spec edge case).
      sourceDice = diceFromNotation(spell.damage?.dice)
      parts = statParts(spell.damage?.parts)
      label = `Dano: ${spell.name}`
    }
  }

  const dice = addDice(sourceDice, config.dice)
  if (!error && flatTotal !== null && Object.values(dice).every(count => !count)) {
    error = `Dano fixo de ${flatTotal}: não precisa rolar.`
  }
  parts = [...parts, ...config.extraModifiers.map(extra => ({ label: extra.label?.trim() ? extra.label : null, value: extra.value }))]
  const selection = selectionOf(config.advantage)
  const input: RollExpressionInput = { dice, parts, selection, repetitions: config.repetitions }
  const expression = buildExpression(input)
  if (!error) error = expressionLimits(input)
  if (!error) {
    const big = (Object.keys(config.dice) as DieKey[]).find(key => (config.dice[key] ?? 0) > MAX_DICE_PER_TYPE)
    if (big) error = `Use até ${MAX_DICE_PER_TYPE} dados de cada tipo.`
  }
  if (!error && config.extraModifiers.length > MAX_EXTRAS) error = `Use até ${MAX_EXTRAS} modificadores adicionais.`
  if (!error && config.extraModifiers.some(extra => extra.value < MIN_EXTRA || extra.value > MAX_EXTRA)) {
    error = `Cada adicional deve ficar entre ${MIN_EXTRA} e +${MAX_EXTRA}.`
  }
  return { expression, parts, dice, label: truncateLabel(label), selection, error }
}

/** The weapon damage that follows a hit (US4: "acertou" opens the damage roll of the same weapon). */
export function weaponDamageExpression(config: RollConfig, stats: CharacterStats | null | undefined): string | null {
  if (config.source?.type !== 'weapon') return null
  const resolved = resolveRoll({ kind: 'Damage', source: config.source, dice: {}, extraModifiers: [], advantage: 'None', repetitions: 1 }, stats)
  return resolved.error ? null : resolved.expression || null
}

/** Saved roll (references only, FR-034) to panel configuration. */
export function configFromSavedRoll(data: SavedRollData): RollConfig {
  let source: RollSource = null
  if (data.inventoryItemId) {
    source = data.versatile && data.kind === 'Damage'
      ? { type: 'weapon', itemId: data.inventoryItemId, versatile: true }
      : { type: 'weapon', itemId: data.inventoryItemId }
  }
  else if (data.characterSpellId) source = { type: 'spell', spellId: data.characterSpellId }
  else if (data.skill) source = { type: 'skill', key: data.skill }
  else if (data.savingThrow) source = { type: 'save', ability: data.savingThrow }
  else if (data.ability) source = { type: 'ability', ability: data.ability }
  const dice: DiceCounts = {}
  for (const key of DIE_ORDER) if (data.dice[key]) dice[key] = data.dice[key]
  return {
    kind: data.kind,
    source,
    dice,
    extraModifiers: data.extraModifiers.map(extra => ({ label: extra.label, value: extra.value })),
    advantage: data.advantage,
    repetitions: data.repetitions
  }
}

/** Panel configuration to the SavedRollData the server stores (same field names as SavedRollData.cs). */
export function savedRollFromConfig(config: RollConfig, name: string, icon: string): SavedRollData {
  const source = config.source
  const dice: DiceCounts = {}
  for (const key of DIE_ORDER) if (config.dice[key]) dice[key] = config.dice[key]
  return {
    name: name.trim(),
    icon,
    kind: config.kind,
    dice,
    ability: source?.type === 'ability' ? source.ability : null,
    skill: source?.type === 'skill' ? source.key : null,
    savingThrow: source?.type === 'save' ? source.ability : null,
    inventoryItemId: source?.type === 'weapon' ? source.itemId : null,
    characterSpellId: source?.type === 'spell' ? source.spellId : null,
    // The server accepts it only for weapon damage; anything else always goes as false.
    versatile: config.kind === 'Damage' && source?.type === 'weapon' && source.versatile === true,
    extraModifiers: config.extraModifiers.map(extra => ({ label: extra.label?.trim() ? extra.label.trim() : null, value: extra.value })),
    advantage: config.advantage,
    repetitions: config.repetitions
  }
}

export interface PlannedDie { id: string; faces: number }

/**
 * Dice to animate: every die of every execution (both executions of advantage and disadvantage)
 * of every repetition, in expression order. Capped at 100, like the server limit.
 */
export function plannedDice(dice: DiceCounts, selection: RollSelection, repetitions: number): PlannedDie[] {
  const planned: PlannedDie[] = []
  const executions = selection === 'none' ? 1 : 2
  for (let repetition = 0; repetition < repetitions; repetition++) {
    for (let execution = 0; execution < executions; execution++) {
      for (const key of DIE_ORDER) {
        for (let index = 0; index < (dice[key] ?? 0); index++) {
          if (planned.length >= 100) return planned
          planned.push({ id: `${repetition}-${execution}-${key}-${index}`, faces: Number(key.slice(1)) })
        }
      }
    }
  }
  return planned
}

/** Server values for `plannedDice`, in the same order (both executions when the result has them). */
export function plannedValues(results: DiceRollResult[]): Array<{ faces: number; value: number }> {
  const values: Array<{ faces: number; value: number }> = []
  for (const result of results) {
    const executions = result.executions?.length ? result.executions : [{ terms: result.terms }]
    for (const execution of executions) {
      for (const term of execution.terms) {
        const faces = Number(term.expression.match(/d(\d+)$/i)?.[1])
        if (!Number.isFinite(faces)) continue
        for (const value of term.values) values.push({ faces, value })
      }
    }
  }
  return values
}
