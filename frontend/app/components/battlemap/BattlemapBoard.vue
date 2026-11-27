<script setup lang="ts">
import type { BattlemapSnapshot, BattlemapToken, Combatant, EncounterStatus, Participant } from '~/types/campaign'
import { useBattlemapViewport } from '~/composables/useBattlemapViewport'
import { useAttackAreas, type AttackAreaDraft } from '~/composables/useAttackAreas'
import { useTokenAppearance, type TokenAppearanceDraft } from '~/composables/useTokenAppearance'
import { attackAreaGeometry } from '~/utils/attackAreaGeometry'
import { useFogOfWar } from '~/composables/useFogOfWar'
import { maskPath } from '~/utils/fogMask'
import BattlemapTokenMarker from './BattlemapToken.vue'
import AttackAreaPanel from './AttackAreaPanel.vue'
import FogToolbar from './FogToolbar.vue'
import TokenSourcePicker from './TokenSourcePicker.vue'

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
  /** A fog save is in flight (store.fogSaving). */
  fogSaving?: boolean
}>(), { pendingParticipantId: null, defeatedIds: () => [], avatars: () => ({}), combatants: () => [], encounterStatus: null, participants: () => [], fogSaving: false })
const emit = defineEmits<{
  move: [participantId: string, column: number | null, row: number | null]
  'attack-area-commit': [tokenId: string, draft: AttackAreaDraft]
  'attack-area-remove': [tokenId: string]
  'token-appearance-commit': [tokenId: string, draft: TokenAppearanceDraft]
  'create-marker': [label: string]
  'marker-remove': [markerId: string]
  'fog-commit': [mask: string | null]
  /** combatantId = null applies to every creature of the encounter. */
  'hit-points-visibility': [combatantId: string | null, hidden: boolean]
  'open-creature-sheet': [tokenId: string, creatureId: string, name: string]
}>()

const newMarkerLabel = ref('')
const catalogOpen = ref(false)
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
// The browser synthesizes a `click` after every pointerup. Because startTokenDrag
// captures the pointer on the surface, that click lands on the SURFACE (not on the
// token) and clickSurface would treat it as "move the selected token here": for a 2x2+
// token or a stacked one the cell under the cursor differs from the anchor, so the token
// jumped and its menu closed as soon as the left button was released (right-click worked
// only because it fires no click). Armed when a token pointer session ends, consumed by
// the next surface click, and cleared on any new pointerdown so it can never swallow a
// later, genuine click even when the browser emits no click at all.
let suppressSurfaceClick = false
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
// Players' photos and library creature images (spec 008) both arrive keyed by token id.
const photoFor = (token: BattlemapToken) => props.avatars[token.participantId] ?? null
// The board follows the map proportion (within sensible height limits), so wide
// maps don't leave a tall empty frame and tall maps get more vertical room.
const boardStyle = computed(() => ({ '--map-ratio': `${columns.value} / ${rows.value}` }))

// Fog of war (spec 007, US4). Players get an opaque layer; the master a translucent one.
const fog = useFogOfWar(computed(() => props.battlemap.fogMask), columns, rows, computed(() => props.fogSaving))
const fogPatternId = computed(() => `fog-pattern-${props.battlemap.id}`)
const fogPathD = computed(() => maskPath(fog.mask.value, columns.value, rows.value, viewport.cellSize.value))
const fogHover = ref<{ column: number; row: number } | null>(null)
const fogBrushPreview = computed(() => {
  if (!fog.active.value || fog.tool.value !== 'brush' || !fogHover.value || fog.rectPreview.value) return null
  const half = Math.floor((fog.brushSize.value - 1) / 2)
  return { column: fogHover.value.column - half, row: fogHover.value.row - half, size: fog.brushSize.value }
})
function toggleFogMode() {
  if (!props.isMaster || !props.online) return
  if (fog.active.value) { exitFogMode(); return }
  closeAttackAreaPanel()
  fog.active.value = true
}
function exitFogMode() {
  if (fog.stroking.value) fog.cancel()
  fog.active.value = false
  fogHover.value = null
}
function fogFillAll(covered: boolean) {
  emit('fog-commit', fog.fillAll(covered))
}
watch(() => props.online, online => { if (!online && fog.active.value) exitFogMode() })
watch(() => props.isMaster, master => { if (!master) exitFogMode() })

