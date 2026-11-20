import type { EffectOp, EffectType, ItemEffect } from '~/types/character'
import { ABILITY_LABEL, ABILITY_ORDER, SKILLS } from '~/types/character'

/**
 * The effect table of data-model.md (ItemEffect), shared by items (spec 011) and features (spec 014):
 * label, targets, allowed operations, value or not. The server validates again; this only guides the form.
 */
export interface EffectSpec {
  label: string
  /** null: the type has no target. 'text': free text. Otherwise the fixed choices. */
  targets: { value: string; label: string }[] | 'text' | null
  ops: EffectOp[]
  textual: boolean
}

const abilityTargets = ABILITY_ORDER.map(code => ({ value: code.toLowerCase(), label: ABILITY_LABEL[code] }))

/** Spec 014: "melee-one-handed" is Duelismo: a melee weapon not two-handed, never the versatile damage. */
export const ATTACK_TARGETS = [
  { value: 'all', label: 'todos' },
  { value: 'melee', label: 'corpo a corpo' },
  { value: 'melee-one-handed', label: 'corpo a corpo de uma mão' },
  { value: 'ranged', label: 'à distância' },
  { value: 'spell', label: 'magia' }
]

export const EFFECT_SPECS: Record<EffectType, EffectSpec> = {
  Ability: { label: 'Atributo', targets: abilityTargets, ops: ['Add', 'Min'], textual: false },
  MaxHp: { label: 'PV máximos', targets: null, ops: ['Add', 'Min'], textual: false },
  ArmorClass: { label: 'Classe de Armadura', targets: null, ops: ['Add', 'Min'], textual: false },
  Speed: {
    label: 'Deslocamento',
    targets: [{ value: 'walk', label: 'andar' }, { value: 'fly', label: 'voo' }, { value: 'swim', label: 'natação' }, { value: 'climb', label: 'escalada' }],
    ops: ['Add', 'Min'], textual: false
  },
  Vision: {
    label: 'Visão',
    targets: [
      { value: 'darkvision', label: 'visão no escuro' }, { value: 'blindsight', label: 'percepção às cegas' },
      { value: 'tremorsense', label: 'sentido sísmico' }, { value: 'truesight', label: 'visão verdadeira' }
    ],
    ops: ['Add', 'Min'], textual: false
  },
  AttackBonus: { label: 'Bônus de ataque', targets: ATTACK_TARGETS, ops: ['Add'], textual: false },
  DamageBonus: { label: 'Bônus de dano', targets: ATTACK_TARGETS, ops: ['Add'], textual: false },
  SaveBonus: { label: 'Bônus de salvaguarda', targets: [{ value: 'all', label: 'todas' }, ...abilityTargets], ops: ['Add'], textual: false },
  SkillBonus: {
    label: 'Bônus de perícia',
    targets: [{ value: 'all', label: 'todas' }, ...SKILLS.map(skill => ({ value: skill.key, label: skill.name }))],
    ops: ['Add'], textual: false
  },
  Initiative: { label: 'Iniciativa', targets: null, ops: ['Add'], textual: false },
  Resistance: { label: 'Resistência', targets: 'text', ops: ['Add'], textual: true },
  Immunity: { label: 'Imunidade', targets: 'text', ops: ['Add'], textual: true },
  Condition: { label: 'Condição', targets: 'text', ops: ['Add'], textual: true },
  Text: { label: 'Texto', targets: 'text', ops: ['Add'], textual: true }
}
export const EFFECT_TYPES = Object.keys(EFFECT_SPECS) as EffectType[]
export const OP_LABEL: Record<EffectOp, string> = { Add: 'Soma', Min: 'Pelo menos' }
export const MAX_EFFECTS = 20

/** A fresh effect row with the first valid target and operation for its type. */
export function defaultEffect(type: EffectType): ItemEffect {
  const spec = EFFECT_SPECS[type]
  const target = Array.isArray(spec.targets) ? spec.targets[0]!.value : spec.targets === 'text' ? '' : null
  return { type, target, op: spec.ops[0]!, value: spec.textual ? null : 1 }
}

const num = (value: unknown) => value === '' || value === null || value === undefined || Number.isNaN(Number(value)) ? null : Number(value)

/** What the user can fix in the form: an empty text or a missing value. Empty string when fine. */
export function effectsError(effects: ItemEffect[]): string {
  for (const effect of effects) {
    const spec = EFFECT_SPECS[effect.type]
    if (spec.targets === 'text' && !(effect.target ?? '').trim()) return `Escreva o texto do efeito "${spec.label}".`
    if (!spec.textual && num(effect.value) === null) return `Informe o valor do efeito "${spec.label}".`
  }
  return ''
}

/** The payload the API expects: only the fields each type uses, numbers as numbers. */
export function normalizeEffects(effects: ItemEffect[]): ItemEffect[] {
  return effects.map((effect) => {
    const spec = EFFECT_SPECS[effect.type]
    return {
      type: effect.type,
      target: spec.targets === null ? null : (effect.target ?? '').trim() || null,
      op: spec.ops.includes(effect.op) ? effect.op : 'Add',
      value: spec.textual ? null : num(effect.value)
    }
  })
}

/** One line for a card: "Bônus de dano +2 (corpo a corpo de uma mão)", "Resistência: fogo". */
export function describeEffect(effect: ItemEffect): string {
  const spec = EFFECT_SPECS[effect.type]
  if (!spec) return effect.type
  if (spec.textual) return `${spec.label}: ${effect.target ?? ''}`
  const value = effect.value ?? 0
  const amount = effect.op === 'Min' ? `pelo menos ${value}` : value >= 0 ? `+${value}` : `${value}`
  const target = Array.isArray(spec.targets) ? spec.targets.find(x => x.value === effect.target)?.label : null
  return target ? `${spec.label} ${amount} (${target})` : `${spec.label} ${amount}`
}
