import { mount } from '@vue/test-utils'
import BattlemapBoard from '~/components/battlemap/BattlemapBoard.vue'
import AttackAreaPanel from '~/components/battlemap/AttackAreaPanel.vue'
import type { BattlemapSnapshot } from '~/types/campaign'

const snapshot: BattlemapSnapshot = {
  id: 'map-1',
  encounterId: 'encounter-1',
  columns: 20,
  rows: 20,
  isGridVisible: true,
  revision: 'revision',
  imageRevision: 'image-revision',
  imageUrl: 'blob:test',
  imageMediaType: 'image/png',
  imageFileName: 'map.png',
  updatedAt: new Date().toISOString(),
  tokens: [
    { participantId: 'player-0', displayName: 'Lia', initials: 'LI', colorKey: 0, column: 2, row: 2, positionVersion: 'v1' }
  ]
}

// Regression guard (US6): after US3 (token size) and US4 (status conditions)
// added sections to the same interaction panel that US1 already opened via a
// single click/tap, a single pointerdown+pointerup (no drag, no second click,
// no right-click, no long-press) must still open the FULL expanded panel.
describe('token interaction panel opens with a single click/tap, expanded sections included', () => {
  it('opens the panel with shape, size and condition sections from one pointerdown+pointerup', async () => {
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: snapshot, imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: true }
    })
    const token = wrapper.get('[data-participant-id="player-0"]')

    await token.trigger('pointerdown')
    await token.trigger('pointerup')

    const panel = wrapper.findComponent(AttackAreaPanel)
    expect(panel.exists()).toBe(true)
    expect(panel.findAll('.attack-area-shape')).toHaveLength(4)
    expect(panel.findAll('.token-size-option')).toHaveLength(5)
    expect(panel.findAll('.token-condition-option')).toHaveLength(6)
  })

  it('does not require a right-click (contextmenu) to open the panel', async () => {
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: snapshot, imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: true }
    })
    const token = wrapper.get('[data-participant-id="player-0"]')
    await token.trigger('contextmenu')
    expect(wrapper.findComponent(AttackAreaPanel).exists()).toBe(false)

    await token.trigger('pointerdown')
    await token.trigger('pointerup')
    expect(wrapper.findComponent(AttackAreaPanel).exists()).toBe(true)
  })

  it('does not require a second click (single tap toggles open, not double-tap)', async () => {
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: snapshot, imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: true }
    })
    const token = wrapper.get('[data-participant-id="player-0"]')
    await token.trigger('pointerdown')
    await token.trigger('pointerup')
    expect(wrapper.findComponent(AttackAreaPanel).exists()).toBe(true)
  })

  // Regression (found via a real Playwright run, not reproducible by simply
  // triggering 'click' on the token): `startTokenDrag` calls
  // `surface.value.setPointerCapture(event.pointerId)`. In a real browser,
  // that redirects the mouseup/click synthesized from this same gesture onto
  // the SURFACE directly, at the token's own on-screen position — the click
  // never bubbles through the token at all, so a `.stop` on the token does
  // nothing. The surface's own @click handler used to read the just-set
  // selection and immediately "commit" it to that same cell, clearing the
  // selection and closing the panel the same tap just opened. Reproduced
  // here by firing 'click' on the surface at the token's own cell (column 2,
  // row 2 — see `snapshot` above), matching what the real browser dispatches.
  it('keeps the panel open when a click resolves to the selected token\'s own current cell', async () => {
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: { ...snapshot, columns: 10, rows: 10 }, imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: true }
    })
    const token = wrapper.get('[data-participant-id="player-0"]')
    await token.trigger('pointerdown')
    await token.trigger('pointerup')
    // Token is at column 2, row 2 (see `snapshot`); default cellSize ~40px,
    // so its own cell spans 80-120 — click at its center to simulate the
    // retargeted click landing exactly where the token visually sits.
    await wrapper.get('[data-board-surface]').trigger('click', { clientX: 100, clientY: 100 })
    expect(wrapper.findComponent(AttackAreaPanel).exists()).toBe(true)
    expect(wrapper.emitted('move')).toBeFalsy()
  })

  it('still commits a click on a genuinely different cell (select-then-tap-destination flow)', async () => {
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: { ...snapshot, columns: 10, rows: 10 }, imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: true }
    })
    const token = wrapper.get('[data-participant-id="player-0"]')
    await token.trigger('pointerdown')
    await token.trigger('pointerup')
    await wrapper.get('[data-board-surface]').trigger('click', { clientX: 320, clientY: 320 })
    expect(wrapper.emitted('move')).toBeTruthy()
  })
})
