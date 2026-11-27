import { clampPan, initialCellSize, MAX_FIT_CELL, useBattlemapViewport } from '~/composables/useBattlemapViewport'

describe('battlemap viewport math', () => {
  it('opens a map too big to read at the 40px default, and fits the rest up to MAX_FIT_CELL', () => {
    expect(initialCellSize(100, 100, 800, 500)).toBe(40)
    expect(initialCellSize(20, 10, 800, 500)).toBe(40)
    // A tiny grid never becomes giant squares.
    expect(initialCellSize(1, 1, 800, 500)).toBe(MAX_FIT_CELL)
    // Regression (layout review, 2560 px): a 24x18 map on a 1300x975 stage fills it (54 px cells)
    // instead of sitting as a 40 px thumbnail in the middle.
    expect(initialCellSize(24, 18, 1300, 975)).toBeCloseTo(54.17, 1)
  })

  it('keeps the map framed to the stage while it grows, until the user zooms or pans', () => {
    const viewport = useBattlemapViewport(ref(24), ref(18))
    viewport.resize(600, 450)
    viewport.reset()
    expect(viewport.cellSize.value).toBe(25)
    viewport.resize(1300, 975)
    expect(viewport.cellSize.value).toBeCloseTo(54.17, 1)
    viewport.zoom(1.2)
    const zoomed = viewport.cellSize.value
    viewport.resize(1600, 1200)
    expect(viewport.cellSize.value).toBe(zoomed)
    // Any touch or key on the board also freezes the framing: nothing moves under the pointer
    // between aiming at a token and clicking it (e2e right-click regression).
    viewport.reset()
    viewport.resize(600, 450)
    viewport.hold()
    viewport.resize(1300, 975)
    expect(viewport.cellSize.value).toBe(25)
    // "Ajustar mapa" style reset hands framing back to the stage.
    viewport.reset()
    viewport.resize(1300, 975)
    expect(viewport.cellSize.value).toBeCloseTo(54.17, 1)
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
