<script setup lang="ts">
import type { BattlemapSnapshot, BattlemapToken, Combatant, EncounterStatus, Participant } from '~/types/campaign'
import { useBattlemapViewport } from '~/composables/useBattlemapViewport'
import { useAttackAreas, type AttackAreaDraft } from '~/composables/useAttackAreas'
import { useTokenAppearance, type TokenAppearanceDraft } from '~/composables/useTokenAppearance'
import { attackAreaGeometry } from '~/utils/attackAreaGeometry'
import BattlemapTokenMarker from './BattlemapToken.vue'
import AttackAreaPanel from './AttackAreaPanel.vue'

const props = withDefaults(defineProps<{
  battlemap: BattlemapSnapshot
  imageSource: string
  currentParticipantId: string
  isMaster: boolean
  online: boolean
  pendingParticipantId?: string | null
  defeatedIds?: string[]
  /** participantId -> profile photo URL; player tokens with a photo show it instead of initials. */
  avatars?: Record<string, string>
  combatants?: Combatant[]
  encounterStatus?: EncounterStatus | null
  participants?: Participant[]
}>(), { pendingParticipantId: null, defeatedIds: () => [], avatars: () => ({}), combatants: () => [], encounterStatus: null, participants: () => [] })
const emit = defineEmits<{
  move: [participantId: string, column: number | null, row: number | null]
  'attack-area-commit': [tokenId: string, draft: AttackAreaDraft]
  'attack-area-remove': [tokenId: string]
  'token-appearance-commit': [tokenId: string, draft: TokenAppearanceDraft]
  'create-marker': [label: string]
  'marker-remove': [markerId: string]
}>()

const newMarkerLabel = ref('')
function submitNewMarker() {
  const label = newMarkerLabel.value.trim()
  if (!label) return
  emit('create-marker', label)
  newMarkerLabel.value = ''
}

const columns = computed(() => props.battlemap.columns)
const rows = computed(() => props.battlemap.rows)
const viewport = useBattlemapViewport(columns, rows)
const surface = ref<SVGSVGElement | null>(null)
const selectedParticipantId = ref<string | null>(null)
const cursor = reactive({ column: 0, row: 0 })
const dragParticipantId = ref<string | null>(null)
const dragPointer = reactive({ x: 0, y: 0, moved: false })
const panPointer = reactive({ active: false, x: 0, y: 0, moved: false })
const activePointers = new Map<number, { x: number; y: number }>()
let pinchStartDistance = 0
let observer: ResizeObserver | null = null

const verticalLines = computed(() => Array.from({ length: columns.value + 1 }, (_, index) => index))
const horizontalLines = computed(() => Array.from({ length: rows.value + 1 }, (_, index) => index))
const positionedTokens = computed(() => props.battlemap.tokens.filter(token => token.column !== null && token.row !== null))
const reserveTokens = computed(() => props.battlemap.tokens.filter(token => token.column === null || token.row === null))
const selectedToken = computed(() =>
  props.battlemap.tokens.find(token => token.participantId === selectedParticipantId.value) ?? null)
const mapWidth = computed(() => columns.value * viewport.cellSize.value)
const mapHeight = computed(() => rows.value * viewport.cellSize.value)
const defeatedSet = computed(() => new Set(props.defeatedIds))
const photoFor = (token: BattlemapToken) => token.isCreature ? null : props.avatars[token.participantId] ?? null
// The board follows the map proportion (within sensible height limits), so wide
// maps don't leave a tall empty frame and tall maps get more vertical room.
const boardStyle = computed(() => ({ '--map-ratio': `${columns.value} / ${rows.value}` }))

function controllable(token: BattlemapToken) {
  if (props.isMaster) return true
  if (token.isMarker) return token.ownerParticipantId === props.currentParticipantId
  return !token.isCreature && token.participantId === props.currentParticipantId
}

// A creature token's `participantId` field actually holds its Combatant.id (see
// BattlemapService.ToSnapshot); a player token's `participantId` matches
// Combatant.participantId. Both cases are covered by checking either field.
const currentTurnTokenId = computed(() => {
  if (props.encounterStatus !== 'Active') return null
  const current = props.combatants.find(item => item.isCurrent)
  if (!current) return null
  return current.participantId ?? current.id
})
function isCurrentTurn(token: BattlemapToken) {
  return currentTurnTokenId.value !== null && currentTurnTokenId.value === token.participantId
}

