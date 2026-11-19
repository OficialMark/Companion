<script setup lang="ts">
import type { AdvantageMode, Character, CharacterStats, RollKind } from '~/types/character'
import { ABILITY_LABEL, ABILITY_NAME, ABILITY_ORDER, SKILLS, abilityKey } from '~/types/character'
import DicePad from '~/components/character/DicePad.vue'
import {
  ADVANTAGE_LABEL, KIND_LABEL, KIND_ORDER, MAX_EXTRA, MAX_EXTRAS, MIN_EXTRA, defaultKindFor, kindsFor, resolveRoll,
  sourceFromKey, sourceKey, usesStrDex, type DiceCounts, type RollConfig
} from '~/utils/playerRoll'
import { MAX_LABEL_LENGTH, MAX_REPETITIONS } from '~/utils/rollExpression'

/**
 * The controls shared by the Modo Jogo roll panel and the saved roll editor (spec 011, FR-036): both
 * render this component, so the same configuration always shows the same options and the same
 * `[data-expression]`.
 */
const props = withDefaults(defineProps<{
  modelValue: RollConfig
  stats: CharacterStats | null
  character: Character | null
  disabled?: boolean
}>(), { disabled: false })
const emit = defineEmits<{ 'update:modelValue': [value: RollConfig] }>()

const NO_DICE = 'Escolha pelo menos um dado.'
const ADVANTAGES: AdvantageMode[] = ['None', 'Advantage', 'Disadvantage']
const ADVANTAGE_KEY: Record<AdvantageMode, string> = { None: 'none', Advantage: 'advantage', Disadvantage: 'disadvantage' }
const ADVANTAGE_ICON: Record<AdvantageMode, string> = { None: 'lucide:minus', Advantage: 'lucide:chevrons-up', Disadvantage: 'lucide:chevrons-down' }

const resolved = computed(() => resolveRoll(props.modelValue, props.stats, props.character))
const visibleError = computed(() => (resolved.value.error && resolved.value.error !== NO_DICE ? resolved.value.error : null))
const allowedKinds = computed(() => kindsFor(props.modelValue.source))
const currentSource = computed(() => sourceKey(props.modelValue.source))
const disadvantageRule = computed(() => Boolean(props.stats?.rollDefaults.disadvantageOnStrDex))
const showReason = computed(() => disadvantageRule.value && usesStrDex(props.modelValue, props.stats))

const weapons = computed(() => (props.stats?.weaponRolls ?? []).filter(weapon => weapon.equipped))
const spells = computed(() => props.stats?.spellRolls ?? [])
const selectedWeapon = computed(() => {
  const source = props.modelValue.source
  return source?.type === 'weapon' ? props.stats?.weaponRolls.find(weapon => weapon.itemId === source.itemId) ?? null : null
})
/** A saved roll may point at an unequipped or removed weapon or spell: keep it selectable so the editor shows it. */
const orphanSource = computed(() => {
  const source = props.modelValue.source
  if (source?.type === 'weapon' && !weapons.value.some(weapon => weapon.itemId === source.itemId)) {
    return { key: sourceKey(source), label: selectedWeapon.value ? `${selectedWeapon.value.name} (desequipada)` : 'Arma ausente' }
  }
  if (source?.type === 'spell' && !spells.value.some(spell => spell.spellId === source.spellId)) {
    return { key: sourceKey(source), label: 'Magia ausente' }
  }
  return null
})
const skills = computed(() => SKILLS.map(skill => ({ key: skill.key, name: props.stats?.skills[skill.key]?.name ?? skill.name })))

// The armor rule preselects disadvantage when the roll starts using FOR or DES; the player can change it.
// A FOR/DES roll the player already set to normal stays normal while they keep picking FOR/DES rolls.
const autoDisadvantage = ref(false)
function applyRollDefaults(next: RollConfig): RollConfig {
  if (!disadvantageRule.value) return next
  if (usesStrDex(next, props.stats)) {
    const playerClearedIt = !autoDisadvantage.value && usesStrDex(props.modelValue, props.stats)
    if (next.advantage === 'None' && !playerClearedIt) {
      autoDisadvantage.value = true
      return { ...next, advantage: 'Disadvantage' }
    }
  } else if (autoDisadvantage.value && next.advantage === 'Disadvantage') {
    autoDisadvantage.value = false
    return { ...next, advantage: 'None' }
  }
  return next
}

