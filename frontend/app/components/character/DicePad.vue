<script setup lang="ts">
import type { DieKey } from '~/types/character'
import { useLongPress } from '~/composables/useLongPress'
import { MAX_DICE_PER_TYPE, type DiceCounts } from '~/utils/playerRoll'

/**
 * Dice pad of the roll panel and the saved roll editor (spec 011, FR-026): a tap adds one die,
 * holding for 0.5 s removes one, and Delete, Backspace or "-" on the focused die removes one.
 */
const props = withDefaults(defineProps<{ modelValue: DiceCounts; disabled?: boolean }>(), { disabled: false })
const emit = defineEmits<{ 'update:modelValue': [value: DiceCounts] }>()

const DICE: DieKey[] = ['d4', 'd6', 'd8', 'd10', 'd12', 'd20', 'd100']
const REMOVE_KEYS = new Set(['Delete', 'Backspace', '-', 'Subtract'])

const count = (die: DieKey) => props.modelValue[die] ?? 0
const total = computed(() => DICE.reduce((sum, die) => sum + count(die), 0))

function set(die: DieKey, next: number) {
  const value: DiceCounts = { ...props.modelValue }
  if (next > 0) value[die] = next
  else delete value[die]
  emit('update:modelValue', value)
}
function add(die: DieKey) {
  if (props.disabled || count(die) >= MAX_DICE_PER_TYPE) return
  set(die, count(die) + 1)
}
function remove(die: DieKey) {
  if (props.disabled || count(die) <= 0) return
  set(die, count(die) - 1)
}
function clear() {
  if (props.disabled) return
  emit('update:modelValue', {})
}

const press = useLongPress<DieKey>(remove)
function onClick(die: DieKey) {
  if (press.consumeClick()) return
  add(die)
}
function onKeydown(event: KeyboardEvent, die: DieKey) {
  if (!REMOVE_KEYS.has(event.key)) return
  event.preventDefault()
  remove(die)
}
function ariaLabel(die: DieKey) {
  const n = count(die)
  const selected = n === 0 ? 'nenhum selecionado' : n === 1 ? '1 selecionado' : `${n} selecionados`
  return `${die}, ${selected}. Clique soma, segure para tirar`
}
</script>

<template>
  <div class="dice-pad" role="group" aria-label="Dados">
    <div class="dice-pad-grid">
      <button
        v-for="die in DICE"
        :key="die"
        type="button"
        class="dice-pad-die"
        :class="{ active: count(die) > 0 }"
        :data-die="die"
        :data-count="count(die)"
        :aria-label="ariaLabel(die)"
        :title="ariaLabel(die)"
        :disabled="disabled"
        v-bind="press.bind(die)"
        @click="onClick(die)"
        @keydown="onKeydown($event, die)"
      >
        <span class="dice-pad-face">{{ die }}</span>
        <span v-if="count(die) > 0" class="dice-pad-badge" aria-hidden="true">{{ count(die) }}</span>
      </button>
    </div>
    <button
      type="button"
      class="button ghost small dice-pad-clear"
      data-clear-dice
      :disabled="disabled || total === 0"
      @click="clear"
    ><Icon name="lucide:eraser" />Limpar dados</button>
  </div>
</template>