// Resolved client-side (not carried on BattlemapTokenDto) so it stays in sync with
// HP mutations, which broadcast via EncounterInvalidated/ParticipantsChanged, not
// BattlemapInvalidated. See specs/006-battlemap-combat-ux/data-model.md.
// Memoized as a computed lookup map (instead of a per-call linear scan) since the
// template reads it twice per token (current/max), for every token, every render.
const healthByTokenId = computed(() => {
  const map = new Map<string, { current: number | null; max: number | null }>()
  const combatantsByTokenId = new Map(props.combatants.map(item => [item.participantId ?? item.id, item]))
  const participantsById = new Map(props.participants.map(item => [item.id, item]))
  for (const token of props.battlemap.tokens) {
    if (token.isMarker) continue
    const combatant = combatantsByTokenId.get(token.participantId)
    if (combatant) {
      if (combatant.maxHitPoints != null) map.set(token.participantId, { current: combatant.currentHitPoints, max: combatant.maxHitPoints })
      continue
    }
    const participant = participantsById.get(token.participantId)
    if (participant?.maxHitPoints != null) map.set(token.participantId, { current: participant.currentHitPoints, max: participant.maxHitPoints })
  }
  return map
})
function healthFor(token: BattlemapToken): { current: number | null; max: number | null } | null {
  return healthByTokenId.value.get(token.participantId) ?? null
}

const attackAreas = useAttackAreas()
const tokenAppearance = useTokenAppearance()
const attackAreaMaxRadius = computed(() => Math.max(columns.value, rows.value))
const activeAttackAreaToken = computed(() =>
  selectedToken.value && controllable(selectedToken.value) ? selectedToken.value : null)

function footprintSize(token: BattlemapToken) {
  return token.footprintSize ?? 1
}

// A token spanning N cells (footprintSize) is anchored at its stored
// column/row (top-left cell) but visually centered over its whole N×N span,
// so its attack area and stacking offset originate from the middle of the
// creature, not from the corner cell (FR-012).
function tokenOrigin(token: BattlemapToken) {
  const span = footprintSize(token)
  return {
    x: (token.column! + span / 2) * viewport.cellSize.value,
    y: (token.row! + span / 2) * viewport.cellSize.value
  }
}

function areaGeometry(token: BattlemapToken) {
  const draft = attackAreas.previews[token.participantId] ?? token.attackArea
  if (!draft) return null
  const origin = tokenOrigin(token)
  return attackAreaGeometry({
    shape: draft.shape,
    originX: origin.x,
    originY: origin.y,
    cellSize: viewport.cellSize.value,
    radius: draft.radius,
    width: draft.width,
    directionDegrees: draft.directionDegrees
  })
}

function selectTokenForArea(token: BattlemapToken) {
  if (!props.online || !controllable(token)) return
  selectedParticipantId.value = token.participantId
  if (token.column !== null && token.row !== null) {
    cursor.column = token.column
    cursor.row = token.row
  }
}

function closeAttackAreaPanel() {
  if (selectedParticipantId.value) {
    attackAreas.clearPreview(selectedParticipantId.value)
    tokenAppearance.clearPreview(selectedParticipantId.value)
  }
  selectedParticipantId.value = null
}

const areaTokens = computed(() => positionedTokens.value.filter(token => areaGeometry(token) !== null))

function selectToken(token: BattlemapToken) {
  if (!props.online || !controllable(token)) return
  if (selectedParticipantId.value === token.participantId) {
    selectedParticipantId.value = null
    attackAreas.clearPreview(token.participantId)
    tokenAppearance.clearPreview(token.participantId)
    return
  }
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
  // A click resolving to the selected token's OWN current cell is never a
  // real destination choice — it is either the browser's click event
  // auto-synthesized right after this same tap's pointerup (setPointerCapture
  // in startTokenDrag retargets it onto the surface, bypassing the token
  // entirely), or a pointless self-click. Either way, committing it would
  // immediately clear the selection that this same tap just set, closing the
  // interaction panel before the user can use it. Ignoring it here (instead
  // of a timing-based suppression flag) leaves genuinely different
  // destination clicks — the documented select-then-tap-elsewhere flow —
  // completely unaffected.
  if (selectedToken.value?.column === column && selectedToken.value?.row === row) return
  cursor.column = column
  cursor.row = row
  emit('move', selectedParticipantId.value, column, row)
  attackAreas.clearPreview(selectedParticipantId.value)
  tokenAppearance.clearPreview(selectedParticipantId.value)
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
  dragPointer.x = event.clientX
  dragPointer.y = event.clientY
  dragPointer.moved = false
  surface.value?.setPointerCapture?.(event.pointerId)
}

