<script setup lang="ts">
import type { BattlemapToken } from '~/types/campaign'

const props = withDefaults(defineProps<{
  token: BattlemapToken
  x: number
  y: number
  size: number
  selected: boolean
  controllable: boolean
  pending: boolean
  isCurrentTurn?: boolean
  currentHitPoints?: number | null
  maxHitPoints?: number | null
}>(), { isCurrentTurn: false, currentHitPoints: null, maxHitPoints: null })

const healthRatio = computed(() => {
  if (props.maxHitPoints == null || props.maxHitPoints <= 0) return null
  const current = props.currentHitPoints ?? 0
  return Math.min(1, Math.max(0, current / props.maxHitPoints))
})
const emit = defineEmits<{
  select: [token: BattlemapToken]
  dragstart: [event: PointerEvent, token: BattlemapToken]
}>()
</script>

<template>
  <g
    data-battlemap-token
    :data-participant-id="token.participantId"
    class="battlemap-token"
    :class="[{ selected, pending, controllable, 'current-turn': isCurrentTurn }, `token-color-${token.colorKey}`]"
    :transform="`translate(${x} ${y})`"
    role="button"
    :tabindex="controllable ? 0 : -1"
    :aria-label="`${token.displayName}, coluna ${(token.column ?? 0) + 1}, linha ${(token.row ?? 0) + 1}`"
    @keydown.enter.prevent="emit('select', token)"
    @keydown.space.prevent="emit('select', token)"
    @pointerdown.stop="emit('dragstart', $event, token)"
  >
    <circle
      v-if="isCurrentTurn"
      class="token-turn-ring"
      :r="size * .49"
      role="img"
      :aria-label="`Turno de ${token.displayName}`"
    />
    <circle :r="size * .37" />
    <circle class="token-ring" :r="size * .43" />
    <svg
      v-if="healthRatio !== null"
      class="battlemap-health-bar"
      role="img"
      :aria-label="`Vida: ${currentHitPoints ?? 0} de ${maxHitPoints}`"
      :x="-size * .45"
      :y="-size * .62"
      :width="size * .9"
      :height="size * .16"
      viewBox="0 0 100 100"
      preserveAspectRatio="none"
    >
      <rect class="health-bg" width="100" height="100" />
      <rect data-health-fill class="health-fill" :width="`${Math.round(healthRatio * 100)}%`" height="100" />
    </svg>
    <text text-anchor="middle" dominant-baseline="central" :font-size="size * .28">{{ token.initials }}</text>
  </g>
</template>
