<script lang="ts">
import type { AbilityCode, ArmorType, EffectOp, EffectType, InventoryItemData, ItemCategory, ItemEffect } from '~/types/character'
import { ABILITY_LABEL, ABILITY_ORDER, SKILLS } from '~/types/character'

/** PT damage types, shared with SpellEditorModal. Values are stored as written. */
export const DAMAGE_TYPES = [
  'ácido', 'contundente', 'cortante', 'elétrico', 'fogo', 'força', 'gélido', 'necrótico', 'perfurante', 'psíquico', 'radiante', 'trovejante', 'veneno'
] as const

export const CATEGORY_ORDER: ItemCategory[] = ['Weapon', 'Armor', 'Shield', 'Gear', 'Misc', 'Potion']
export const ARMOR_TYPE_LABEL: Record<ArmorType, string> = { Light: 'leve', Medium: 'média', Heavy: 'pesada' }

export const WEAPON_PROPERTIES: { key: string; label: string }[] = [
  { key: 'finesse', label: 'Acuidade' },
  { key: 'light', label: 'Leve' },
  { key: 'heavy', label: 'Pesada' },
  { key: 'two-handed', label: 'Duas Mãos' },
  { key: 'thrown', label: 'Arremesso' },
  { key: 'ammunition', label: 'Munição' },
  { key: 'loading', label: 'Recarga' },
  { key: 'reach', label: 'Alcance' },
  { key: 'versatile', label: 'Versátil' }
]

/** Effect table of data-model.md (ItemEffect): label, targets, allowed operations, value or not. */
export interface EffectSpec {
  label: string
  /** null: the type has no target. 'text': free text. Otherwise the fixed choices. */
  targets: { value: string; label: string }[] | 'text' | null
  ops: EffectOp[]
  textual: boolean
}

