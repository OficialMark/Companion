import { mount } from '@vue/test-utils'
import BattlemapBoard from '~/components/battlemap/BattlemapBoard.vue'
import AttackAreaPanel from '~/components/battlemap/AttackAreaPanel.vue'
import FogToolbar from '~/components/battlemap/FogToolbar.vue'
import type { BattlemapSnapshot, BattlemapToken, Combatant } from '~/types/campaign'
import { encodeMask, fillMask, setCell } from '~/utils/fogMask'

function map(tokens: BattlemapToken[], overrides: Partial<BattlemapSnapshot> = {}): BattlemapSnapshot {
  return {
    id: 'map-1', encounterId: 'encounter-1', columns: 10, rows: 10, isGridVisible: true,
    revision: 'r', imageRevision: 'i', imageUrl: 'blob:test', imageMediaType: 'image/png',
    imageFileName: 'map.png', updatedAt: new Date().toISOString(), tokens, ...overrides
  }
}
const lia: BattlemapToken = { participantId: 'player-0', displayName: 'Lia', initials: 'LI', colorKey: 0, column: 2, row: 2, positionVersion: 'v1' }
const goblin: BattlemapToken = { participantId: 'goblin-1', displayName: 'Goblin', initials: 'GO', colorKey: 1, column: 5, row: 5, positionVersion: 'v1', isCreature: true }
function creature(overrides: Partial<Combatant> = {}): Combatant {
  return {
    id: 'goblin-1', participantId: null, name: 'Goblin', initiativeModifier: 0, initiative: 10, creationOrder: 0,
    isCurrent: false, currentHitPoints: 5, maxHitPoints: 7, ...overrides
  }
}
/** jsdom has no PointerEvent: dispatch a MouseEvent carrying button/coords (+ pointerId). */
async function pointer(target: { element: Element }, type: string, init: { button?: number; clientX?: number; clientY?: number; pointerId?: number } = {}) {
  const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: init.button ?? 0, clientX: init.clientX ?? 0, clientY: init.clientY ?? 0 })
  Object.defineProperty(event, 'pointerId', { value: init.pointerId ?? 1 })
  target.element.dispatchEvent(event)
  await nextTick()
}

/** Client coordinates of a token's center: map position shifted by the board's pan transform. */
function clientOf(wrapper: { get: (selector: string) => { attributes: (name: string) => string | undefined } }, tokenId: string) {
  const numbers = (value: string | undefined) => (value ?? '').match(/-?[\d.]+/g)!.map(Number)
  const [tx, ty] = numbers(wrapper.get(`[data-participant-id="${tokenId}"]`).attributes('transform'))
  const [px, py] = numbers(wrapper.get('[data-board-surface] > g').attributes('transform'))
  return { clientX: tx! + px!, clientY: ty! + py! }
}

function board(props: Record<string, unknown>) {
  return mount(BattlemapBoard, { props: { imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: true, ...props } as never })
}