function update(patch: Partial<RollConfig>) {
  emit('update:modelValue', { ...props.modelValue, ...patch })
}
function selectSource(key: string) {
  const source = sourceFromKey(key)
  const kind = defaultKindFor(source, props.modelValue.kind, props.stats)
  emit('update:modelValue', applyRollDefaults({ ...props.modelValue, source, kind }))
}
function selectKind(kind: RollKind) {
  if (!allowedKinds.value.includes(kind)) return
  emit('update:modelValue', applyRollDefaults({ ...props.modelValue, kind }))
}
function selectAdvantage(advantage: AdvantageMode) {
  autoDisadvantage.value = false
  update({ advantage })
}
function setVersatile(versatile: boolean) {
  const source = props.modelValue.source
  if (source?.type !== 'weapon') return
  update({ source: { ...source, versatile } })
}
function setDice(dice: DiceCounts) { update({ dice }) }

function clampRepetitions(value: number) {
  return Math.min(MAX_REPETITIONS, Math.max(1, Math.trunc(Number.isFinite(value) ? value : 1)))
}
function setRepetitions(value: number) { update({ repetitions: clampRepetitions(value) }) }

function addExtra() {
  if (props.modelValue.extraModifiers.length >= MAX_EXTRAS) return
  update({ extraModifiers: [...props.modelValue.extraModifiers, { label: null, value: 1 }] })
}
function editExtra(index: number, patch: { label?: string | null; value?: number }) {
  const extras = props.modelValue.extraModifiers.map((extra, i) => (i === index ? { ...extra, ...patch } : extra))
  update({ extraModifiers: extras })
}
function extraValue(raw: string) {
  const value = Math.trunc(Number(raw))
  return Number.isFinite(value) ? Math.min(MAX_EXTRA, Math.max(MIN_EXTRA, value)) : 0
}
function removeExtra(index: number) {
  update({ extraModifiers: props.modelValue.extraModifiers.filter((_, i) => i !== index) })
}

function formatBonus(value: number) { return value >= 0 ? `+${value}` : `${value}` }
</script>

