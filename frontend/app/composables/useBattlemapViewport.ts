import type { Ref } from 'vue'

export interface Pan { x: number; y: number }

export function initialCellSize(columns: number, rows: number, width: number, height: number) {
  const fit = Math.min(width / Math.max(columns, 1), height / Math.max(rows, 1))
  // Show the whole map on open whenever cells stay readable (>= 16px); otherwise use the 40px default.
  return fit >= 16 && fit <= 44 ? fit : 40
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

  function clamp() {
    Object.assign(pan, clampPan(
      columns.value, rows.value, cellSize.value,
      viewportWidth.value, viewportHeight.value, pan.x, pan.y
    ))
  }

  function reset() {
    cellSize.value = initialCellSize(
      columns.value, rows.value, viewportWidth.value, viewportHeight.value)
    pan.x = 0
    pan.y = 0
    clamp()
  }

  function defaultScale() {
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
    const previous = cellSize.value
    const next = Math.min(120, Math.max(12, previous * multiplier))
    const ratio = next / previous
    pan.x = (pan.x + anchorX) * ratio - anchorX
    pan.y = (pan.y + anchorY) * ratio - anchorY
    cellSize.value = next
    clamp()
  }

  function moveBy(deltaX: number, deltaY: number) {
    pan.x += deltaX
    pan.y += deltaY
    clamp()
  }

  function resize(width: number, height: number) {
    viewportWidth.value = Math.max(1, width)
    viewportHeight.value = Math.max(1, height)
    clamp()
  }

  watch([columns, rows], reset)

  return { viewportWidth, viewportHeight, cellSize, pan, reset, defaultScale, fit, zoom, moveBy, resize, clamp }
}