// Master-owned tokens are hidden from players when fully fogged; the master sees them dimmed.
function masterOwned(token: BattlemapToken) {
  if (token.isCreature) return true
  if (!token.isMarker) return false
  return !props.participants.some(item => item.id === token.ownerParticipantId && item.role === 'Player')
}
function foggedForMaster(token: BattlemapToken) {
  return props.isMaster && masterOwned(token) && fog.tokenFogged(token.column, token.row, token.footprintSize ?? 1)
}

// HP visibility (spec 007, US3). A creature token's participantId is its Combatant.id.
const combatantsById = computed(() => new Map(props.combatants.map(item => [item.id, item])))
function creatureCombatant(token: BattlemapToken) {
  return token.isCreature ? combatantsById.value.get(token.participantId) ?? null : null
}
function hitPointsHiddenFor(token: BattlemapToken) {
  return props.isMaster && Boolean(creatureCombatant(token)?.isHitPointsHidden)
}
/** Toggle state for the token menu: null hides the toggle (players, characters, markers). */
function panelHitPointsHidden(token: BattlemapToken) {
  if (!props.isMaster) return null
  const combatant = creatureCombatant(token)
  return combatant ? Boolean(combatant.isHitPointsHidden) : null
}
const creatureCombatants = computed(() => props.combatants.filter(item => !item.participantId))
const allCreatureHitPointsHidden = computed(() =>
  creatureCombatants.value.length > 0 && creatureCombatants.value.every(item => item.isHitPointsHidden))

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

/**
 * The sheet a right-click (or Shift+F10 / Menu key) opens for this token (spec 010). Today only
 * the master's creature tokens have one; player character sheets will plug in here.
 */
function sheetFor(token: BattlemapToken): { creatureId: string } | null {
  return props.isMaster && token.creatureId ? { creatureId: token.creatureId } : null
}

const hasSheetTokens = computed(() => positionedTokens.value.some(token => sheetFor(token)))

/**
 * Right-click / context key: the token's sheet when it has one, else the token menu as before.
 * Same guards for mouse and keyboard: nothing in fog mode (it owns the board) or offline.
 */
function openTokenContext(token: BattlemapToken) {
  if (fog.active.value || !props.online) return
  const sheet = sheetFor(token)
  if (!sheet) {
    selectToken(token)
    return
  }
  // Any open token menu goes away, not only this token's: closing the sheet must not bring back
  // another token's menu with a pending move.
  if (selectedParticipantId.value) closeAttackAreaPanel()
  emit('open-creature-sheet', token.participantId, sheet.creatureId, token.displayName)
}

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

/**
 * Keeps the browser's own context menu closed for a right-click gesture that started on the board.
 * The board's `@contextmenu.prevent` is not enough: Windows Chrome fires `contextmenu` when the right
 * button (or the Menu key) goes UP, and by then our right-click may have opened the sheet or a token
 * menu on top of the board, or the pointer may have been dragged off it. So every `contextmenu` on
 * the page is cancelled from the press until the gesture is over: after the release, the first
 * `contextmenu` (the one Windows fires on release) or 300 ms, whichever comes first; a new press
 * anywhere ends it at once, and a 10 s cap covers a release we never hear about. Right-clicks that
 * start anywhere else keep the normal browser menu.
 */
let releaseMenuGuard: (() => void) | null = null
function guardBrowserMenu(endsOn: 'pointerup' | 'keyup') {
  releaseMenuGuard?.()
  let released = false
  const block = (event: Event) => {
    event.preventDefault()
    if (released) release() // the release-time event: the gesture is over
  }
  let timer = window.setTimeout(release, 10_000)
  const ended = (event: Event) => {
    if (endsOn === 'pointerup' && (event as PointerEvent).button !== 2) return
    // Only the menu key itself ends a keyboard gesture (Shift going up mid-hold does not).
    if (endsOn === 'keyup' && !['ContextMenu', 'F10'].includes((event as KeyboardEvent).key)) return
    released = true
    window.clearTimeout(timer)
    timer = window.setTimeout(release, 300)
  }
  // Any new press is a new gesture: it gets the normal menu unless it is on the board, which re-arms
  // right after (window capture runs before the board's handlers). This also frees the page when the
  // previous release was lost (focus change, pen cancel), instead of eating menus for up to 10 s.
  const newPress = () => release()
  function release() {
    window.clearTimeout(timer)
    document.removeEventListener('contextmenu', block, true)
    window.removeEventListener(endsOn, ended, true)
    window.removeEventListener('pointerdown', newPress, true)
    releaseMenuGuard = null
  }
  document.addEventListener('contextmenu', block, true)
  window.addEventListener(endsOn, ended, true)
  window.addEventListener('pointerdown', newPress, true)
  releaseMenuGuard = release
}
onBeforeUnmount(() => releaseMenuGuard?.())