<template>
  <div class="roll-config">
    <div class="roll-config-row">
      <label class="sheet-field roll-source">
        <span>Fonte</span>
        <select
          data-source-select
          :value="currentSource"
          :disabled="disabled"
          @change="selectSource(($event.target as HTMLSelectElement).value)"
        >
          <option value="none" data-source="none">Nenhuma (rolagem livre)</option>
          <optgroup v-if="weapons.length || orphanSource?.key.startsWith('weapon:')" label="Armas equipadas">
            <option v-for="weapon in weapons" :key="weapon.itemId" :value="`weapon:${weapon.itemId}`" :data-source="`weapon:${weapon.itemId}`">
              {{ weapon.name }} ({{ formatBonus(weapon.attackBonus) }})
            </option>
            <option v-if="orphanSource?.key.startsWith('weapon:')" :value="orphanSource.key" :data-source="orphanSource.key">{{ orphanSource.label }}</option>
          </optgroup>
          <optgroup v-if="spells.length || orphanSource?.key.startsWith('spell:')" label="Magias">
            <option v-for="spell in spells" :key="spell.spellId" :value="`spell:${spell.spellId}`" :data-source="`spell:${spell.spellId}`">{{ spell.name }}</option>
            <option v-if="orphanSource?.key.startsWith('spell:')" :value="orphanSource.key" :data-source="orphanSource.key">{{ orphanSource.label }}</option>
          </optgroup>
          <optgroup label="Atributos">
            <option v-for="code in ABILITY_ORDER" :key="code" :value="`ability:${code}`" :data-source="`ability:${code}`">
              {{ ABILITY_NAME[code] }} ({{ formatBonus(stats?.abilities[abilityKey(code)]?.modifier ?? 0) }})
            </option>
          </optgroup>
          <optgroup label="Perícias">
            <option v-for="skill in skills" :key="skill.key" :value="`skill:${skill.key}`" :data-source="`skill:${skill.key}`">
              {{ skill.name }} ({{ formatBonus(stats?.skills[skill.key]?.bonus ?? 0) }})
            </option>
          </optgroup>
          <optgroup label="Salvaguardas">
            <option v-for="code in ABILITY_ORDER" :key="code" :value="`save:${code}`" :data-source="`save:${code}`">
              Salvaguarda de {{ ABILITY_LABEL[code] }} ({{ formatBonus(stats?.savingThrows[abilityKey(code)]?.bonus ?? 0) }})
            </option>
          </optgroup>
        </select>
      </label>
    </div>

    <div class="roll-segment" role="radiogroup" aria-label="Tipo de rolagem">
      <button
        v-for="kind in KIND_ORDER"
        :key="kind"
        type="button"
        role="radio"
        class="roll-chip"
        :class="{ selected: modelValue.kind === kind }"
        :data-kind="kind.toLowerCase()"
        :aria-checked="modelValue.kind === kind"
        :disabled="disabled || !allowedKinds.includes(kind)"
        @click="selectKind(kind)"
      >{{ KIND_LABEL[kind] }}</button>
    </div>

    <label v-if="selectedWeapon?.versatile && modelValue.kind === 'Damage'" class="roll-toggle">
      <input
        type="checkbox"
        data-versatile
        :checked="modelValue.source?.type === 'weapon' && Boolean(modelValue.source.versatile)"
        :disabled="disabled"
        @change="setVersatile(($event.target as HTMLInputElement).checked)"
      >
      <span>Duas mãos (versátil: {{ selectedWeapon.versatile.dice }})</span>
    </label>

    <DicePad :model-value="modelValue.dice" :disabled="disabled" @update:model-value="setDice" />
    <p v-if="modelValue.kind === 'Check' || modelValue.kind === 'Attack'" class="roll-config-hint">
      <Icon name="lucide:info" />O d20 do {{ modelValue.kind === 'Check' ? 'teste' : 'ataque' }} já entra na expressão; os dados acima se somam a ele.
    </p>

    <div class="roll-segment three" role="radiogroup" aria-label="Vantagem ou desvantagem">
      <button
        v-for="mode in ADVANTAGES"
        :key="mode"
        type="button"
        role="radio"
        class="roll-chip"
        :class="{ selected: modelValue.advantage === mode }"
        :data-advantage="ADVANTAGE_KEY[mode]"
        :aria-checked="modelValue.advantage === mode"
        :disabled="disabled"
        @click="selectAdvantage(mode)"
      ><Icon :name="ADVANTAGE_ICON[mode]" />{{ ADVANTAGE_LABEL[mode] }}</button>
    </div>
    <p v-if="showReason" class="roll-config-warning" data-disadvantage-reason role="status">
      <Icon name="lucide:shield-alert" />{{ stats?.rollDefaults.reason || 'Desvantagem em FOR e DES.' }}
    </p>

    <div class="roll-extras">
      <div v-for="(extra, index) in modelValue.extraModifiers" :key="index" class="roll-extra" data-extra>
        <input
          class="roll-extra-label"
          type="text"
          :value="extra.label ?? ''"
          :maxlength="MAX_LABEL_LENGTH"
          placeholder="Rótulo (opcional)"
          aria-label="Rótulo do adicional"
          data-extra-label
          :disabled="disabled"
          @input="editExtra(index, { label: ($event.target as HTMLInputElement).value || null })"
        >
        <input
          class="roll-extra-value"
          type="number"
          inputmode="numeric"
          :min="MIN_EXTRA"
          :max="MAX_EXTRA"
          :value="extra.value"
          aria-label="Valor do adicional"
          data-extra-value
          :disabled="disabled"
          @input="editExtra(index, { value: extraValue(($event.target as HTMLInputElement).value) })"
        >
        <button class="icon-button" type="button" aria-label="Remover adicional" title="Remover adicional" data-extra-remove :disabled="disabled" @click="removeExtra(index)">
          <Icon name="lucide:x" />
        </button>
      </div>
      <button
        v-if="modelValue.extraModifiers.length < MAX_EXTRAS"
        class="button ghost small"
        type="button"
        data-add-extra
        :disabled="disabled"
        @click="addExtra"
      ><Icon name="lucide:plus" />Adicional</button>
    </div>

    <div class="roll-repetitions" role="group" aria-label="Repetições">
      <span class="roll-repetitions-label">Repetições</span>
      <button class="icon-button" type="button" aria-label="Menos uma repetição" :disabled="disabled || modelValue.repetitions <= 1" @click="setRepetitions(modelValue.repetitions - 1)">
        <Icon name="lucide:minus" />
      </button>
      <input
        type="number"
        inputmode="numeric"
        min="1"
        :max="MAX_REPETITIONS"
        :value="modelValue.repetitions"
        aria-label="Repetições, de 1 a 20"
        data-repetitions
        :disabled="disabled"
        @change="setRepetitions(Number(($event.target as HTMLInputElement).value))"
      >
      <button class="icon-button" type="button" aria-label="Mais uma repetição" :disabled="disabled || modelValue.repetitions >= MAX_REPETITIONS" @click="setRepetitions(modelValue.repetitions + 1)">
        <Icon name="lucide:plus" />
      </button>
    </div>

    <div class="roll-expression" aria-live="polite">
      <span class="roll-expression-label">Expressão</span>
      <code data-expression>{{ resolved.expression }}</code>
      <span v-if="!resolved.expression" class="roll-expression-empty">Toque nos dados para montar a rolagem.</span>
    </div>
    <p v-if="visibleError" class="form-error" data-roll-error role="alert"><Icon name="lucide:circle-alert" />{{ visibleError }}</p>
  </div>
</template>
