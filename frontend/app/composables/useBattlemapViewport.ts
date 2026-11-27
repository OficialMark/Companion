import type { Ref } from 'vue'

export interface Pan { x: number; y: number }

/** Largest cell the automatic fit uses: a tiny grid on a big screen must not turn into giant squares. */
export const MAX_FIT_CELL = 80

export function initialCellSize(columns: number, rows: number, width: number, height: number) {
  const fit = Math.min(width / Math.max(columns, 1), height / Math.max(rows, 1))
  // Show the whole map on open whenever cells stay readable (>= 16px), up to MAX_FIT_CELL so a wide
  // screen fills its stage (it used to stop at 44 px: a 24x18 map at 2560 px sat as a thumbnail);
  // a map too big to fit readably opens at the 40px default.
  return fit >= 16 ? Math.min(fit, MAX_FIT_CELL) : 40
}

export function clampPan(
  columns: number,
  rows: number,
  cellSize: number,
  viewportWidth: number,
  viewportHeight: number,
  x: number,
  y: number
): Pan {
  const mapWidth = columns * cellSize
  const mapHeight = rows * cellSize
  // When the map is smaller than the viewport on an axis, center it instead of
  // pinning it to the top-left corner (negative pan = empty margin before the map).
  const axis = (value: number, map: number, view: number) =>
    map <= view ? -(view - map) / 2 : Math.min(map - view, Math.max(0, value))
  return { x: axis(x, mapWidth, viewportWidth), y: axis(y, mapHeight, viewportHeight) }
}

export function useBattlemapViewport(columns: Ref<number>, rows: Ref<number>) {
  const viewportWidth = ref(800)
  const viewportHeight = ref(500)
  const cellSize = ref(40)
  const pan = reactive<Pan>({ x: 0, y: 0 })
  /** The user zoomed, panned or picked a scale: from then on a resize must not undo their framing. */
  let adjusted = false

  function clamp() {
    Object.assign(pan, clampPan(
      columns.value, rows.value, cellSize.value,
      viewportWidth.value, viewportHeight.value, pan.x, pan.y
    ))
  }

  function reset() {
    adjusted = false
    cellSize.value = initialCellSize(
      columns.value, rows.value, viewportWidth.value, viewportHeight.value)
    pan.x = 0
    pan.y = 0
    clamp()
  }

  function defaultScale() {
    adjusted = true
    cellSize.value = 40
    pan.x = 0
    pan.y = 0
    clamp()
  }

  function fit() {
    cellSize.value = Math.max(12, Math.min(
      viewportWidth.value / Math.max(columns.value, 1),
      viewportHeight.value / Math.max(rows.value, 1)
    ))
    pan.x = 0
    pan.y = 0
    clamp()
  }

  function zoom(multiplier: number, anchorX = viewportWidth.value / 2, anchorY = viewportHeight.value / 2) {
    adjusted = true
    const previous = cellSize.value
    const next = Math.min(120, Math.max(12, previous * multiplier))
    const ratio = next / previous
    pan.x = (pan.x + anchorX) * ratio - anchorX
    pan.y = (pan.y + anchorY) * ratio - anchorY
    cellSize.value = next
    clamp()
  }

  function moveBy(deltaX: number, deltaY: number) {
    if (deltaX || deltaY) adjusted = true
    pan.x += deltaX
    pan.y += deltaY
    clamp()
  }

  function resize(width: number, height: number) {
    viewportWidth.value = Math.max(1, width)
    viewportHeight.value = Math.max(1, height)
    // The stage grows after the first measure (image loaded, side columns laid out): keep the map
    // framed to it until the user takes over.
    if (!adjusted) reset()
    else clamp()
  }

  watch([columns, rows], reset)

  return { viewportWidth, viewportHeight, cellSize, pan, reset, defaultScale, fit, zoom, moveBy, resize, clamp }
}