/** Keyboard "right-click" (Shift+F10 / Menu key) on a token: same guard, released on key up. */
function openTokenContextFromKey(token: BattlemapToken) {
  guardBrowserMenu('keyup')
  openTokenContext(token)
}

function clickSurface(event: MouseEvent) {
  if (suppressSurfaceClick) {
    suppressSurfaceClick = false
    return
  }
  if (fog.active.value) return
  if (panPointer.moved) {
    panPointer.moved = false
    return
  }
  const cell = cellFromEvent(event)
  commitCell(cell.column, cell.row)
}

function startTokenDrag(event: PointerEvent, token: BattlemapToken) {
  suppressSurfaceClick = false
  if (event.button === 2) guardBrowserMenu('pointerup')
  if (fog.active.value) return
  // Middle button: nothing. Right button: the token's sheet, or its menu when it has no sheet,
  // without dragging (the browser menu is suppressed on the board). Only the primary
  // button/touch/pen drags.
  if (event.button === 1) return
  if (event.button === 2) {
    openTokenContext(token)
    return
  }
  selectToken(token)
  if (!props.online || !controllable(token)) return
  dragParticipantId.value = token.participantId
  dragPointer.x = event.clientX
  dragPointer.y = event.clientY
  dragPointer.moved = false
  capturePointer(event.pointerId)
}