describe('US1: left click opens the token menu and keeps it open', () => {
  async function tap(wrapper: ReturnType<typeof board>, selector: string, clickAt: { clientX: number; clientY: number }) {
    const token = wrapper.get(selector)
    await pointer(token, 'pointerdown', { button: 0, clientX: clickAt.clientX, clientY: clickAt.clientY })
    await pointer(token, 'pointerup', { button: 0, clientX: clickAt.clientX, clientY: clickAt.clientY })
    // What the browser does next: a click retargeted to the capturing surface.
    await wrapper.get('[data-board-surface]').trigger('click', clickAt)
  }

  it('2x2 token: the synthesized click over another cell neither moves the token nor closes the menu', async () => {
    const wrapper = board({ battlemap: map([{ ...lia, footprintSize: 2 }]) })
    // Lower-right quarter of a 2x2 token at (2,2) resolves to cell (3,3), not the anchor.
    const cell = Number(wrapper.get('[data-participant-id="player-0"] .token-body').attributes('r')) / .37 / 2
    await tap(wrapper, '[data-participant-id="player-0"]', { clientX: cell * 3.6, clientY: cell * 3.6 })
    expect(wrapper.emitted('move')).toBeFalsy()
    expect(wrapper.findComponent(AttackAreaPanel).exists()).toBe(true)
  })

  it('stacked token: the synthesized click is ignored too', async () => {
    const wrapper = board({ battlemap: map([lia, { ...lia, participantId: 'marker-1', displayName: 'Lobo', isMarker: true, ownerParticipantId: 'player-0' }]) })
    await tap(wrapper, '[data-participant-id="marker-1"]', { clientX: 1, clientY: 1 })
    expect(wrapper.emitted('move')).toBeFalsy()
    expect(wrapper.findComponent(AttackAreaPanel).props('tokenName')).toBe('Lobo')
  })

  it('dragging still moves the token', async () => {
    const wrapper = board({ battlemap: map([lia]) })
    const token = wrapper.get('[data-participant-id="player-0"]')
    await pointer(token, 'pointerdown', { button: 0, clientX: 10, clientY: 10 })
    await pointer(wrapper.get('[data-board-surface]'), 'pointermove', { clientX: 200, clientY: 200 })
    await pointer(wrapper.get('[data-board-surface]'), 'pointerup', { clientX: 200, clientY: 200 })
    expect(wrapper.emitted('move')?.[0]?.[0]).toBe('player-0')
  })

  it('right button opens the menu without starting a drag; middle button does nothing', async () => {
    const wrapper = board({ battlemap: map([lia]) })
    const token = wrapper.get('[data-participant-id="player-0"]')
    await pointer(token, 'pointerdown', { button: 1 })
    expect(wrapper.findComponent(AttackAreaPanel).exists()).toBe(false)
    await pointer(token, 'pointerdown', { button: 2, clientX: 10, clientY: 10 })
    expect(wrapper.findComponent(AttackAreaPanel).exists()).toBe(true)
    await pointer(wrapper.get('[data-board-surface]'), 'pointermove', { clientX: 300, clientY: 300 })
    await pointer(wrapper.get('[data-board-surface]'), 'pointerup', { clientX: 300, clientY: 300 })
    expect(wrapper.emitted('move')).toBeFalsy()
  })

  it('the board suppresses the browser context menu', async () => {
    const wrapper = board({ battlemap: map([lia]) })
    const event = new MouseEvent('contextmenu', { bubbles: true, cancelable: true })
    wrapper.get('[data-board-surface]').element.dispatchEvent(event)
    expect(event.defaultPrevented).toBe(true)
  })
})

describe('US2: condition badges', () => {
  it('draws an icon badge per condition and collapses extras into +N', () => {
    const conditions = ['Poisoned', 'Bleeding', 'Paralyzed', 'Prone', 'Burning'] as const
    const wrapper = board({ battlemap: map([{ ...lia, statusConditions: [...conditions] }]) })
    const badges = wrapper.findAll('[data-participant-id="player-0"] [data-status-condition]')
    expect(badges).toHaveLength(3)
    expect(badges.map(item => item.attributes('aria-label'))).toEqual(['Envenenado', 'Sangrando', 'Paralisado'])
    expect(badges[0]!.find('[data-icon="lucide:biohazard"]').exists()).toBe(true)
    const overflow = wrapper.get('[data-participant-id="player-0"] [data-status-condition-overflow]')
    expect(overflow.text()).toContain('+2')
    expect(overflow.attributes('aria-label')).toContain('Caído')
    expect(overflow.attributes('aria-label')).toContain('Em chamas')
    expect(wrapper.get('[data-participant-id="player-0"]').attributes('aria-label')).toContain('condições: Envenenado, Sangrando, Paralisado, Caído, Em chamas')
  })

  it('menu chips show the icon, are toggles, and report how many are active', async () => {
    const wrapper = mount(AttackAreaPanel, {
      props: { draft: { shape: 'Circle', radius: 2, width: null, directionDegrees: null }, hasExisting: false, appearanceDraft: { footprintSize: 1, statusConditions: ['Bleeding'] } }
    })
    const bleeding = wrapper.get('[data-condition="Bleeding"]')
    expect(bleeding.attributes('aria-pressed')).toBe('true')
    expect(bleeding.find('[data-icon="lucide:droplets"]').exists()).toBe(true)
    expect(wrapper.get('[data-tab="conditions"]').text()).toContain('1')
    await wrapper.get('[data-condition="Paralyzed"]').trigger('click')
    expect(wrapper.emitted('appearance-commit')?.[0]?.[0]).toEqual({ footprintSize: 1, statusConditions: ['Bleeding', 'Paralyzed'] })
  })
})

