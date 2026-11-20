<script lang="ts">
import type { AbilityCode, ArmorType, InventoryItemData, ItemCategory } from '~/types/character'
import { ABILITY_LABEL, ABILITY_ORDER } from '~/types/character'
import { EFFECT_SPECS, EFFECT_TYPES, MAX_EFFECTS, OP_LABEL, defaultEffect, effectsError, normalizeEffects } from '~/utils/effects'

// The effect table moved to utils/effects (spec 014: features use it too); re-exported for existing imports.
export { EFFECT_SPECS, EFFECT_TYPES, MAX_EFFECTS, OP_LABEL, defaultEffect }
export type { EffectSpec } from '~/utils/effects'

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

/** One die kind, like the server's DiceNotation.Single: "1d8", "2d6". */
export const SINGLE_DICE = /^([1-9]|1\d|20)?d(4|6|8|10|12|20|100)$/
/** A sum of dice, like DiceNotation.Sum: "8d6", "2d6+1d4". */
export const SUM_DICE = /^(([1-9]|1\d|20)?d(4|6|8|10|12|20|100))(\+([1-9]|1\d|20)?d(4|6|8|10|12|20|100))*$/
export const compactDice = (value: string | null | undefined) => (value ?? '').replace(/\s+/g, '').toLowerCase()

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
import type { CatalogItem, InventoryItem, WeaponClass } from '~/types/character'
import { CATEGORY_LABEL } from '~/types/character'
import EffectRowsEditor from './EffectRowsEditor.vue'

/**
 * Create or edit an inventory item (spec 011, US3). Category blocks appear on demand; effects are
 * edited as rows type -> target -> operation -> value. The server validates and recalculates.
 *
 * With `catalog` (spec 013) the same form edits the master's compendium: it adds the catalog fields
 * (English name, subcategory, rarity, attunement, cost, "asks for a base item") and saves through the
 * catalog API instead of the sheet. Saving a compendium entry creates the table's version of it.
 */
const props = defineProps<{ item?: InventoryItem | null; catalog?: { entry: CatalogItem | null } | null }>()
const emit = defineEmits<{ close: []; saved: [entry: CatalogItem] }>()

const RARITY_OPTIONS = ['Comum', 'Incomum', 'Raro', 'Muito Raro', 'Lendário', 'Artefato', 'Varia']

const character = useCharacterStore()
const catalogMode = computed(() => Boolean(props.catalog))
const entry = props.catalog?.entry ?? null
const isNew = catalogMode.value ? !entry : !props.item
const initial = entry ? entry.template : props.item?.data
const form = reactive<InventoryItemData>(initial ? structuredClone(toRaw(initial)) : emptyItem())
if (!Array.isArray(form.effects)) form.effects = []
const extra = reactive({
  nameEn: entry?.nameEn ?? '',
  subcategory: entry?.subcategory ?? '',
  rarity: entry?.rarity ?? '',
  requiresAttunement: entry?.requiresAttunement ?? false,
  cost: entry?.cost ?? '',
  /** Generic magic weapon or armor ("Arma +1"): no stat block, the player picks the base item. */
  generic: entry?.requiresBaseItem ?? false
})
const canBeGeneric = computed(() => catalogMode.value && (form.category === 'Weapon' || form.category === 'Armor'))
const isGeneric = computed(() => canBeGeneric.value && extra.generic)
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

const num = (value: unknown) => value === '' || value === null || value === undefined || Number.isNaN(Number(value)) ? null : Number(value)

/** Client-side checks for what the user can fix right here; everything else is the server's call. */
function validate(): string {
  if (!form.name.trim()) return 'Dê um nome ao item.'
  if (form.category === 'Weapon' && !isGeneric.value) {
    if (!SINGLE_DICE.test(compactDice(form.weapon?.damageDice))) return 'Informe o dano da arma como 1d8 (um tipo de dado: d4, d6, d8, d10, d12, d20 ou d100).'
    const versatile = compactDice(form.weapon?.versatileDice)
    if (versatile && !SINGLE_DICE.test(versatile)) return 'O dano versátil deve ser escrito como 1d10.'
  }
  return effectsError(form.effects)
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
  data.effects = normalizeEffects(data.effects)
  if (isGeneric.value) {
    data.weapon = null
    data.armor = null
  }
  if (catalogMode.value) {
    // A catalog template never starts equipped nor with a proficiency: the sheet decides when it is added.
    data.equipped = false
    data.proficient = false
  }
  return data
}

