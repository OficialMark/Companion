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
}>(), { isCurrentTurn: false })
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
    <text text-anchor="middle" dominant-baseline="central" :font-size="size * .28">{{ token.initials }}</text>
  </g>
</template>
