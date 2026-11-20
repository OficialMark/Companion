<script setup lang="ts">
import type { StatPart } from '~/types/character'

/**
 * A calculated value of the sheet (spec 011, FR-007): read-only, with a button that reveals where
 * the number comes from (base and every contribution, naming the item or rule).
 */
const props = defineProps<{
  name: string
  label: string
  value: number | string
  parts: StatPart[]
  stale?: boolean
  signed?: boolean
  /** Spoken name when the visible label alone is ambiguous ("Modificador" appears in six cards). */
  accessibleLabel?: string
  /** Value-only button for dense rows (saves, skills): smaller, still a 44 px touch target. */
  compact?: boolean
}>()

const open = ref(false)
const shown = computed(() => {
  if (typeof props.value !== 'number' || !props.signed) return String(props.value)
  return props.value >= 0 ? `+${props.value}` : String(props.value)
})
const spoken = computed(() => props.accessibleLabel || props.label)
const id = `derived-${props.name}`
function signed(value: number) { return value >= 0 ? `+${value}` : String(value) }
</script>

<template>
  <div class="derived-stat" :class="{ 'derived-stat--compact': compact }" :data-derived="name" :data-stale="stale ? '' : undefined">
    <button
      type="button"
      class="derived-stat-button"
      :aria-expanded="open"
      :aria-controls="id"
      :aria-label="`${spoken}: ${shown}. Ver composição`"
      @click="open = !open"
    >
      <span v-if="!compact" class="derived-stat-label">{{ label }}</span>
      <strong class="derived-stat-value" data-value>{{ shown }}</strong>
    </button>
    <div v-if="open" :id="id" class="derived-stat-popover" role="region" :aria-label="`Composição de ${spoken}`" data-breakdown>
      <ul>
        <li v-for="(part, index) in parts" :key="index">
          <span>{{ part.source }}</span><strong>{{ signed(part.value) }}</strong>
        </li>
        <li v-if="!parts.length" class="derived-stat-empty"><span>Nenhum bônus</span><strong>+0</strong></li>
      </ul>
      <p v-if="stale" class="derived-stat-stale">Atualizando…</p>
    </div>
  </div>
</template>
