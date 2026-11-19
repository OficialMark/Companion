import { clampPan, initialCellSize } from '~/composables/useBattlemapViewport'

describe('battlemap viewport math', () => {
  it('keeps the default cell near 40px when fit would be too small or too large', () => {
    expect(initialCellSize(100, 100, 800, 500)).toBe(40)
    expect(initialCellSize(1, 1, 800, 500)).toBe(40)
    expect(initialCellSize(20, 10, 800, 500)).toBe(40)
  })

  it('clamps pan while preserving access to every map edge', () => {
    expect(clampPan(100, 100, 40, 800, 500, 5000, 5000)).toEqual({ x: 3200, y: 3500 })
    expect(clampPan(100, 100, 40, 800, 500, -5000, -5000)).toEqual({ x: 0, y: 0 })
  })

  it('centers a map that is smaller than the viewport instead of pinning it to the corner', () => {
    expect(clampPan(10, 10, 40, 800, 500, 0, 0)).toEqual({ x: -200, y: -50 })
    expect(clampPan(10, 20, 40, 800, 500, 0, 9999)).toEqual({ x: -200, y: 300 })
  })
})
