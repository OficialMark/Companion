import { FIT_BOTTOM_GAP, fittedHeight } from '~/composables/useFitToViewport'

// Sticky side columns of the Mesa (spec 017): their foot ("Rolar", "Começar combate") must be inside
// the window both before they stick (start lower) and once stuck at `top`.
describe('fittedHeight', () => {
  it('on load, when the column still starts below its sticky top, ends at the window bottom', () => {
    // 1280x800 laptop: column starts at y=255, sticky top 88 -> 800 - 255 - 16.
    expect(fittedHeight(255, 800, 88)).toBe(800 - 255 - FIT_BOTTOM_GAP)
  })

  it('once stuck, uses the sticky top, not a scrolled-away position above it', () => {
    expect(fittedHeight(88, 800, 88)).toBe(800 - 88 - FIT_BOTTOM_GAP)
    expect(fittedHeight(-400, 800, 88)).toBe(800 - 88 - FIT_BOTTOM_GAP)
  })

  it('never collapses below a usable minimum on short windows or far-down columns', () => {
    expect(fittedHeight(700, 800, 88)).toBe(240)
  })
})
