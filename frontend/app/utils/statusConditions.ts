import type { StatusCondition } from '~/types/campaign'

export interface StatusConditionInfo {
  value: StatusCondition
  label: string
  /** Lucide icon. Kept as a string literal so @nuxt/icon's client-bundle scanner picks it up. */
  icon: string
  /** Badge/chip accent; chosen to stay legible on the dark map and menu backgrounds. */
  color: string
}

/**
 * Single source of truth for every status condition: label, icon and color are the same
 * in the token menu (AttackAreaPanel.vue) and on the board badge (BattlemapToken.vue).
 * Order = order in the menu (most frequent first). Spec 007, data-model.md.
 */
export const STATUS_CONDITIONS: readonly StatusConditionInfo[] = [
  { value: 'Prone', label: 'Caído', icon: 'lucide:arrow-down-to-line', color: '#d9a35f' },
  { value: 'Stunned', label: 'Atordoado', icon: 'lucide:sparkles', color: '#f2d16b' },
  { value: 'Paralyzed', label: 'Paralisado', icon: 'lucide:zap', color: '#ffe066' },
  { value: 'Poisoned', label: 'Envenenado', icon: 'lucide:biohazard', color: '#7fd858' },
  { value: 'Bleeding', label: 'Sangrando', icon: 'lucide:droplets', color: '#ff5a6e' },
  { value: 'Restrained', label: 'Imobilizado', icon: 'lucide:link', color: '#b9a7ff' },
  { value: 'Grappled', label: 'Agarrado', icon: 'lucide:grab', color: '#c79bff' },
  { value: 'Blinded', label: 'Cego', icon: 'lucide:eye-off', color: '#a3b1c6' },
  { value: 'Deafened', label: 'Surdo', icon: 'lucide:ear-off', color: '#8fa3b8' },
  { value: 'Invisible', label: 'Invisível', icon: 'lucide:ghost', color: '#cfe8ff' },
  { value: 'Frightened', label: 'Amedrontado', icon: 'lucide:frown', color: '#b98cff' },
  { value: 'Charmed', label: 'Enfeitiçado', icon: 'lucide:heart-handshake', color: '#ff8fcf' },
  { value: 'Unconscious', label: 'Inconsciente', icon: 'lucide:bed', color: '#7f9cff' },
  { value: 'Petrified', label: 'Petrificado', icon: 'lucide:mountain', color: '#b7a99a' },
  { value: 'Incapacitated', label: 'Incapacitado', icon: 'lucide:ban', color: '#ff9f6e' },
  { value: 'Exhausted', label: 'Exausto', icon: 'lucide:battery-low', color: '#e6a15c' },
  { value: 'Concentrating', label: 'Concentrando', icon: 'lucide:brain', color: '#5ce1c4' },
  { value: 'Burning', label: 'Em chamas', icon: 'lucide:flame', color: '#ff7a3d' }
]

const byValue = new Map(STATUS_CONDITIONS.map(item => [item.value, item]))

const UNKNOWN: Omit<StatusConditionInfo, 'value'> = { label: '', icon: 'lucide:circle-help', color: '#a3b1c6' }

export function statusConditionInfo(value: StatusCondition | string): StatusConditionInfo {
  return byValue.get(value as StatusCondition) ?? { ...UNKNOWN, value: value as StatusCondition, label: value }
}

/** How many condition badges a token draws before collapsing the rest into "+N". */
export const MAX_VISIBLE_CONDITION_BADGES = 3

export function visibleConditionBadges(conditions: readonly StatusCondition[]) {
  if (conditions.length <= MAX_VISIBLE_CONDITION_BADGES) return { shown: [...conditions], overflow: 0 }
  // Keep room for the "+N" badge so the row never grows past MAX_VISIBLE_CONDITION_BADGES + 1.
  const shown = conditions.slice(0, MAX_VISIBLE_CONDITION_BADGES)
  return { shown, overflow: conditions.length - shown.length }
}

export function conditionListLabel(conditions: readonly StatusCondition[]) {
  return conditions.map(item => statusConditionInfo(item).label).join(', ')
}