const text = (value: string) => value.trim() || null

async function saveCatalog(data: InventoryItemData) {
  const body = {
    template: data,
    nameEn: text(extra.nameEn),
    subcategory: text(extra.subcategory),
    rarity: text(extra.rarity),
    requiresAttunement: extra.requiresAttunement,
    cost: text(extra.cost)
  }
  try {
    const api = useCharacterApi()
    const session = character.session()
    const saved = entry ? await api.saveCatalogItem(session, entry.id, body) : await api.createCatalogItem(session, body)
    emit('saved', saved)
    return true
  } catch (error) {
    localError.value = (error as Error).message || 'Não foi possível salvar no compêndio.'
    return false
  }
}

async function save() {
  localError.value = validate()
  if (localError.value) return
  const data = payload()
  saving.value = true
  const item = props.item
  const ok = catalogMode.value
    ? await saveCatalog(data)
    : await character.run((api, session) => item ? api.saveItem(session, item.id, data, item.version) : api.addItem(session, data))
  saving.value = false
  if (ok) emit('close')
}

const title = computed(() => catalogMode.value
  ? (isNew ? 'Novo item do compêndio' : 'Editar item do compêndio')
  : (isNew ? 'Novo item' : 'Editar item'))

const shownError = computed(() => localError.value || (saving.value || catalogMode.value ? '' : character.error))

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
        <h2 id="item-editor-title">{{ title }}</h2>
        <button type="button" class="icon-button" aria-label="Fechar" @click="emit('close')"><Icon name="lucide:x" /></button>
      </header>
      <p v-if="entry?.origin === 'Compendium'" class="sheet-hint" data-catalog-copy-hint>
        <Icon name="lucide:info" /> Salvar cria a versão da mesa deste item. O original do compêndio não muda e volta se você excluir a versão da mesa.
      </p>

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

      <fieldset v-if="catalogMode" class="item-block catalog-extra" data-block="catalog">
        <legend>Compêndio</legend>
        <div class="sheet-grid">
          <label class="sheet-field">
            <span>Nome em inglês</span>
            <input v-model="extra.nameEn" type="text" maxlength="120" data-item-field="nameEn">
          </label>
          <label class="sheet-field">
            <span>Subcategoria</span>
            <input v-model="extra.subcategory" type="text" maxlength="80" placeholder="ex.: Arma marcial corpo a corpo" data-item-field="subcategory">
          </label>
          <label class="sheet-field">
            <span>Raridade</span>
            <input v-model="extra.rarity" type="text" list="catalog-rarities" maxlength="30" placeholder="vazio: item não mágico" aria-describedby="catalog-rarity-hint" data-item-field="rarity">
            <small id="catalog-rarity-hint" class="field-hint">Deixe vazio para itens comuns, não mágicos: só eles servem de base para "Arma +1". "Comum" é a raridade de itens mágicos comuns.</small>
            <datalist id="catalog-rarities"><option v-for="value in RARITY_OPTIONS" :key="value" :value="value" /></datalist>
          </label>
          <label class="sheet-field">
            <span>Custo</span>
            <input v-model="extra.cost" type="text" maxlength="40" placeholder="ex.: 15 PO" data-item-field="cost">
          </label>
        </div>
        <label class="item-check">
          <input v-model="extra.requiresAttunement" type="checkbox" data-item-field="requiresAttunement">
          <span>Requer sintonia</span>
        </label>
        <label v-if="canBeGeneric" class="item-check">
          <input v-model="extra.generic" type="checkbox" data-item-field="generic">
          <span>{{ form.category === 'Armor' ? 'Pede a armadura base ao adicionar à ficha (ex.: Armadura +1)' : 'Pede a arma base ao adicionar à ficha (ex.: Arma +1)' }}</span>
        </label>
      </fieldset>

      <fieldset v-if="form.category === 'Weapon' && form.weapon && !isGeneric" class="item-block" data-block="weapon">
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

      <fieldset v-if="form.category === 'Armor' && form.armor && !isGeneric" class="item-block" data-block="armor">
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

      <label v-if="takesProficiency && !catalogMode" class="item-check">
        <input v-model="form.proficient" type="checkbox" data-item-field="proficient" @change="proficientTouched = true">
        <span>Tenho proficiência com este item</span>
      </label>

      <EffectRowsEditor v-model="form.effects" hint="Nenhum efeito. Use efeitos para bônus de CA, atributos, deslocamento, resistências e afins." />

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
