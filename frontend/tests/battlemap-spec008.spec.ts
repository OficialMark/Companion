import { mount } from '@vue/test-utils'
import BattlemapToken from '~/components/battlemap/BattlemapToken.vue'
import type { BattlemapToken as Token } from '~/types/campaign'

const token: Token = {
  participantId: 'goblin-1', displayName: 'Goblin', initials: 'GO', colorKey: 1, column: 1, row: 1,
  positionVersion: 'v1', isCreature: true, statusConditions: ['Poisoned', 'Bleeding']
}

function mountAt(size: number, footprintSize = 1) {
  return mount({ components: { BattlemapToken }, template: '<svg><BattlemapToken v-bind="$attrs" /></svg>' }, {
    attrs: { token, x: 0, y: 0, size, footprintSize, selected: false, controllable: true, pending: false, hitPointsHidden: true, currentHitPoints: 3, maxHitPoints: 7 }
  })
}

function parse(transform: string) {
  const [tx, ty, scale] = transform.match(/-?\d+(?:\.\d+)?(?:e-?\d+)?/g)!.map(Number)
  return { tx: tx!, ty: ty!, scale: scale! }
}

describe('US1 (spec 008): badge glyphs stay centered at every size', () => {
  // Regression: the icon is an em-sized nested <svg> that ignores CSS width:100%, so the old
  // x/y/width/height box left it a fixed 17.6 px pinned to the box corner (off-center at
  // every zoom but one). The glyph must now be centered purely by transform.
  it.each([8, 12, 20, 40, 120])('cell size %i: translate is exactly half of the scaled 24-unit box', (size) => {
    const wrapper = mountAt(size)
    const glyphs = wrapper.findAll('.token-glyph')
    expect(glyphs.length).toBeGreaterThanOrEqual(3) // 2 conditions + hidden-HP indicator
    for (const glyph of glyphs) {
      const { tx, ty, scale } = parse(glyph.attributes('transform')!)
      expect(tx).toBeCloseTo(-12 * scale, 6)
      expect(ty).toBeCloseTo(-12 * scale, 6)
      expect(glyph.get('.icon').attributes('size')).toBe('24px')
    }
  })

  it('no glyph is a nested <svg> box anymore', () => {
    const wrapper = mountAt(10)
    expect(wrapper.findAll('svg.token-glyph')).toHaveLength(0)
  })
})
