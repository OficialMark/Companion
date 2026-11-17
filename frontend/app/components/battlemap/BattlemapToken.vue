<script setup lang="ts">
import { STATUS_CONDITION_OPTIONS } from '~/composables/useTokenAppearance'
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
  /** Profile photo URL; replaces the initials inside the token circle. */
  photo?: string | null
  isCurrentTurn?: boolean
  currentHitPoints?: number | null
  maxHitPoints?: number | null
  footprintSize?: number
}>(), { defeated: false, photo: null, isCurrentTurn: false, currentHitPoints: null, maxHitPoints: null, footprintSize: 1 })

// The token's visual radius/text/bars all scale with its footprint (FR-012),
// e.g. a 3x3 creature is drawn three cells wide, not squeezed into one.
const effectiveSize = computed(() => props.size * props.footprintSize)

const healthRatio = computed(() => {
  if (props.maxHitPoints == null || props.maxHitPoints <= 0) return null
  const current = props.currentHitPoints ?? 0
  return Math.min(1, Math.max(0, current / props.maxHitPoints))
})

const conditionAbbreviations: Record<string, string> = Object.fromEntries(
  STATUS_CONDITION_OPTIONS.map(option => [option.value, option.abbreviation]))
const conditionLabels: Record<string, string> = Object.fromEntries(
  STATUS_CONDITION_OPTIONS.map(option => [option.value, option.label]))
const statusConditions = computed(() => props.token.statusConditions ?? [])
const emit = defineEmits<{
  select: [token: BattlemapToken]
  dragstart: [event: PointerEvent, token: BattlemapToken]
}>()
// Small corner badge marking defeated (0 HP) combatants; drawn in SVG so it scales with zoom.
const badge = computed(() => Math.max(7, props.size * .17))
const radius = computed(() => effectiveSize.value * .37)
// Unique per token so several photo tokens can share one <svg>.
const clipId = computed(() => `token-photo-${props.token.participantId}`)
</script>

<template>
  <g
    data-battlemap-token
    :data-participant-id="token.participantId"
    class="battlemap-token"
    :class="[{ selected, pending, controllable, defeated, creature: token.isCreature, 'current-turn': isCurrentTurn }, `token-color-${token.colorKey}`]"
    :transform="`translate(${x} ${y})`"
    role="button"
    :tabindex="controllable ? 0 : -1"
    :aria-label="`${token.displayName}${defeated ? ' (caído)' : ''}, coluna ${(token.column ?? 0) + 1}, linha ${(token.row ?? 0) + 1}`"
    @keydown.enter.prevent="emit('select', token)"
    @keydown.space.prevent="emit('select', token)"
    @pointerdown.stop="emit('dragstart', $event, token)"
    @click.stop
  >
    <title>{{ token.displayName }}{{ defeated ? ' — caído' : '' }}</title>
    <circle
      v-if="isCurrentTurn"
      class="token-turn-ring"
      :r="effectiveSize * .49"
      role="img"
      :aria-label="`Turno de ${token.displayName}`"
    />
    <circle class="token-body" :r="radius" />
    <template v-if="photo">
      <clipPath :id="clipId"><circle :r="radius" /></clipPath>
      <image
        class="token-photo"
        :href="photo"
        :x="-radius" :y="-radius" :width="radius * 2" :height="radius * 2"
        preserveAspectRatio="xMidYMid slice"
        :clip-path="`url(#${clipId})`"
      />
      <circle class="token-photo-border" :r="radius" />
    </template>
    <circle class="token-ring" :r="effectiveSize * .43" />
    <text v-if="!photo" text-anchor="middle" dominant-baseline="central" :font-size="effectiveSize * .28">{{ token.initials }}</text>
    <svg
      v-if="healthRatio !== null"
      class="battlemap-health-bar"
      role="img"
      :aria-label="`Vida: ${currentHitPoints ?? 0} de ${maxHitPoints}`"
      :x="-effectiveSize * .45"
      :y="-effectiveSize * .62"
      :width="effectiveSize * .9"
      :height="effectiveSize * .16"
      viewBox="0 0 100 100"
      preserveAspectRatio="none"
    >
      <rect class="health-bg" width="100" height="100" />
      <rect data-health-fill class="health-fill" :width="`${Math.round(healthRatio * 100)}%`" height="100" />
    </svg>
    <g
      v-for="(condition, index) in statusConditions"
      :key="condition"
      data-status-condition
      class="token-condition-badge"
      :aria-label="conditionLabels[condition] ?? condition"
      role="img"
      :transform="`translate(${(index - (statusConditions.length - 1) / 2) * effectiveSize * .38} ${effectiveSize * .58})`"
    >
      <circle :r="effectiveSize * .17" />
      <text text-anchor="middle" dominant-baseline="central" :font-size="effectiveSize * .13">{{ conditionAbbreviations[condition] ?? condition.slice(0, 3).toUpperCase() }}</text>
    </g>
    <g v-if="defeated" class="token-defeated-badge" :transform="`translate(${effectiveSize * .27} ${-effectiveSize * .27})`">
      <circle :r="badge" />
      <path :d="`M ${-badge * .45} ${-badge * .45} L ${badge * .45} ${badge * .45} M ${badge * .45} ${-badge * .45} L ${-badge * .45} ${badge * .45}`" />
    </g>
  </g>
</template>