describe('US3: HP visibility', () => {
  it('master sees the toggle for creatures only and it emits the new state', async () => {
    const wrapper = board({ battlemap: map([lia, goblin]), isMaster: true, currentParticipantId: 'master', combatants: [creature()] })
    await pointer(wrapper.get('[data-participant-id="goblin-1"]'), 'pointerdown', { button: 2 })
    const panel = wrapper.findComponent(AttackAreaPanel)
    expect(panel.props('hitPointsHidden')).toBe(false)
    await panel.get('[data-hp-visibility-toggle]').setValue(false)
    expect(wrapper.emitted('hit-points-visibility')?.[0]).toEqual(['goblin-1', true])

    await pointer(wrapper.get('[data-participant-id="goblin-1"]'), 'pointerdown', { button: 2 }) // close
    await pointer(wrapper.get('[data-participant-id="player-0"]'), 'pointerdown', { button: 2 })
    expect(wrapper.findComponent(AttackAreaPanel).find('[data-hp-visibility-toggle]').exists()).toBe(false)
  })

  it('master sees a hidden-HP indicator; players get no bar at all', () => {
    const master = board({ battlemap: map([goblin]), isMaster: true, currentParticipantId: 'master', combatants: [creature({ isHitPointsHidden: true })] })
    expect(master.find('[data-participant-id="goblin-1"] [data-health-fill]').exists()).toBe(true)
    expect(master.find('[data-participant-id="goblin-1"] [data-hp-hidden-indicator]').exists()).toBe(true)

    // What the server sends a player for a hidden creature: null HP.
    const player = board({ battlemap: map([goblin]), combatants: [creature({ isHitPointsHidden: true, currentHitPoints: null, maxHitPoints: null })] })
    expect(player.find('[data-participant-id="goblin-1"] [data-health-fill]').exists()).toBe(false)
    expect(player.find('[data-hp-hidden-indicator]').exists()).toBe(false)
  })

  it('toolbar quick action hides or shows every creature at once', async () => {
    const wrapper = board({ battlemap: map([goblin]), isMaster: true, currentParticipantId: 'master', combatants: [creature()] })
    await wrapper.get('[data-hp-visibility-all]').trigger('click')
    expect(wrapper.emitted('hit-points-visibility')?.[0]).toEqual([null, true])
    await wrapper.setProps({ combatants: [creature({ isHitPointsHidden: true })] } as never)
    await wrapper.get('[data-hp-visibility-all]').trigger('click')
    expect(wrapper.emitted('hit-points-visibility')?.[1]).toEqual([null, false])
  })
})

