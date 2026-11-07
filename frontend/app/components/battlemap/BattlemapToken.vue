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
  defeated?: boolean
}>(), { defeated: false })
const emit = defineEmits<{
  select: [token: BattlemapToken]
  dragstart: [event: PointerEvent, token: BattlemapToken]
}>()
// Small corner badge marking defeated (0 HP) combatants; drawn in SVG so it scales with zoom.
const badge = computed(() => Math.max(7, props.size * .17))
</script>

<template>
  <g
    data-battlemap-token
    :data-participant-id="token.participantId"
    class="battlemap-token"
    :class="[{ selected, pending, controllable, defeated, creature: token.isCreature }, `token-color-${token.colorKey}`]"
    :transform="`translate(${x} ${y})`"
    role="button"
    :tabindex="controllable ? 0 : -1"
    :aria-label="`${token.displayName}${defeated ? ' (caído)' : ''}, coluna ${(token.column ?? 0) + 1}, linha ${(token.row ?? 0) + 1}`"
    @keydown.enter.prevent="emit('select', token)"
    @keydown.space.prevent="emit('select', token)"
    @pointerdown.stop="emit('dragstart', $event, token)"
  >
    <title>{{ token.displayName }}{{ defeated ? ' — caído' : '' }}</title>
    <circle :r="size * .37" />
    <circle class="token-ring" :r="size * .43" />
    <text text-anchor="middle" dominant-baseline="central" :font-size="size * .28">{{ token.initials }}</text>
    <g v-if="defeated" class="token-defeated-badge" :transform="`translate(${size * .27} ${-size * .27})`">
      <circle :r="badge" />
      <path :d="`M ${-badge * .45} ${-badge * .45} L ${badge * .45} ${badge * .45} M ${badge * .45} ${-badge * .45} L ${-badge * .45} ${badge * .45}`" />
    </g>
  </g>
</template>
