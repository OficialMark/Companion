import { computed, ref, shallowRef, triggerRef, watch, type Ref } from 'vue'
import {
  applyBrush, applyRect, countFogged, decodeMask, encodeMask, fillMask, isAreaFogged,
  type Cell, type FogMode
} from '~/utils/fogMask'

export type FogTool = 'brush' | 'rect'

/**
 * Local fog state for the board (spec 007, US4). Holds the server mask, a working copy
 * that strokes edit with instant preview, and turns a finished stroke into one encoded
 * mask for the caller to persist. The server snapshot always wins when it changes.
 */
export function useFogOfWar(
  source: Ref<string | null | undefined>,
  columns: Ref<number>,
  rows: Ref<number>,
  /** True while the caller is persisting a mask: server echoes in between are stale. */
  saving: Ref<boolean> = ref(false)
) {
  const active = ref(false)
  const mode = ref<FogMode>('cover')
  const tool = ref<FogTool>('brush')
  const brushSize = ref(1)

  const mask = shallowRef(decodeMask(source.value, columns.value, rows.value))
  let strokeBase: Uint8Array | null = null
  const stroking = ref(false)
  const rectStart = ref<Cell | null>(null)
  const rectEnd = ref<Cell | null>(null)

  watch([source, columns, rows, saving], () => {
    // Server state is adopted only when nothing local is pending: not mid-stroke and no
    // save in flight. Otherwise an older echo would flicker the master's painting back.
    if (stroking.value || saving.value) return
    mask.value = decodeMask(source.value, columns.value, rows.value)
  })

  const hasFog = computed(() => countFogged(mask.value, columns.value, rows.value) > 0)

  function begin(cell: Cell) {
    strokeBase = mask.value.slice()
    stroking.value = true
    if (tool.value === 'rect') {
      rectStart.value = cell
      rectEnd.value = cell
      return
    }
    if (applyBrush(mask.value, columns.value, rows.value, cell, brushSize.value, mode.value)) triggerRef(mask)
  }

  function extend(cell: Cell) {
    if (!stroking.value) return
    if (tool.value === 'rect') {
      rectEnd.value = cell
      return
    }
    if (applyBrush(mask.value, columns.value, rows.value, cell, brushSize.value, mode.value)) triggerRef(mask)
  }

  /** Ends the stroke; returns the encoded mask to persist, or undefined when nothing changed. */
  function end(): string | null | undefined {
    if (!stroking.value) return undefined
    if (tool.value === 'rect' && rectStart.value && rectEnd.value) {
      applyRect(mask.value, columns.value, rows.value, rectStart.value, rectEnd.value, mode.value)
      triggerRef(mask)
    }
    const changed = !strokeBase || !sameMask(strokeBase, mask.value)
    strokeBase = null
    stroking.value = false
    rectStart.value = null
    rectEnd.value = null
    return changed ? encodeMask(mask.value) : undefined
  }

  /** Abandons the stroke (second finger for pinch, Esc, lost connection). */
  function cancel() {
    if (strokeBase) mask.value = strokeBase
    strokeBase = null
    stroking.value = false
    rectStart.value = null
    rectEnd.value = null
    triggerRef(mask)
  }

  function fillAll(covered: boolean) {
    mask.value = fillMask(columns.value, rows.value, covered)
    return encodeMask(mask.value)
  }

  /** Reverts to the last server state (after a failed save). */
  function resync() {
    cancel()
    mask.value = decodeMask(source.value, columns.value, rows.value)
  }

  const rectPreview = computed(() => {
    if (!rectStart.value || !rectEnd.value) return null
    return {
      column: Math.max(0, Math.min(rectStart.value.column, rectEnd.value.column)),
      row: Math.max(0, Math.min(rectStart.value.row, rectEnd.value.row)),
      right: Math.min(columns.value - 1, Math.max(rectStart.value.column, rectEnd.value.column)),
      bottom: Math.min(rows.value - 1, Math.max(rectStart.value.row, rectEnd.value.row))
    }
  })

  function tokenFogged(column: number | null, row: number | null, footprint: number) {
    if (column === null || row === null) return false
    return isAreaFogged(mask.value, columns.value, rows.value, column, row, footprint)
  }

  return {
    active, mode, tool, brushSize, mask, stroking, rectPreview, hasFog,
    begin, extend, end, cancel, fillAll, resync, tokenFogged
  }
}

function sameMask(a: Uint8Array, b: Uint8Array) {
  if (a.length !== b.length) return false
  for (let i = 0; i < a.length; i++) if (a[i] !== b[i]) return false
  return true
}
