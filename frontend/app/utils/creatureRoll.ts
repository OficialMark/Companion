import type { AbilityKey, Creature, RollPreset } from '~/types/creatures'

export const ROLL_DICE = [4, 6, 8, 10, 12, 20, 100] as const
export const MIN_BONUS = -20
export const MAX_BONUS = 20
/** How long a creature roll's settled dice and total stay on screen (spec 010). */
export const RESULT_HOLD_MS = 2000
export const MAX_COUNT = 20
export const DEFAULT_PRESET: RollPreset = { count: 1, die: 20, ability: null, bonus: 0 }

export const ABILITIES: Array<{ key: AbilityKey; label: string; field: keyof Pick<Creature, 'strength' | 'dexterity' | 'constitution' | 'intelligence' | 'wisdom' | 'charisma'> }> = [
  { key: 'str', label: 'FOR', field: 'strength' },
  { key: 'dex', label: 'DES', field: 'dexterity' },
  { key: 'con', label: 'CON', field: 'constitution' },
  { key: 'int', label: 'INT', field: 'intelligence' },
  { key: 'wis', label: 'SAB', field: 'wisdom' },
  { key: 'cha', label: 'CAR', field: 'charisma' }
]

/** D&D modifier: floor((score - 10) / 2). Same rule as the server's Creature.Modifier. */
export function abilityModifier(score: number) {
  return Math.floor((score - 10) / 2)
}

export function formatModifier(value: number) {
  return value >= 0 ? `+${value}` : `${value}`
}

export function modifierFor(creature: Pick<Creature, 'strength' | 'dexterity' | 'constitution' | 'intelligence' | 'wisdom' | 'charisma'>, ability: AbilityKey | null) {
  if (!ability) return 0
  const entry = ABILITIES.find(item => item.key === ability)
  return entry ? abilityModifier(creature[entry.field]) : 0
}

export function clampPreset(preset: RollPreset): RollPreset {
  return {
    count: Math.min(MAX_COUNT, Math.max(1, Math.trunc(preset.count) || 1)),
    die: (ROLL_DICE as readonly number[]).includes(preset.die) ? preset.die : 20,
    ability: preset.ability && ABILITIES.some(item => item.key === preset.ability) ? preset.ability : null,
    bonus: Math.min(MAX_BONUS, Math.max(MIN_BONUS, Math.trunc(preset.bonus) || 0))
  }
}

/** Server expression, e.g. "1d20+2+1"; zero terms are dropped. */
export function buildCreatureExpression(preset: RollPreset, abilityMod: number) {
  const { count, die, bonus } = clampPreset(preset)
  let expression = `${count}d${die}`
  if (abilityMod) expression += abilityMod > 0 ? `+${abilityMod}` : `${abilityMod}`
  if (bonus) expression += bonus > 0 ? `+${bonus}` : `${bonus}`
  return expression
}

/** Human preview, e.g. "1d20 + 2 (DES) + 1". */
export function describeRoll(preset: RollPreset, abilityMod: number) {
  const { count, die, ability, bonus } = clampPreset(preset)
  const parts = [`${count}d${die}`]
  const label = ABILITIES.find(item => item.key === ability)?.label
  if (ability) parts.push(`${abilityMod < 0 ? '−' : '+'} ${Math.abs(abilityMod)} (${label})`)
  if (bonus) parts.push(`${bonus < 0 ? '−' : '+'} ${Math.abs(bonus)}`)
  return parts.join(' ')
}

export interface ActionRoll { kind: 'hit' | 'damage'; label: string; preset: RollPreset }

/**
 * Quick-roll shortcuts read from an action's text (spec 008, FR-023):
 * "+4 para acertar" / "Ataque Corpo a Corpo: +9" -> d20 + 4; "5 (1d6 + 2) de dano" -> 1d6 + 2.
 * Only the first hit bonus and the first damage expression are used.
 */
export function parseActionRolls(description: string): ActionRoll[] {
  const rolls: ActionRoll[] = []
  const hit = description.match(/([+\-−]\s?\d+)\s+para\s+acertar/i) ?? description.match(/Ataque[^:.]*:\s*([+\-−]\s?\d+)/i)
  if (hit) {
    const bonus = Number(hit[1]!.replace(/\s|−/g, match => (match === '−' ? '-' : '')))
    if (Number.isFinite(bonus)) rolls.push({ kind: 'hit', label: `Acerto ${formatModifier(bonus)}`, preset: clampPreset({ count: 1, die: 20, ability: null, bonus }) })
  }
  const damage = description.match(/\((\d+)d(\d+)\s*(?:([+\-−])\s*(\d+))?\)/i)
  if (damage) {
    const count = Number(damage[1]); const die = Number(damage[2])
    const bonus = damage[3] ? (damage[3] === '+' ? 1 : -1) * Number(damage[4]) : 0
    if ((ROLL_DICE as readonly number[]).includes(die) && count >= 1 && count <= MAX_COUNT) {
      rolls.push({ kind: 'damage', label: `Dano ${count}d${die}${bonus ? formatModifier(bonus) : ''}`, preset: clampPreset({ count, die, ability: null, bonus }) })
    }
  }
  return rolls
}
