<script setup lang="ts">
import { useFogOfWar } from '~/composables/useFogOfWar'
import { encodeMask, fillMask, maskPath } from '~/utils/fogMask'
import FogToolbar from './FogToolbar.vue'

/**
 * Fog editor over the LOCAL image preview, before the map is published (spec 008, US2).
 * Nothing here talks to the server: the encoded mask is v-modeled to the setup form and
 * sent together with the image, so players never receive the map uncovered.
 */
const props = defineProps<{
  imageUrl: string
  columns: number
  rows: number
  isGridVisible: boolean
  /** Base64 row-major bitset, null = fully revealed (same format as the board). */
  modelValue: string | null
}>()
const emit = defineEmits<{ 'update:modelValue': [mask: string | null] }>()

// Map units per cell inside the preview's own viewBox; the SVG scales to its box.
const UNIT = 24
const columns = computed(() => props.columns)
const rows = computed(() => props.rows)
const fog = useFogOfWar(computed(() => props.modelValue), columns, rows)
const surface = ref<SVGSVGElement | null>(null)
const width = computed(() => props.columns * UNIT)
const height = computed(() => props.rows * UNIT)
const fogPath = computed(() => maskPath(fog.mask.value, props.columns, props.rows, UNIT))
const coveredLabel = computed(() => {
  let covered = 0
  const total = props.columns * props.rows
  for (let index = 0; index < total; index++) if (fog.mask.value[index >> 3]! & (1 << (index & 7))) covered++
  return covered === 0 ? 'Mapa todo revelado' : covered === total ? 'Mapa todo coberto' : `${Math.round(covered / total * 100)}% coberto`
})
// Keep the grid proportion while capping the height, so tall maps still fit on a phone.
const surfaceStyle = computed(() => ({
  aspectRatio: `${props.columns} / ${props.rows}`,
  width: `min(100%, ${Math.round(56 * props.columns / props.rows)}svh)`
}))
let pointerId: number | null = null

function cellFrom(event: PointerEvent) {
  const rect = surface.value!.getBoundingClientRect()
  const x = rect.width > 0 ? (event.clientX - rect.left) / rect.width * props.columns : 0
  const y = rect.height > 0 ? (event.clientY - rect.top) / rect.height * props.rows : 0
  return { column: Math.floor(x), row: Math.floor(y) }
}

function down(event: PointerEvent) {
  if (event.button !== 0 || pointerId !== null) {
    // A second finger cancels the stroke, like on the board.
    if (pointerId !== null && event.pointerId !== pointerId) cancel()
    return
  }
  pointerId = event.pointerId
  surface.value?.setPointerCapture?.(event.pointerId)
  fog.begin(cellFrom(event))
}
function move(event: PointerEvent) {
  if (event.pointerId === pointerId) fog.extend(cellFrom(event))
}
function up(event: PointerEvent) {
  if (event.pointerId !== pointerId) return
  pointerId = null
  const mask = fog.end()
  if (mask !== undefined) emit('update:modelValue', mask)
}
function cancel() {
  pointerId = null
  fog.cancel()
}
function fillAll(covered: boolean) {
  emit('update:modelValue', encodeMask(fillMask(props.columns, props.rows, covered)))
}
</script>

<template>
  <div class="fog-preview" data-fog-preview>
    <FogToolbar
      v-model:mode="fog.mode.value"
      v-model:tool="fog.tool.value"
      v-model:brush-size="fog.brushSize.value"
      :closable="false"
      context="preview"
      @cover-all="fillAll(true)"
      @reveal-all="fillAll(false)"
    />
    <svg
      ref="surface"
      class="fog-preview-surface"
      :style="surfaceStyle"
      :viewBox="`0 0 ${width} ${height}`"
      preserveAspectRatio="none"
      role="img"
      :aria-label="`Prévia do mapa com névoa: ${coveredLabel}`"
      @pointerdown="down"
      @pointermove="move"
      @pointerup="up"
      @pointercancel="cancel"
      @lostpointercapture="up"
      @contextmenu.prevent
    >
      <image :href="imageUrl" :width="width" :height="height" preserveAspectRatio="none" />
      <template v-if="isGridVisible">
        <line v-for="column in columns + 1" :key="`v${column}`" class="battlemap-grid-line" :x1="(column - 1) * UNIT" y1="0" :x2="(column - 1) * UNIT" :y2="height" />
        <line v-for="row in rows + 1" :key="`h${row}`" class="battlemap-grid-line" x1="0" :y1="(row - 1) * UNIT" :x2="width" :y2="(row - 1) * UNIT" />
      </template>
      <path v-if="fogPath" data-fog-preview-layer class="battlemap-fog master" :d="fogPath" fill="#1b1721" />
      <rect
        v-if="fog.rectPreview.value"
        class="fog-rect-preview"
        :class="fog.mode.value"
        :x="fog.rectPreview.value.column * UNIT"
        :y="fog.rectPreview.value.row * UNIT"
        :width="(fog.rectPreview.value.right - fog.rectPreview.value.column + 1) * UNIT"
        :height="(fog.rectPreview.value.bottom - fog.rectPreview.value.row + 1) * UNIT"
      />
    </svg>
    <p class="fog-preview-status" aria-live="polite"><Icon name="lucide:cloud-fog" />{{ coveredLabel }}. Os jogadores só verão o que estiver revelado.</p>
  </div>
</template>
