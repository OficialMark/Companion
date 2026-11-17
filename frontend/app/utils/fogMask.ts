/**
 * Fog of war mask helpers (spec 007, research R6/R8). Pure functions, no Vue.
 *
 * The mask is a row-major bitset: cell index = row * columns + column, stored in bit
 * (index % 8) of byte (index / 8). 1 = covered. The server sends/accepts it as base64;
 * null means "fully revealed".
 */

export type FogMode = 'cover' | 'reveal'
export interface Cell { column: number; row: number }

export function maskLength(columns: number, rows: number) {
  return Math.ceil((columns * rows) / 8)
}

export function emptyMask(columns: number, rows: number) {
  return new Uint8Array(maskLength(columns, rows))
}

export function decodeMask(base64: string | null | undefined, columns: number, rows: number): Uint8Array {
  const mask = emptyMask(columns, rows)
  if (!base64) return mask
  let binary: string
  try { binary = atob(base64) } catch { return mask }
  // A mask for another grid size (stale snapshot) is ignored rather than misread.
  if (binary.length !== mask.length) return mask
  for (let i = 0; i < binary.length; i++) mask[i] = binary.charCodeAt(i)
  return mask
}

/** Encodes for the API; an all-revealed mask becomes null (the server normalizes the same way). */
export function encodeMask(mask: Uint8Array): string | null {
  let any = false
  let binary = ''
  for (let i = 0; i < mask.length; i++) {
    const byte = mask[i]!
    if (byte) any = true
    binary += String.fromCharCode(byte)
  }
  return any ? btoa(binary) : null
}

export function isFogged(mask: Uint8Array, columns: number, rows: number, column: number, row: number) {
  if (column < 0 || row < 0 || column >= columns || row >= rows) return false
  const index = row * columns + column
  return (mask[index >> 3]! & (1 << (index & 7))) !== 0
}

/** Sets one cell; returns true when it actually changed (lets callers skip re-rendering). */
export function setCell(mask: Uint8Array, columns: number, rows: number, column: number, row: number, covered: boolean) {
  if (column < 0 || row < 0 || column >= columns || row >= rows) return false
  const index = row * columns + column
  const bit = 1 << (index & 7)
  const before = mask[index >> 3]!
  const after = covered ? before | bit : before & ~bit
  if (after === before) return false
  mask[index >> 3] = after
  return true
}

/** Square brush of `size` cells centered on (column,row); even sizes lean up/left. */
export function applyBrush(mask: Uint8Array, columns: number, rows: number, center: Cell, size: number, mode: FogMode) {
  const half = Math.floor((size - 1) / 2)
  let changed = false
  for (let row = center.row - half; row < center.row - half + size; row++)
    for (let column = center.column - half; column < center.column - half + size; column++)
      changed = setCell(mask, columns, rows, column, row, mode === 'cover') || changed
  return changed
}

/** Every cell of the rectangle spanned by two corners (any order), clipped to the grid. */
export function applyRect(mask: Uint8Array, columns: number, rows: number, a: Cell, b: Cell, mode: FogMode) {
  const left = Math.max(0, Math.min(a.column, b.column))
  const right = Math.min(columns - 1, Math.max(a.column, b.column))
  const top = Math.max(0, Math.min(a.row, b.row))
  const bottom = Math.min(rows - 1, Math.max(a.row, b.row))
  let changed = false
  for (let row = top; row <= bottom; row++)
    for (let column = left; column <= right; column++)
      changed = setCell(mask, columns, rows, column, row, mode === 'cover') || changed
  return changed
}

export function fillMask(columns: number, rows: number, covered: boolean) {
  const mask = emptyMask(columns, rows)
  if (!covered) return mask
  for (let row = 0; row < rows; row++)
    for (let column = 0; column < columns; column++) setCell(mask, columns, rows, column, row, true)
  return mask
}

/**
 * One SVG path for all covered cells: each horizontal run of covered cells in a row
 * becomes one rectangle sub-path. A 100x100 grid is at most 5000 runs, far cheaper
 * than 10k <rect> nodes, and recomputing it takes well under a millisecond.
 */
export function maskPath(mask: Uint8Array, columns: number, rows: number, cellSize: number) {
  const parts: string[] = []
  for (let row = 0; row < rows; row++) {
    let start = -1
    for (let column = 0; column <= columns; column++) {
      const covered = column < columns && isFogged(mask, columns, rows, column, row)
      if (covered && start < 0) start = column
      else if (!covered && start >= 0) {
        const x = start * cellSize
        const y = row * cellSize
        const w = (column - start) * cellSize
        parts.push(`M${x} ${y}h${w}v${cellSize}h${-w}z`)
        start = -1
      }
    }
  }
  return parts.join('')
}

export function countFogged(mask: Uint8Array, columns: number, rows: number) {
  let count = 0
  for (let row = 0; row < rows; row++)
    for (let column = 0; column < columns; column++) if (isFogged(mask, columns, rows, column, row)) count++
  return count
}

/** True when every in-grid cell of a footprint anchored at (column,row) is covered (mirrors the server). */
export function isAreaFogged(mask: Uint8Array, columns: number, rows: number, column: number, row: number, footprint: number) {
  let any = false
  for (let r = row; r < row + Math.max(1, footprint); r++)
    for (let c = column; c < column + Math.max(1, footprint); c++) {
      if (c < 0 || r < 0 || c >= columns || r >= rows) continue
      any = true
      if (!isFogged(mask, columns, rows, c, r)) return false
    }
  return any
}