describe('US4: fog of war', () => {
  const half = (() => { const mask = fillMask(10, 10, false); for (let c = 0; c < 10; c++) for (let r = 0; r < 5; r++) setCell(mask, 10, 10, c, r, true); return encodeMask(mask) })()

  it('players see an opaque layer, the master a translucent one', () => {
    const player = board({ battlemap: map([lia], { fogMask: half }) })
    const layer = player.get('[data-fog-layer]')
    expect(layer.classes()).not.toContain('master')
    expect(layer.attributes('d')).toMatch(/^M0 0h/)
    const master = board({ battlemap: map([lia], { fogMask: half }), isMaster: true, currentParticipantId: 'master' })
    expect(master.get('[data-fog-layer]').classes()).toContain('master')
    expect(board({ battlemap: map([lia]) }).find('[data-fog-layer]').exists()).toBe(false)
  })

  it('only the master gets the fog tool', () => {
    expect(board({ battlemap: map([lia]) }).find('[data-fog-toggle]').exists()).toBe(false)
    expect(board({ battlemap: map([lia]), isMaster: true, currentParticipantId: 'master' }).find('[data-fog-toggle]').exists()).toBe(true)
  })

  it('in fog mode, dragging paints and commits one mask; tokens are not dragged', async () => {
    const wrapper = board({ battlemap: map([lia]), isMaster: true, currentParticipantId: 'master' })
    await wrapper.get('[data-fog-toggle]').trigger('click')
    expect(wrapper.findComponent(FogToolbar).exists()).toBe(true)
    const surface = wrapper.get('[data-board-surface]')
    const at = clientOf(wrapper, 'player-0')
    await pointer(surface, 'pointerdown', { pointerId: 1, button: 0, ...at })
    await pointer(surface, 'pointermove', { pointerId: 1, clientX: at.clientX + 60, clientY: at.clientY })
    await pointer(surface, 'pointerup', { pointerId: 1, clientX: at.clientX + 60, clientY: at.clientY })
    const commits = wrapper.emitted('fog-commit')
    expect(commits).toHaveLength(1)
    expect(typeof commits![0]![0]).toBe('string')
    expect(wrapper.find('[data-fog-layer]').exists()).toBe(true)
    // A token press in fog mode does nothing.
    await pointer(wrapper.get('[data-participant-id="player-0"]'), 'pointerdown', { button: 0 })
    expect(wrapper.findComponent(AttackAreaPanel).exists()).toBe(false)
    expect(wrapper.emitted('move')).toBeFalsy()
  })

  it('a second finger cancels the stroke instead of committing it', async () => {
    const wrapper = board({ battlemap: map([lia]), isMaster: true, currentParticipantId: 'master' })
    await wrapper.get('[data-fog-toggle]').trigger('click')
    const surface = wrapper.get('[data-board-surface]')
    await pointer(surface, 'pointerdown', { pointerId: 1, button: 0, ...clientOf(wrapper, 'player-0') })
    expect(wrapper.find('[data-fog-layer]').exists()).toBe(true) // live preview while painting
    await pointer(surface, 'pointerdown', { pointerId: 2, button: 0, clientX: 90, clientY: 90 })
    await pointer(surface, 'pointerup', { pointerId: 2 })
    await pointer(surface, 'pointerup', { pointerId: 1 })
    expect(wrapper.emitted('fog-commit')).toBeFalsy()
    expect(wrapper.find('[data-fog-layer]').exists()).toBe(false)
  })

  it('cover all / reveal all commit full and empty masks, Escape leaves fog mode', async () => {
    const wrapper = board({ battlemap: map([lia]), isMaster: true, currentParticipantId: 'master' })
    await wrapper.get('[data-fog-toggle]').trigger('click')
    await wrapper.get('[data-fog-cover-all]').trigger('click')
    await wrapper.get('[data-fog-reveal-all]').trigger('click')
    const commits = wrapper.emitted('fog-commit')!
    expect(commits[0]![0]).toBe(encodeMask(fillMask(10, 10, true)))
    expect(commits[1]![0]).toBeNull()
    await wrapper.get('[data-board-surface]').trigger('keydown', { key: 'Escape' })
    expect(wrapper.findComponent(FogToolbar).exists()).toBe(false)
  })

  it('master sees creatures under full fog dimmed', () => {
    const wrapper = board({ battlemap: map([goblin], { fogMask: encodeMask(fillMask(10, 10, true)) }), isMaster: true, currentParticipantId: 'master' })
    expect(wrapper.get('[data-participant-id="goblin-1"]').classes()).toContain('fogged')
  })
})