/** Capture can fail when the pointer is already gone (touch races); dragging still works without it. */
function capturePointer(pointerId: number) {
  try { surface.value?.setPointerCapture?.(pointerId) } catch { /* not capturable */ }
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

function startPan(event: PointerEvent) {
  panPointer.active = true
  panPointer.x = event.clientX
  panPointer.y = event.clientY
  panPointer.moved = false
}

function pointerDown(event: PointerEvent) {
  suppressSurfaceClick = false
  if (event.button === 2) guardBrowserMenu('pointerup')
  // A pan that ended without a click (right/middle button) must not swallow the next click.
  panPointer.moved = false
  // A primary pointer going down means no other pointer is really active: drop entries
  // whose pointerup was lost (released outside the window, focus change), so a stale
  // one can never turn the next press into a "pinch" or leave a fog stroke hanging.
  if (event.isPrimary !== false) {
    activePointers.clear()
    if (fog.stroking.value) commitFogStroke()
  }
  activePointers.set(event.pointerId, { x: event.clientX, y: event.clientY })
  capturePointer(event.pointerId)
  if (activePointers.size === 2) {
    // Second finger: pinch wins over painting/panning; an in-progress fog stroke is undone.
    if (fog.stroking.value) fog.cancel()
    panPointer.active = false
    pinchStartDistance = pointerDistance()
    return
  }
  if (activePointers.size > 1 || dragParticipantId.value) return
  // Right/middle drag always pans (the only mouse pan while fog mode paints with the left button).
  if (event.button === 1 || event.button === 2) {
    startPan(event)
    return
  }
  if (fog.active.value) {
    fog.begin(cellFromEvent(event))
    return
  }
  if (selectedParticipantId.value) return
  panPointer.active = true
  panPointer.x = event.clientX
  panPointer.y = event.clientY
  panPointer.moved = false
}

function pointerMove(event: PointerEvent) {
  // Right button pressed while another is already down (chorded): no pointerdown, only a move.
  if (event.button === 2) guardBrowserMenu('pointerup')
  if (fog.active.value) {
    const cell = cellFromEvent(event)
    if (!fogHover.value || fogHover.value.column !== cell.column || fogHover.value.row !== cell.row) fogHover.value = cell
    if (fog.stroking.value && activePointers.size === 1) {
      fog.extend(cell)
      return
    }
  }
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
  if (fog.stroking.value) commitFogStroke()
  if (dragParticipantId.value) {
    suppressSurfaceClick = true
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

function commitFogStroke() {
  const mask = fog.end()
  if (mask !== undefined) emit('fog-commit', mask)
}

/** Capture lost without a pointerup (e.g. window blur): finish what that pointer was doing. */
function lostPointerCapture(event: PointerEvent) {
  if (activePointers.has(event.pointerId)) pointerUp(event)
}

function pointerCancel(event: PointerEvent) {
  if (fog.stroking.value) fog.cancel()
  pointerUp(event)
}

function pointerLeave() {
  fogHover.value = null
}

function wheel(event: WheelEvent) {
  event.preventDefault()
  const rect = surface.value?.getBoundingClientRect()
  const factor = Math.min(1.08, Math.max(1 / 1.08, 1 - event.deltaY * 0.0015))
  viewport.zoom(factor, event.clientX - (rect?.left ?? 0), event.clientY - (rect?.top ?? 0))
}

function keydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    if (fog.active.value) exitFogMode()
    else closeAttackAreaPanel()
    return
  }
  if (fog.active.value) return
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
      <div v-if="isMaster" class="toolbar-group">
        <button
          class="button compact"
          :class="fog.active.value ? 'primary' : 'ghost'"
          type="button"
          data-fog-toggle
          :aria-pressed="fog.active.value"
          :disabled="!online"
          title="Ocultar ou revelar partes do mapa"
          @click="toggleFogMode"
        ><Icon name="lucide:cloud-fog" /><span>Névoa</span></button>
        <button
          v-if="creatureCombatants.length"
          class="button ghost compact"
          type="button"
          data-hp-visibility-all
          :aria-pressed="allCreatureHitPointsHidden"
          :disabled="!online"
          :title="allCreatureHitPointsHidden ? 'Mostrar a vida das criaturas aos jogadores' : 'Ocultar a vida das criaturas dos jogadores'"
          @click="emit('hit-points-visibility', null, !allCreatureHitPointsHidden)"
        ><Icon :name="allCreatureHitPointsHidden ? 'lucide:eye-off' : 'lucide:eye'" /><span>{{ allCreatureHitPointsHidden ? 'HP oculto' : 'HP visível' }}</span></button>
      </div>
      <div class="toolbar-group">
        <button class="button ghost compact" type="button" title="Mostrar o mapa inteiro" @click="viewport.fit"><Icon name="lucide:maximize" /><span>Ajustar mapa</span></button>
        <button class="button ghost compact" type="button" title="Voltar à escala padrão de 40 px por célula" @click="viewport.defaultScale"><Icon name="lucide:rotate-ccw" /><span>Escala 40 px</span></button>
      </div>
    </div>

    <FogToolbar
      v-if="fog.active.value"
      v-model:mode="fog.mode.value"
      v-model:tool="fog.tool.value"
      v-model:brush-size="fog.brushSize.value"
      :busy="fog.stroking.value"
      @cover-all="fogFillAll(true)"
      @reveal-all="fogFillAll(false)"
      @close="exitFogMode"
    />

    <svg
      ref="surface"
      data-board-surface
      class="battlemap-board"
      :class="{ 'fog-editing': fog.active.value }"
      :style="boardStyle"
      :viewBox="`0 0 ${viewport.viewportWidth.value} ${viewport.viewportHeight.value}`"
      role="application"
      tabindex="0"
      :aria-label="`Battlemap com ${columns} colunas e ${rows} linhas`"
      @click="clickSurface"
      @pointerdown="pointerDown"
      @pointermove="pointerMove"
      @pointerup="pointerUp"
      @pointercancel="pointerCancel"
      @lostpointercapture="lostPointerCapture"
      @pointerleave="pointerLeave"
      @contextmenu.prevent
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
        <defs v-if="fogPathD">
          <!-- Soft "cloud" texture so fog reads as unexplored map, not as the empty board around it. -->
          <pattern :id="fogPatternId" patternUnits="userSpaceOnUse" width="64" height="64">
            <rect width="64" height="64" fill="#1b1721" />
            <circle cx="14" cy="18" r="16" fill="#25202d" opacity=".75" />
            <circle cx="46" cy="40" r="20" fill="#221d2a" opacity=".7" />
            <circle cx="56" cy="8" r="10" fill="#29233a" opacity=".5" />
            <circle cx="20" cy="54" r="9" fill="#28223a" opacity=".45" />
          </pattern>
        </defs>
        <path
          v-if="fogPathD"
          data-fog-layer
          class="battlemap-fog"
          :class="{ master: isMaster }"
          :fill="`url(#${fogPatternId})`"
          :d="fogPathD"
        />
        <rect
          v-if="fog.rectPreview.value"
          data-fog-rect-preview
          class="fog-rect-preview"
          :class="fog.mode.value"
          :x="fog.rectPreview.value.column * viewport.cellSize.value"
          :y="fog.rectPreview.value.row * viewport.cellSize.value"
          :width="(fog.rectPreview.value.right - fog.rectPreview.value.column + 1) * viewport.cellSize.value"
          :height="(fog.rectPreview.value.bottom - fog.rectPreview.value.row + 1) * viewport.cellSize.value"
        />
        <rect
          v-if="fogBrushPreview"
          class="fog-brush-preview"
          :class="fog.mode.value"
          :x="fogBrushPreview.column * viewport.cellSize.value"
          :y="fogBrushPreview.row * viewport.cellSize.value"
          :width="fogBrushPreview.size * viewport.cellSize.value"
          :height="fogBrushPreview.size * viewport.cellSize.value"
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
          :hit-points-hidden="hitPointsHiddenFor(token)"
          :fogged="foggedForMaster(token)"
          @select="selectToken"
          @dragstart="startTokenDrag"
          :has-sheet="Boolean(sheetFor(token))"
          @context="openTokenContextFromKey"
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
      :hit-points-hidden="panelHitPointsHidden(activeAttackAreaToken)"
      :can-open-sheet="Boolean(sheetFor(activeAttackAreaToken))"
      @open-sheet="emit('open-creature-sheet', activeAttackAreaToken!.participantId, activeAttackAreaToken!.creatureId!, activeAttackAreaToken!.displayName)"
      @change="draft => attackAreas.preview(activeAttackAreaToken!.participantId, draft)"
      @commit="draft => emit('attack-area-commit', activeAttackAreaToken!.participantId, draft)"
      @remove="emit('attack-area-remove', activeAttackAreaToken!.participantId)"
      @appearance-change="draft => tokenAppearance.preview(activeAttackAreaToken!.participantId, draft)"
      @appearance-commit="draft => emit('token-appearance-commit', activeAttackAreaToken!.participantId, draft)"
      @remove-token="emit('marker-remove', activeAttackAreaToken!.participantId)"
      @hit-points-visibility="hidden => emit('hit-points-visibility', activeAttackAreaToken!.participantId, hidden)"
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
      <button data-board-catalog class="button secondary compact" type="button" :aria-expanded="catalogOpen" @click="catalogOpen = !catalogOpen"><Icon :name="catalogOpen ? 'lucide:x' : 'lucide:book-open'" /><span>{{ catalogOpen ? 'Fechar compêndio' : 'Do compêndio' }}</span></button>
    </form>
    <div v-if="online && catalogOpen" class="battlemap-catalog"><TokenSourcePicker :is-master="isMaster" /></div>
    <p class="battlemap-status" :class="{ warning: !online, active: online && selectedToken }" aria-live="polite">
      <Icon :name="!online ? 'lucide:wifi-off' : selectedToken ? 'lucide:mouse-pointer-click' : 'lucide:hand'" class="status-icon" />
      <span>
        <template v-if="!online">Conexão necessária para mover pinos.</template>
        <template v-else-if="fog.active.value">Modo névoa: os jogadores não veem as áreas cobertas nem as criaturas dentro delas.</template>
        <template v-else-if="selectedToken">Selecione a célula de destino para {{ selectedToken.displayName }}. Coluna {{ cursor.column + 1 }}, linha {{ cursor.row + 1 }}.</template>
        <template v-else>Arraste seu pino ou selecione-o e depois escolha uma célula. Arraste o fundo para navegar.<template v-if="hasSheetTokens"> Botão direito (ou Shift+F10) numa criatura abre a ficha.</template></template>
      </span>
    </p>
  </div>
</template>
