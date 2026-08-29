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
})