const abilityTargets = ABILITY_ORDER.map(code => ({ value: code.toLowerCase(), label: ABILITY_LABEL[code] }))
const attackTargets = [
  { value: 'all', label: 'todos' }, { value: 'melee', label: 'corpo a corpo' }, { value: 'ranged', label: 'à distância' }, { value: 'spell', label: 'magia' }
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
  AttackBonus: { label: 'Bônus de ataque', targets: attackTargets, ops: ['Add'], textual: false },
  DamageBonus: { label: 'Bônus de dano', targets: attackTargets, ops: ['Add'], textual: false },
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

/** One die kind, like the server's DiceNotation.Single: "1d8", "2d6". */
export const SINGLE_DICE = /^([1-9]|1\d|20)?d(4|6|8|10|12|20|100)$/
/** A sum of dice, like DiceNotation.Sum: "8d6", "2d6+1d4". */
export const SUM_DICE = /^(([1-9]|1\d|20)?d(4|6|8|10|12|20|100))(\+([1-9]|1\d|20)?d(4|6|8|10|12|20|100))*$/
export const compactDice = (value: string | null | undefined) => (value ?? '').replace(/\s+/g, '').toLowerCase()

/** A fresh effect row with the first valid target and operation for its type. */
export function defaultEffect(type: EffectType): ItemEffect {
  const spec = EFFECT_SPECS[type]
  const target = Array.isArray(spec.targets) ? spec.targets[0]!.value : spec.targets === 'text' ? '' : null
  return { type, target, op: spec.ops[0]!, value: spec.textual ? null : 1 }
}

export function emptyItem(): InventoryItemData {
  return {
    name: '', category: 'Gear', quantity: 1, weight: null, description: null, notes: null,
    equipped: false, proficient: false, magicBonus: 0, weapon: null, armor: null, shield: null, effects: []
  }
}

export type AbilityOverride = AbilityCode | ''
</script>

<script setup lang="ts">
import { toRaw } from 'vue'
import type { InventoryItem, WeaponClass } from '~/types/character'
import { CATEGORY_LABEL } from '~/types/character'

/**
 * Create or edit an inventory item (spec 011, US3). Category blocks appear on demand; effects are
 * edited as rows type -> target -> operation -> value. The server validates and recalculates.
 */
const props = defineProps<{ item?: InventoryItem | null }>()
const emit = defineEmits<{ close: [] }>()

const character = useCharacterStore()
const isNew = !props.item
const form = reactive<InventoryItemData>(props.item ? structuredClone(toRaw(props.item.data)) : emptyItem())
if (!Array.isArray(form.effects)) form.effects = []
const localError = ref('')
const saving = ref(false)
const proficientTouched = ref(!isNew)
const nameInput = ref<HTMLInputElement | null>(null)
let opener: Element | null = null

const takesProficiency = computed(() => ['Weapon', 'Armor', 'Shield'].includes(form.category))
const proficiencies = computed(() => character.draft?.generalProficiencies ?? character.character?.sheet.generalProficiencies ?? null)

function ensureBlocks() {
  if (form.category === 'Weapon' && !form.weapon) {
    form.weapon = { damageDice: '', damageType: null, versatileDice: null, kind: 'Melee', class: 'Simple', properties: [], rangeNormal: null, rangeLong: null, abilityOverride: null, mastery: null }
  }
  if (form.category === 'Armor' && !form.armor) form.armor = { baseAc: 11, type: 'Light', strengthMin: null, stealthDisadvantage: false }
  if (form.category === 'Shield' && !form.shield) form.shield = { acBonus: 2 }
}
ensureBlocks()

/** FR-030: a new item starts proficient when the general proficiencies of the sheet cover it. */
function defaultProficient(): boolean {
  const general = proficiencies.value
  if (!general) return false
  if (form.category === 'Weapon') return (form.weapon?.class as WeaponClass) === 'Martial' ? general.martialWeapons : general.simpleWeapons
  if (form.category === 'Armor') return Boolean(form.armor && general.armor.includes(form.armor.type))
  if (form.category === 'Shield') return general.shields
  return false
}
watch(() => [form.category, form.weapon?.class, form.armor?.type], () => {
  ensureBlocks()
  if (!proficientTouched.value) form.proficient = defaultProficient()
}, { immediate: true })

function toggleProperty(key: string) {
  if (!form.weapon) return
  const list = form.weapon.properties
  form.weapon.properties = list.includes(key) ? list.filter(x => x !== key) : [...list, key]
}

function addEffect() {
  if (form.effects.length >= MAX_EFFECTS) return
  form.effects.push(defaultEffect('ArmorClass'))
}
function changeEffectType(index: number, type: EffectType) {
  form.effects[index] = defaultEffect(type)
}
function onEffectType(index: number, event: Event) {
  changeEffectType(index, (event.target as HTMLSelectElement).value as EffectType)
}
function targetOptions(type: EffectType) {
  const targets = EFFECT_SPECS[type].targets
  return Array.isArray(targets) ? targets : []
}
function removeEffect(index: number) { form.effects.splice(index, 1) }

const num = (value: unknown) => value === '' || value === null || value === undefined || Number.isNaN(Number(value)) ? null : Number(value)

/** Client-side checks for what the user can fix right here; everything else is the server's call. */
function validate(): string {
  if (!form.name.trim()) return 'Dê um nome ao item.'
  if (form.category === 'Weapon') {
    if (!SINGLE_DICE.test(compactDice(form.weapon?.damageDice))) return 'Informe o dano da arma como 1d8 (um tipo de dado: d4, d6, d8, d10, d12, d20 ou d100).'
    const versatile = compactDice(form.weapon?.versatileDice)
    if (versatile && !SINGLE_DICE.test(versatile)) return 'O dano versátil deve ser escrito como 1d10.'
  }
  for (const effect of form.effects) {
    const spec = EFFECT_SPECS[effect.type]
    if (spec.targets === 'text' && !(effect.target ?? '').trim()) return `Escreva o texto do efeito "${spec.label}".`
    if (!spec.textual && num(effect.value) === null) return `Informe o valor do efeito "${spec.label}".`
  }
  return ''
}

/** The payload the API expects: only the block of the chosen category, numbers as numbers. */
function payload(): InventoryItemData {
  const data = structuredClone(toRaw(form)) as InventoryItemData
  data.name = data.name.trim()
  data.quantity = Math.max(0, num(data.quantity) ?? 1)
  data.weight = num(data.weight)
  data.description = data.description?.trim() || null
  data.notes = data.notes?.trim() || null
  data.magicBonus = takesProficiency.value ? num(data.magicBonus) ?? 0 : 0
  data.proficient = takesProficiency.value ? data.proficient : false
  if (!['Weapon', 'Armor', 'Shield', 'Gear'].includes(data.category)) data.equipped = false
  data.weapon = data.category === 'Weapon' && data.weapon
    ? {
        ...data.weapon,
        damageDice: compactDice(data.weapon.damageDice),
        versatileDice: compactDice(data.weapon.versatileDice) || null,
        damageType: data.weapon.damageType || null,
        rangeNormal: num(data.weapon.rangeNormal),
        rangeLong: num(data.weapon.rangeLong),
        abilityOverride: data.weapon.abilityOverride || null
      }
    : null
  data.armor = data.category === 'Armor' && data.armor
    ? { ...data.armor, baseAc: num(data.armor.baseAc) ?? 10, strengthMin: num(data.armor.strengthMin) }
    : null
  data.shield = data.category === 'Shield' && data.shield ? { acBonus: num(data.shield.acBonus) ?? 2 } : null
  data.effects = data.effects.map((effect) => {
    const spec = EFFECT_SPECS[effect.type]
    return {
      type: effect.type,
      target: spec.targets === null ? null : (effect.target ?? '').trim() || null,
      op: spec.ops.includes(effect.op) ? effect.op : 'Add',
      value: spec.textual ? null : num(effect.value)
    }
  })
  return data
}

async function save() {
  localError.value = validate()
  if (localError.value) return
  const data = payload()
  saving.value = true
  const item = props.item
  const ok = await character.run((api, session) => item ? api.saveItem(session, item.id, data, item.version) : api.addItem(session, data))
  saving.value = false
  if (ok) emit('close')
}

const shownError = computed(() => localError.value || (saving.value ? '' : character.error))

onMounted(async () => {
  opener = document.activeElement
  character.error = ''
  await nextTick()
  nameInput.value?.focus()
})
onBeforeUnmount(() => { if (opener instanceof HTMLElement) opener.focus() })
</script>

<template>
  <div class="modal-overlay sheet-modal-overlay" @keydown.esc.stop="emit('close')" @click.self="emit('close')">
    <form
      class="modal-panel sheet-modal"
      role="dialog"
      aria-modal="true"
      aria-labelledby="item-editor-title"
      data-item-editor
      novalidate
      @submit.prevent="save"
    >
      <header class="sheet-modal-header">
        <h2 id="item-editor-title">{{ isNew ? 'Novo item' : 'Editar item' }}</h2>
        <button type="button" class="icon-button" aria-label="Fechar" @click="emit('close')"><Icon name="lucide:x" /></button>
      </header>

      <div class="sheet-grid">
        <label class="sheet-field wide">
          <span>Nome</span>
          <input ref="nameInput" v-model="form.name" type="text" maxlength="80" required data-item-field="name">
        </label>
        <label class="sheet-field">
          <span>Categoria</span>
          <select v-model="form.category" data-item-field="category">
            <option v-for="category in CATEGORY_ORDER" :key="category" :value="category">{{ CATEGORY_LABEL[category] }}</option>
          </select>
        </label>
        <label class="sheet-field">
          <span>Quantidade</span>
          <input v-model.number="form.quantity" type="number" min="0" max="9999" inputmode="numeric" data-item-field="quantity">
        </label>
        <label class="sheet-field">
          <span>Peso (kg)</span>
          <input v-model.number="form.weight" type="number" min="0" step="0.1" inputmode="decimal" data-item-field="weight">
        </label>
        <label v-if="takesProficiency" class="sheet-field">
          <span>Bônus mágico</span>
          <select v-model.number="form.magicBonus" data-item-field="magicBonus">
            <option v-for="bonus in [0, 1, 2, 3]" :key="bonus" :value="bonus">{{ bonus ? `+${bonus}` : 'nenhum' }}</option>
          </select>
        </label>
      </div>

      <fieldset v-if="form.category === 'Weapon' && form.weapon" class="item-block" data-block="weapon">
        <legend>Arma</legend>
        <div class="sheet-grid">
          <label class="sheet-field">
            <span>Dano</span>
            <input
              v-model="form.weapon.damageDice"
              type="text"
              placeholder="1d8"
              autocomplete="off"
              pattern="\s*([1-9]|1\d|20)?\s*d\s*(4|6|8|10|12|20|100)\s*"
              aria-describedby="weapon-dice-hint"
              data-item-field="damageDice"
            >
            <small id="weapon-dice-hint" class="field-hint">Um tipo de dado, como 1d8 ou 2d6.</small>
          </label>
          <label class="sheet-field">
            <span>Tipo de dano</span>
            <select v-model="form.weapon.damageType" data-item-field="damageType">
              <option :value="null">sem tipo</option>
              <option v-for="type in DAMAGE_TYPES" :key="type" :value="type">{{ type }}</option>
            </select>
          </label>
          <label class="sheet-field">
            <span>Dano versátil (opcional)</span>
            <input v-model="form.weapon.versatileDice" type="text" placeholder="1d10" autocomplete="off" data-item-field="versatileDice">
          </label>
          <label class="sheet-field">
            <span>Tipo</span>
            <select v-model="form.weapon.kind" data-item-field="kind">
              <option value="Melee">corpo a corpo</option>
              <option value="Ranged">à distância</option>
            </select>
          </label>
          <label class="sheet-field">
            <span>Classe</span>
            <select v-model="form.weapon.class" data-item-field="weaponClass">
              <option value="Simple">simples</option>
              <option value="Martial">marcial</option>
            </select>
          </label>
          <label class="sheet-field">
            <span>Atributo do ataque</span>
            <select v-model="form.weapon.abilityOverride" data-item-field="abilityOverride">
              <option :value="null">automático</option>
              <option v-for="code in ABILITY_ORDER" :key="code" :value="code">{{ ABILITY_LABEL[code] }}</option>
            </select>
          </label>
          <label class="sheet-field">
            <span>Alcance normal (m)</span>
            <input v-model.number="form.weapon.rangeNormal" type="number" min="0" step="0.5" inputmode="decimal" data-item-field="rangeNormal">
          </label>
          <label class="sheet-field">
            <span>Alcance máximo (m)</span>
            <input v-model.number="form.weapon.rangeLong" type="number" min="0" step="0.5" inputmode="decimal" data-item-field="rangeLong">
          </label>
        </div>
        <div class="chip-row" role="group" aria-label="Propriedades da arma">
          <button
            v-for="property in WEAPON_PROPERTIES"
            :key="property.key"
            type="button"
            class="chip"
            :aria-pressed="form.weapon.properties.includes(property.key)"
            :data-property="property.key"
            @click="toggleProperty(property.key)"
          >
            {{ property.label }}
          </button>
        </div>
      </fieldset>

      <fieldset v-if="form.category === 'Armor' && form.armor" class="item-block" data-block="armor">
        <legend>Armadura</legend>
        <div class="sheet-grid">
          <label class="sheet-field">
            <span>CA base</span>
            <input v-model.number="form.armor.baseAc" type="number" min="0" max="30" inputmode="numeric" data-item-field="baseAc">
          </label>
          <label class="sheet-field">
            <span>Tipo</span>
            <select v-model="form.armor.type" data-item-field="armorType">
              <option value="Light">leve</option>
              <option value="Medium">média</option>
              <option value="Heavy">pesada</option>
            </select>
          </label>
          <label class="sheet-field">
            <span>Força mínima</span>
            <input v-model.number="form.armor.strengthMin" type="number" min="0" max="30" inputmode="numeric" data-item-field="strengthMin">
          </label>
          <label class="item-check">
            <input v-model="form.armor.stealthDisadvantage" type="checkbox" data-item-field="stealthDisadvantage">
            <span>Desvantagem em Furtividade</span>
          </label>
        </div>
      </fieldset>

      <fieldset v-if="form.category === 'Shield' && form.shield" class="item-block" data-block="shield">
        <legend>Escudo</legend>
        <label class="sheet-field">
          <span>Bônus de CA</span>
          <input v-model.number="form.shield.acBonus" type="number" min="0" max="10" inputmode="numeric" data-item-field="acBonus">
        </label>
      </fieldset>

      <label v-if="takesProficiency" class="item-check">
        <input v-model="form.proficient" type="checkbox" data-item-field="proficient" @change="proficientTouched = true">
        <span>Tenho proficiência com este item</span>
      </label>

      <fieldset class="item-block" data-block="effects">
        <legend>Efeitos</legend>
        <p v-if="!form.effects.length" class="sheet-hint">Nenhum efeito. Use efeitos para bônus de CA, atributos, deslocamento, resistências e afins.</p>
        <ul class="effect-rows">
          <li v-for="(effect, index) in form.effects" :key="index" class="effect-row" :data-effect-row="index">
            <label class="sheet-field">
              <span>Tipo</span>
              <select :value="effect.type" data-effect-type @change="onEffectType(index, $event)">
                <option v-for="type in EFFECT_TYPES" :key="type" :value="type">{{ EFFECT_SPECS[type].label }}</option>
              </select>
            </label>
            <label v-if="targetOptions(effect.type).length" class="sheet-field">
              <span>Alvo</span>
              <select v-model="effect.target" data-effect-target>
                <option v-for="target in targetOptions(effect.type)" :key="target.value" :value="target.value">{{ target.label }}</option>
              </select>
            </label>
            <label v-else-if="EFFECT_SPECS[effect.type].targets === 'text'" class="sheet-field">
              <span>Texto</span>
              <input v-model="effect.target" type="text" maxlength="80" placeholder="ex.: fogo, envenenado" data-effect-target>
            </label>
            <label v-if="EFFECT_SPECS[effect.type].ops.length > 1" class="sheet-field">
              <span>Operação</span>
              <select v-model="effect.op" data-effect-op>
                <option v-for="op in EFFECT_SPECS[effect.type].ops" :key="op" :value="op">{{ OP_LABEL[op] }}</option>
              </select>
            </label>
            <label v-if="!EFFECT_SPECS[effect.type].textual" class="sheet-field">
              <span>Valor</span>
              <input v-model.number="effect.value" type="number" :min="effect.op === 'Min' ? 0 : -30" :max="effect.op === 'Min' ? 300 : 30" inputmode="numeric" data-effect-value>
            </label>
            <button type="button" class="icon-button effect-remove" :aria-label="`Remover efeito ${index + 1}`" data-effect-remove @click="removeEffect(index)">
              <Icon name="lucide:trash-2" />
            </button>
          </li>
        </ul>
        <button type="button" class="button ghost small" :disabled="form.effects.length >= MAX_EFFECTS" data-add-effect @click="addEffect">
          <Icon name="lucide:plus" />Adicionar efeito
        </button>
      </fieldset>

      <div class="sheet-grid">
        <label class="sheet-field wide">
          <span>Descrição</span>
          <textarea v-model="form.description" rows="3" data-item-field="description" />
        </label>
        <label class="sheet-field wide">
          <span>Anotações</span>
          <textarea v-model="form.notes" rows="2" data-item-field="notes" />
        </label>
      </div>

      <p v-if="shownError" class="form-error" role="alert" data-item-error>{{ shownError }}</p>

      <div class="sheet-modal-actions">
        <button type="button" class="button ghost" @click="emit('close')">Cancelar</button>
        <button type="submit" class="button primary" :disabled="saving" data-item-save>
          <Icon :name="saving ? 'lucide:loader-circle' : 'lucide:check'" :class="{ spin: saving }" />{{ isNew ? 'Adicionar' : 'Salvar' }}
        </button>
      </div>
    </form>
  </div>
</template>