function pointerDistance() {
  const [a, b] = activePointers.values()
  if (!a || !b) return 0
  return Math.hypot(a.x - b.x, a.y - b.y)
}

function pointerMidpoint(rect: DOMRect) {
  const [a, b] = activePointers.values()
  if (!a || !b) return { x: 0, y: 0 }
  return { x: (a.x + b.x) / 2 - rect.left, y: (a.y + b.y) / 2 - rect.top }
}

function pointerDown(event: PointerEvent) {
  activePointers.set(event.pointerId, { x: event.clientX, y: event.clientY })
  surface.value?.setPointerCapture?.(event.pointerId)
  if (activePointers.size === 2) {
    panPointer.active = false
    pinchStartDistance = pointerDistance()
    return
  }
  if (activePointers.size > 1 || selectedParticipantId.value || dragParticipantId.value) return
  panPointer.active = true
  panPointer.x = event.clientX
  panPointer.y = event.clientY
  panPointer.moved = false
}

function pointerMove(event: PointerEvent) {
  if (dragParticipantId.value) {
    const dx = event.clientX - dragPointer.x
    const dy = event.clientY - dragPointer.y
    if (Math.abs(dx) + Math.abs(dy) > 4) dragPointer.moved = true
    return
  }
  if (activePointers.has(event.pointerId)) activePointers.set(event.pointerId, { x: event.clientX, y: event.clientY })
  if (activePointers.size === 2) {
    const rect = surface.value?.getBoundingClientRect()
    if (!rect) return
    const distance = pointerDistance()
    if (pinchStartDistance > 0 && distance > 0) {
      const midpoint = pointerMidpoint(rect)
      viewport.zoom(distance / pinchStartDistance, midpoint.x, midpoint.y)
    }
    pinchStartDistance = distance
    return
  }
  if (!panPointer.active) return
  const dx = panPointer.x - event.clientX
  const dy = panPointer.y - event.clientY
  if (Math.abs(dx) + Math.abs(dy) > 2) panPointer.moved = true
  viewport.moveBy(dx, dy)
  panPointer.x = event.clientX
  panPointer.y = event.clientY
}

function pointerUp(event: PointerEvent) {
  activePointers.delete(event.pointerId)
  if (activePointers.size < 2) pinchStartDistance = 0
  if (dragParticipantId.value) {
    const tokenId = dragParticipantId.value
    const dx = event.clientX - dragPointer.x
    const dy = event.clientY - dragPointer.y
    const moved = dragPointer.moved || Math.abs(dx) + Math.abs(dy) > 4
    dragParticipantId.value = null
    dragPointer.moved = false
    if (moved) {
      const cell = cellFromEvent(event)
      selectedParticipantId.value = tokenId
      if (!isInsideMap(cell.column, cell.row)) {
        emit('move', tokenId, null, null)
        attackAreas.clearPreview(tokenId)
        tokenAppearance.clearPreview(tokenId)
        selectedParticipantId.value = null
      } else {
        commitCell(cell.column, cell.row)
      }
    }
    // A plain tap/click (no movement) only toggles selection, already applied
    // by selectToken() in startTokenDrag — nothing further to commit here.
  }
  panPointer.active = false
}

function wheel(event: WheelEvent) {
  event.preventDefault()
  const rect = surface.value?.getBoundingClientRect()
  const factor = Math.min(1.08, Math.max(1 / 1.08, 1 - event.deltaY * 0.0015))
  viewport.zoom(factor, event.clientX - (rect?.left ?? 0), event.clientY - (rect?.top ?? 0))
}

function keydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    closeAttackAreaPanel()
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
  if (footprintSize(token) > 1) return { x: 0, y: 0 }
  const sameCell = positionedTokens.value.filter(item => item.column === token.column && item.row === token.row)
  const index = sameCell.findIndex(item => item.participantId === token.participantId)
  const distance = Math.min(10, viewport.cellSize.value * .2)
  return {
    x: ((index % 2) - .5) * distance,
    y: (Math.floor(index / 2) - .5) * distance
  }
}

