<script setup lang="ts">
import type { BattlemapSnapshot, BattlemapToken } from '~/types/campaign'
import { useBattlemapViewport } from '~/composables/useBattlemapViewport'
import BattlemapTokenMarker from './BattlemapToken.vue'

const props = withDefaults(defineProps<{
  battlemap: BattlemapSnapshot
  imageSource: string
  currentParticipantId: string
  isMaster: boolean
  online: boolean
  pendingParticipantId?: string | null
}>(), { pendingParticipantId: null })
const emit = defineEmits<{ move: [participantId: string, column: number | null, row: number | null] }>()

const columns = computed(() => props.battlemap.columns)
const rows = computed(() => props.battlemap.rows)
const viewport = useBattlemapViewport(columns, rows)
const surface = ref<SVGSVGElement | null>(null)
const selectedParticipantId = ref<string | null>(null)
const cursor = reactive({ column: 0, row: 0 })
const dragParticipantId = ref<string | null>(null)
const panPointer = reactive({ active: false, x: 0, y: 0, moved: false })
let observer: ResizeObserver | null = null

const verticalLines = computed(() => Array.from({ length: columns.value + 1 }, (_, index) => index))
const horizontalLines = computed(() => Array.from({ length: rows.value + 1 }, (_, index) => index))
const positionedTokens = computed(() => props.battlemap.tokens.filter(token => token.column !== null && token.row !== null))
const reserveTokens = computed(() => props.battlemap.tokens.filter(token => token.column === null || token.row === null))
const selectedToken = computed(() =>
  props.battlemap.tokens.find(token => token.participantId === selectedParticipantId.value) ?? null)
const mapWidth = computed(() => columns.value * viewport.cellSize.value)
const mapHeight = computed(() => rows.value * viewport.cellSize.value)

function controllable(token: BattlemapToken) {
  return props.isMaster || (!token.isCreature && token.participantId === props.currentParticipantId)
}

function selectToken(token: BattlemapToken) {
  if (!props.online || !controllable(token)) return
  selectedParticipantId.value = token.participantId
  if (token.column !== null && token.row !== null) {
    cursor.column = token.column
    cursor.row = token.row
  }
}

function cellFromEvent(event: MouseEvent | PointerEvent) {
  const rect = surface.value?.getBoundingClientRect()
  const left = rect?.left ?? 0
  const top = rect?.top ?? 0
  const scaleX = rect && rect.width > 0 ? viewport.viewportWidth.value / rect.width : 1
  const scaleY = rect && rect.height > 0 ? viewport.viewportHeight.value / rect.height : 1
  const x = (event.clientX - left) * scaleX + viewport.pan.x
  const y = (event.clientY - top) * scaleY + viewport.pan.y
  return {
    column: Math.floor(x / viewport.cellSize.value),
    row: Math.floor(y / viewport.cellSize.value)
  }
}

function isInsideMap(column: number, row: number) {
  return column >= 0 && column < columns.value && row >= 0 && row < rows.value
}

function commitCell(column: number, row: number) {
  if (!props.online || !selectedParticipantId.value) return
  if (!isInsideMap(column, row)) return
  cursor.column = column
  cursor.row = row
  emit('move', selectedParticipantId.value, column, row)
  selectedParticipantId.value = null
}

function clickSurface(event: MouseEvent) {
  if (panPointer.moved) {
    panPointer.moved = false
    return
  }
  const cell = cellFromEvent(event)
  commitCell(cell.column, cell.row)
}

function startTokenDrag(event: PointerEvent, token: BattlemapToken) {
  selectToken(token)
  if (!props.online || !controllable(token)) return
  dragParticipantId.value = token.participantId
  surface.value?.setPointerCapture?.(event.pointerId)
}

function pointerDown(event: PointerEvent) {
  if (selectedParticipantId.value || dragParticipantId.value) return
  panPointer.active = true
  panPointer.x = event.clientX
  panPointer.y = event.clientY
  panPointer.moved = false
  surface.value?.setPointerCapture?.(event.pointerId)
}

function pointerMove(event: PointerEvent) {
  if (!panPointer.active) return
  const dx = panPointer.x - event.clientX
  const dy = panPointer.y - event.clientY
  if (Math.abs(dx) + Math.abs(dy) > 2) panPointer.moved = true
  viewport.moveBy(dx, dy)
  panPointer.x = event.clientX
  panPointer.y = event.clientY
}

function pointerUp(event: PointerEvent) {
  if (dragParticipantId.value) {
    const cell = cellFromEvent(event)
    selectedParticipantId.value = dragParticipantId.value
    dragParticipantId.value = null
    if (!isInsideMap(cell.column, cell.row)) {
      emit('move', selectedParticipantId.value, null, null)
      selectedParticipantId.value = null
      return
    }
    commitCell(cell.column, cell.row)
  }
  panPointer.active = false
}

function wheel(event: WheelEvent) {
  event.preventDefault()
  const rect = surface.value?.getBoundingClientRect()
  viewport.zoom(event.deltaY < 0 ? 1.15 : 1 / 1.15, event.clientX - (rect?.left ?? 0), event.clientY - (rect?.top ?? 0))
}

function keydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    selectedParticipantId.value = null
    return
  }
  const delta = event.shiftKey ? 5 : 1
  if (event.key === 'ArrowLeft') cursor.column = Math.max(0, cursor.column - delta)
  else if (event.key === 'ArrowRight') cursor.column = Math.min(columns.value - 1, cursor.column + delta)
  else if (event.key === 'ArrowUp') cursor.row = Math.max(0, cursor.row - delta)
  else if (event.key === 'ArrowDown') cursor.row = Math.min(rows.value - 1, cursor.row + delta)
  else if (event.key === 'Enter' || event.key === ' ') commitCell(cursor.column, cursor.row)
  else return
  event.preventDefault()
}

function stackOffset(token: BattlemapToken) {
  const sameCell = positionedTokens.value.filter(item => item.column === token.column && item.row === token.row)
  const index = sameCell.findIndex(item => item.participantId === token.participantId)
  const distance = Math.min(10, viewport.cellSize.value * .2)
  return {
    x: ((index % 2) - .5) * distance,
    y: (Math.floor(index / 2) - .5) * distance
  }
}

onMounted(() => {
  viewport.reset()
  if (surface.value && typeof ResizeObserver !== 'undefined') {
    observer = new ResizeObserver(entries => {
      const rect = entries[0]?.contentRect
      if (rect) viewport.resize(rect.width, rect.height)
    })
    observer.observe(surface.value)
  }
})
onBeforeUnmount(() => observer?.disconnect())
</script>

<template>
  <div class="battlemap-shell">
    <div class="battlemap-toolbar" aria-label="Controles do mapa">
      <button class="icon-button" type="button" aria-label="Diminuir zoom" @click="viewport.zoom(1 / 1.2)">−</button>
      <span>{{ Math.round(viewport.cellSize.value) }} px</span>
      <button class="icon-button" type="button" aria-label="Aumentar zoom" @click="viewport.zoom(1.2)">+</button>
      <button class="button ghost" type="button" @click="viewport.fit">Ajustar mapa</button>
      <button class="button ghost" type="button" @click="viewport.reset">Escala 40 px</button>
    </div>

    <svg
      ref="surface"
      data-board-surface
      class="battlemap-board"
      :viewBox="`0 0 ${viewport.viewportWidth.value} ${viewport.viewportHeight.value}`"
      role="application"
      tabindex="0"
      :aria-label="`Battlemap com ${columns} colunas e ${rows} linhas`"
      @click="clickSurface"
      @pointerdown="pointerDown"
      @pointermove="pointerMove"
      @pointerup="pointerUp"
      @pointercancel="pointerUp"
      @wheel="wheel"
      @keydown="keydown"
    >
      <defs>
        <clipPath :id="`battlemap-clip-${battlemap.id}`">
          <rect :width="mapWidth" :height="mapHeight" />
        </clipPath>
      </defs>
      <g :transform="`translate(${-viewport.pan.x} ${-viewport.pan.y})`">
        <image
          :href="imageSource"
          :width="mapWidth"
          :height="mapHeight"
          preserveAspectRatio="xMidYMid slice"
          :clip-path="`url(#battlemap-clip-${battlemap.id})`"
        />
        <rect class="battlemap-boundary" :width="mapWidth" :height="mapHeight" />
        <line
          v-if="battlemap.isGridVisible"
          v-for="line in verticalLines"
          :key="`v-${line}`"
          data-grid-line
          class="battlemap-grid-line"
          :x1="line * viewport.cellSize.value"
          y1="0"
          :x2="line * viewport.cellSize.value"
          :y2="mapHeight"
        />
        <line
          v-if="battlemap.isGridVisible"
          v-for="line in horizontalLines"
          :key="`h-${line}`"
          data-grid-line
          class="battlemap-grid-line"
          x1="0"
          :y1="line * viewport.cellSize.value"
          :x2="mapWidth"
          :y2="line * viewport.cellSize.value"
        />
        <rect
          v-if="selectedToken"
          class="battlemap-cell-selection"
          :x="cursor.column * viewport.cellSize.value"
          :y="cursor.row * viewport.cellSize.value"
          :width="viewport.cellSize.value"
          :height="viewport.cellSize.value"
        />
        <BattlemapTokenMarker
          v-for="token in positionedTokens"
          :key="token.participantId"
          :token="token"
          :x="(token.column! + .5) * viewport.cellSize.value + stackOffset(token).x"
          :y="(token.row! + .5) * viewport.cellSize.value + stackOffset(token).y"
          :size="viewport.cellSize.value"
          :selected="selectedParticipantId === token.participantId"
          :controllable="controllable(token)"
          :pending="pendingParticipantId === token.participantId"
          @select="selectToken"
          @dragstart="startTokenDrag"
        />
      </g>
    </svg>

    <div v-if="reserveTokens.length" class="battlemap-reserve" aria-label="Pinos na reserva">
      <span>Reserva</span>
      <button
        v-for="token in reserveTokens"
        :key="token.participantId"
        :data-participant-id="token.participantId"
        class="reserve-token"
        :class="{ selected: selectedParticipantId === token.participantId }"
        type="button"
        :disabled="!online || !controllable(token)"
        @click="selectToken(token)"
      >{{ token.initials }} <span>{{ token.displayName }}</span></button>
    </div>
    <p class="battlemap-status" aria-live="polite">
      <template v-if="!online">Conexão necessária para mover pinos.</template>
      <template v-else-if="selectedToken">Selecione a célula de destino para {{ selectedToken.displayName }}. Coluna {{ cursor.column + 1 }}, linha {{ cursor.row + 1 }}.</template>
      <template v-else>Arraste seu pino ou selecione-o e depois escolha uma célula. Arraste o fundo para navegar.</template>
    </p>
  </div>
</template>
