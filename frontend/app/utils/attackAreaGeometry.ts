import type { AttackAreaShape } from '~/types/campaign'

export interface AttackAreaGeometryInput {
  shape: AttackAreaShape
  originX: number
  originY: number
  cellSize: number
  radius: number
  width: number | null
  directionDegrees: number | null
}

export type AttackAreaGeometry =
  | { kind: 'circle'; cx: number; cy: number; r: number }
  | { kind: 'rect'; x: number; y: number; width: number; height: number }
  | { kind: 'polygon'; points: string }

/**
 * Cone/Cylinder direction: 0deg points along +x (screen right), increasing clockwise
 * (SVG's y axis points down, so the usual cos/sin pair rotates visually clockwise).
 */
export function attackAreaGeometry(input: AttackAreaGeometryInput): AttackAreaGeometry {
  const { shape, originX, originY, cellSize, radius } = input
  const r = Math.max(0, radius) * cellSize

  if (shape === 'Circle') return { kind: 'circle', cx: originX, cy: originY, r }
  if (shape === 'Square') return { kind: 'rect', x: originX - r, y: originY - r, width: r * 2, height: r * 2 }

  const width = Math.max(0, input.width ?? 1) * cellSize
  const angle = ((input.directionDegrees ?? 0) * Math.PI) / 180
  const dir = { x: Math.cos(angle), y: Math.sin(angle) }
  const perp = { x: -dir.y, y: dir.x }
  const halfWidth = width / 2

  if (shape === 'Cylinder') {
    const p1 = { x: originX + perp.x * halfWidth, y: originY + perp.y * halfWidth }
    const p2 = { x: originX - perp.x * halfWidth, y: originY - perp.y * halfWidth }
    const p3 = { x: p2.x + dir.x * r, y: p2.y + dir.y * r }
    const p4 = { x: p1.x + dir.x * r, y: p1.y + dir.y * r }
    return { kind: 'polygon', points: [p1, p2, p3, p4].map(p => `${p.x},${p.y}`).join(' ') }
  }

  const baseCenter = { x: originX + dir.x * r, y: originY + dir.y * r }
  const b1 = { x: baseCenter.x + perp.x * halfWidth, y: baseCenter.y + perp.y * halfWidth }
  const b2 = { x: baseCenter.x - perp.x * halfWidth, y: baseCenter.y - perp.y * halfWidth }
  return { kind: 'polygon', points: [{ x: originX, y: originY }, b1, b2].map(p => `${p.x},${p.y}`).join(' ') }
}