function tokenCenter(token: BattlemapToken) {
  const origin = tokenOrigin(token)
  const offset = stackOffset(token)
  return { x: origin.x + offset.x, y: origin.y + offset.y }
}

onMounted(() => {
  viewport.reset()
  if (surface.value && typeof ResizeObserver !== 'undefined') {
    let measured = false
    observer = new ResizeObserver(entries => {
      const rect = entries[0]?.contentRect
      if (!rect) return
      viewport.resize(rect.width, rect.height)
      // First real measurement: pick the initial zoom from the actual board size.
      if (!measured && rect.width > 0 && rect.height > 0) { measured = true; viewport.reset() }
    })
    observer.observe(surface.value)
  }
})
onBeforeUnmount(() => observer?.disconnect())
</script>

<template>
  <div class="battlemap-shell">
    <div class="battlemap-toolbar" role="toolbar" aria-label="Controles do mapa">
      <div class="toolbar-group">
        <button class="icon-button" type="button" aria-label="Diminuir zoom" title="Diminuir zoom" @click="viewport.zoom(1 / 1.2)"><Icon name="lucide:zoom-out" /></button>
        <span class="zoom-label" :title="`${Math.round(viewport.cellSize.value)} px por célula`">{{ Math.round(viewport.cellSize.value) }} px</span>
        <button class="icon-button" type="button" aria-label="Aumentar zoom" title="Aumentar zoom" @click="viewport.zoom(1.2)"><Icon name="lucide:zoom-in" /></button>
      </div>
      <div class="toolbar-group">
        <button class="button ghost compact" type="button" title="Mostrar o mapa inteiro" @click="viewport.fit"><Icon name="lucide:maximize" /><span>Ajustar mapa</span></button>
        <button class="button ghost compact" type="button" title="Voltar à escala padrão de 40 px por célula" @click="viewport.defaultScale"><Icon name="lucide:rotate-ccw" /><span>Escala 40 px</span></button>
      </div>
    </div>

    <svg
      ref="surface"
      data-board-surface
      class="battlemap-board"
      :style="boardStyle"
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
      <g :transform="`translate(${-viewport.pan.x} ${-viewport.pan.y})`">
        <rect class="battlemap-backdrop" :width="mapWidth" :height="mapHeight" />
        <!-- preserveAspectRatio="none": the whole image always fills the grid area and is
             never cropped; the grid suggestion in BattlemapSetup keeps any stretch negligible. -->
        <image
          :href="imageSource"
          :width="mapWidth"
          :height="mapHeight"
          preserveAspectRatio="none"
        />
        <rect class="battlemap-boundary" :width="mapWidth" :height="mapHeight" />
        <line
          v-for="line in (battlemap.isGridVisible ? verticalLines : [])"
          :key="`v-${line}`"
          data-grid-line
          class="battlemap-grid-line"
          :x1="line * viewport.cellSize.value"
          y1="0"
          :x2="line * viewport.cellSize.value"
          :y2="mapHeight"
        />
        <line
          v-for="line in (battlemap.isGridVisible ? horizontalLines : [])"
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
        <template v-for="token in areaTokens" :key="`area-${token.participantId}`">
          <circle
            v-if="areaGeometry(token)?.kind === 'circle'"
            class="attack-area-shape-drawing"
            :class="{ interactive: controllable(token) }"
            :cx="(areaGeometry(token) as any).cx"
            :cy="(areaGeometry(token) as any).cy"
            :r="(areaGeometry(token) as any).r"
            @click.stop="selectTokenForArea(token)"
          />
          <rect
            v-else-if="areaGeometry(token)?.kind === 'rect'"
            class="attack-area-shape-drawing"
            :class="{ interactive: controllable(token) }"
            :x="(areaGeometry(token) as any).x"
            :y="(areaGeometry(token) as any).y"
            :width="(areaGeometry(token) as any).width"
            :height="(areaGeometry(token) as any).height"
            @click.stop="selectTokenForArea(token)"
          />
          <polygon
            v-else
            class="attack-area-shape-drawing"
            :class="{ interactive: controllable(token) }"
            :points="(areaGeometry(token) as any).points"
            @click.stop="selectTokenForArea(token)"
          />
        </template>
        <BattlemapTokenMarker
          v-for="token in positionedTokens"
          :key="token.participantId"
          :token="token"
          :x="tokenCenter(token).x"
          :y="tokenCenter(token).y"
          :size="viewport.cellSize.value"
          :footprint-size="footprintSize(token)"
          :selected="selectedParticipantId === token.participantId"
          :controllable="controllable(token)"
          :pending="pendingParticipantId === token.participantId"
          :defeated="defeatedSet.has(token.participantId)"
          :photo="photoFor(token)"
          :is-current-turn="isCurrentTurn(token)"
          :current-hit-points="healthFor(token)?.current ?? null"
          :max-hit-points="healthFor(token)?.max ?? null"
          @select="selectToken"
          @dragstart="startTokenDrag"
        />
      </g>
    </svg>

    <AttackAreaPanel
      v-if="activeAttackAreaToken"
      :draft="attackAreas.draftFor(activeAttackAreaToken)"
      :has-existing="Boolean(activeAttackAreaToken.attackArea)"
      :max-radius="attackAreaMaxRadius"
      :token-name="activeAttackAreaToken.displayName"
      :appearance-draft="tokenAppearance.draftFor(activeAttackAreaToken)"
      :is-marker="Boolean(activeAttackAreaToken.isMarker)"
      @change="draft => attackAreas.preview(activeAttackAreaToken!.participantId, draft)"
      @commit="draft => emit('attack-area-commit', activeAttackAreaToken!.participantId, draft)"
      @remove="emit('attack-area-remove', activeAttackAreaToken!.participantId)"
      @appearance-change="draft => tokenAppearance.preview(activeAttackAreaToken!.participantId, draft)"
      @appearance-commit="draft => emit('token-appearance-commit', activeAttackAreaToken!.participantId, draft)"
      @remove-token="emit('marker-remove', activeAttackAreaToken!.participantId)"
      @close="closeAttackAreaPanel"
    />

    <div v-if="reserveTokens.length" class="battlemap-reserve" role="group" aria-label="Pinos na reserva">
      <p class="reserve-label"><Icon name="lucide:package" />Reserva</p>
      <button
        v-for="token in reserveTokens"
        :key="token.participantId"
        :data-participant-id="token.participantId"
        class="reserve-token"
        :class="[{ selected: selectedParticipantId === token.participantId, defeated: defeatedSet.has(token.participantId) }, `token-color-${token.colorKey}`]"
        type="button"
        :disabled="!online || !controllable(token)"
        :title="controllable(token) ? `Selecionar ${token.displayName} e escolher uma célula` : token.displayName"
        @click="selectToken(token)"
      ><span class="reserve-initials" :class="{ 'has-photo': photoFor(token) }"><img v-if="photoFor(token)" :src="photoFor(token)!" alt="" draggable="false"><template v-else>{{ token.initials }}</template></span> <span class="reserve-name">{{ token.displayName }}</span><Icon v-if="defeatedSet.has(token.participantId)" name="lucide:skull" class="reserve-kind" /></button>
    </div>
    <form v-if="online" class="battlemap-new-marker" aria-label="Inserir token representativo" @submit.prevent="submitNewMarker">
      <input
        v-model="newMarkerLabel"
        data-new-marker-label
        type="text"
        maxlength="40"
        aria-label="Nome do novo token"
        placeholder="Nome do token (ex.: Lobo invocado)"
      >
      <button data-new-marker-submit class="button ghost compact" type="submit" :disabled="!newMarkerLabel.trim()" @click.prevent="submitNewMarker"><Icon name="lucide:map-pin-plus" /><span>Inserir token</span></button>
    </form>
    <p class="battlemap-status" :class="{ warning: !online, active: online && selectedToken }" aria-live="polite">
      <Icon :name="!online ? 'lucide:wifi-off' : selectedToken ? 'lucide:mouse-pointer-click' : 'lucide:hand'" class="status-icon" />
      <span>
        <template v-if="!online">Conexão necessária para mover pinos.</template>
        <template v-else-if="selectedToken">Selecione a célula de destino para {{ selectedToken.displayName }}. Coluna {{ cursor.column + 1 }}, linha {{ cursor.row + 1 }}.</template>
        <template v-else>Arraste seu pino ou selecione-o e depois escolha uma célula. Arraste o fundo para navegar.</template>
      </span>
    </p>
  </div>
</template>
