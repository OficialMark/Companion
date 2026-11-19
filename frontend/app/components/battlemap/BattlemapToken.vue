<script setup lang="ts">
import { conditionListLabel, statusConditionInfo, visibleConditionBadges } from '~/utils/statusConditions'
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
  /** Master view only: this creature's HP is hidden from players (spec 007, US3). */
  hitPointsHidden?: boolean
  /** Master view only: the token's whole footprint is under fog (players do not see it). */
  fogged?: boolean
  /** The token opens a sheet on right-click / Shift+F10 (spec 010); announced to assistive tech. */
  hasSheet?: boolean
}>(), { defeated: false, photo: null, isCurrentTurn: false, currentHitPoints: null, maxHitPoints: null, footprintSize: 1, hitPointsHidden: false, fogged: false })

// The token's visual radius/text/bars all scale with its footprint (FR-012),
// e.g. a 3x3 creature is drawn three cells wide, not squeezed into one.
const effectiveSize = computed(() => props.size * props.footprintSize)

const healthRatio = computed(() => {
  if (props.maxHitPoints == null || props.maxHitPoints <= 0) return null
  const current = props.currentHitPoints ?? 0
  return Math.min(1, Math.max(0, current / props.maxHitPoints))
})

const statusConditions = computed(() => props.token.statusConditions ?? [])
const conditionBadges = computed(() => visibleConditionBadges(statusConditions.value))
// Badges are sized from the CELL (not the footprint) so a 5x5 dragon does not get
// giant icons, with a floor that keeps the glyph readable when zoomed far out.
const conditionRadius = computed(() => Math.min(16, Math.max(8, props.size * .17)) * Math.sqrt(props.footprintSize))
const conditionSlots = computed(() => conditionBadges.value.shown.length + (conditionBadges.value.overflow ? 1 : 0))
function conditionX(index: number) {
  const step = conditionRadius.value * 2.15
  return (index - (conditionSlots.value - 1) / 2) * step
}
const conditionY = computed(() => effectiveSize.value * .43)

// Badge glyphs (spec 008, US1). The Lucide icon renders as a nested <svg> sized in `em`
// (CSS width:100% does NOT apply to a nested svg), so sizing it with x/y/width/height
// left the glyph a fixed 17.6 px anchored at the box corner: off-center at every zoom but
// one. Instead the icon is pinned to its native 24-unit box (font-size 24px => 1em = 24
// user units) and placed purely by transform, the same math that places the circle.
const GLYPH_BOX = 24
function glyphTransform(size: number) {
  return `translate(${-size / 2} ${-size / 2}) scale(${size / GLYPH_BOX})`
}
const accessibleLabel = computed(() => {
  const parts = [props.token.displayName]
  if (props.defeated) parts.push('caído')
  if (healthRatio.value !== null) parts.push(`vida ${props.currentHitPoints ?? 0} de ${props.maxHitPoints}${props.hitPointsHidden ? ' (oculta aos jogadores)' : ''}`)
  if (statusConditions.value.length) parts.push(`condições: ${conditionListLabel(statusConditions.value)}`)
  parts.push(`coluna ${(props.token.column ?? 0) + 1}, linha ${(props.token.row ?? 0) + 1}`)
  if (props.hasSheet) parts.push('Shift+F10 abre a ficha')
  return parts.join(', ')
})
const emit = defineEmits<{
  select: [token: BattlemapToken]
  dragstart: [event: PointerEvent, token: BattlemapToken]
  /** Keyboard equivalent of a right-click (Shift+F10 or the Menu key). */
  context: [token: BattlemapToken]
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
    :class="[{ selected, pending, controllable, defeated, fogged, creature: token.isCreature, 'current-turn': isCurrentTurn }, `token-color-${token.colorKey}`]"
    :transform="`translate(${x} ${y})`"
    role="button"
    :tabindex="controllable ? 0 : -1"
    :aria-label="accessibleLabel"
    @keydown.enter.prevent="emit('select', token)"
    @keydown.space.prevent="emit('select', token)"
    @keydown.shift.exact.f10.prevent="emit('context', token)"
    @keydown.context-menu.prevent="emit('context', token)"
    @pointerdown.stop="emit('dragstart', $event, token)"
    @click.stop
  >
    <title>{{ accessibleLabel }}</title>
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
      :class="{ 'hidden-from-players': hitPointsHidden }"
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
      <rect data-health-fill class="health-fill" :class="{ low: healthRatio <= .25, mid: healthRatio > .25 && healthRatio <= .5 }" :width="`${Math.round(healthRatio * 100)}%`" height="100" />
    </svg>
    <g
      v-if="healthRatio !== null && hitPointsHidden"
      data-hp-hidden-indicator
      class="token-hp-hidden"
      :transform="`translate(${-effectiveSize * .45 - badge * .9} ${-effectiveSize * .54})`"
    >
      <title>HP oculto aos jogadores</title>
      <circle :r="badge * .85" />
      <g class="token-glyph" :transform="glyphTransform(badge * 1.1)"><Icon name="lucide:eye-off" :size="`${GLYPH_BOX}px`" /></g>
    </g>
    <g
      v-for="(condition, index) in conditionBadges.shown"
      :key="condition"
      data-status-condition
      :data-condition="condition"
      class="token-condition-badge"
      :style="{ '--condition-color': statusConditionInfo(condition).color }"
      :aria-label="statusConditionInfo(condition).label"
      role="img"
      :transform="`translate(${conditionX(index)} ${conditionY})`"
    >
      <title>{{ statusConditionInfo(condition).label }}</title>
      <circle :r="conditionRadius" />
      <g class="token-glyph" :transform="glyphTransform(conditionRadius * 1.24)"><Icon :name="statusConditionInfo(condition).icon" :size="`${GLYPH_BOX}px`" /></g>
    </g>
    <g
      v-if="conditionBadges.overflow"
      data-status-condition-overflow
      class="token-condition-badge overflow"
      role="img"
      :aria-label="`Mais ${conditionBadges.overflow}: ${conditionListLabel(statusConditions.slice(conditionBadges.shown.length))}`"
      :transform="`translate(${conditionX(conditionBadges.shown.length)} ${conditionY})`"
    >
      <title>{{ conditionListLabel(statusConditions.slice(conditionBadges.shown.length)) }}</title>
      <circle :r="conditionRadius" />
      <text text-anchor="middle" dominant-baseline="central" :font-size="conditionRadius * .95">+{{ conditionBadges.overflow }}</text>
    </g>
    <g v-if="defeated" class="token-defeated-badge" :transform="`translate(${effectiveSize * .27} ${-effectiveSize * .27})`">
      <circle :r="badge" />
      <path :d="`M ${-badge * .45} ${-badge * .45} L ${badge * .45} ${badge * .45} M ${badge * .45} ${-badge * .45} L ${-badge * .45} ${badge * .45}`" />
    </g>
  </g>
</template>
