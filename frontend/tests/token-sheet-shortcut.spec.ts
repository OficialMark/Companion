import { enableAutoUnmount, mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import BattlemapBoard from '~/components/battlemap/BattlemapBoard.vue'
import AttackAreaPanel from '~/components/battlemap/AttackAreaPanel.vue'
import CreatureRollPanel from '~/components/creatures/CreatureRollPanel.vue'
import { useCampaignStore } from '~/stores/campaign'
import { RESULT_HOLD_MS } from '~/utils/creatureRoll'
import type { BattlemapSnapshot } from '~/types/campaign'
import type { Creature } from '~/types/creatures'

// Every mounted board is unmounted after each test, so no right-click guard leaks into the next one.
enableAutoUnmount(afterEach)

// Spec 010: right-click on a token opens its sheet; a sheet roll's result stays 2 s on screen.

const snapshot: BattlemapSnapshot = {
  id: 'map-1', encounterId: 'e1', columns: 20, rows: 20, isGridVisible: true, revision: 'r', imageRevision: 'ir',
  imageUrl: 'blob:test', imageMediaType: 'image/png', imageFileName: 'map.png', updatedAt: new Date().toISOString(),
  tokens: [
    { participantId: 'goblin-1', displayName: 'Goblin 1', initials: 'G1', colorKey: 1, column: 3, row: 3, positionVersion: 'v1', creatureId: 'goblin', isCreature: true },
    { participantId: 'player-0', displayName: 'Lia', initials: 'LI', colorKey: 0, column: 2, row: 2, positionVersion: 'v1' }
  ]
} as BattlemapSnapshot

/** jsdom has no PointerEvent: a MouseEvent carrying button and pointerId. */
async function pointer(element: Element, type: string, button = 0, clientX = 0, clientY = 0) {
  const event = new MouseEvent(type, { bubbles: true, cancelable: true, button, clientX, clientY })
  Object.defineProperty(event, 'pointerId', { value: 1 })
  Object.defineProperty(event, 'isPrimary', { value: true })
  element.dispatchEvent(event)
  await nextTick()
}

function board(isMaster: boolean, currentParticipantId = isMaster ? 'master' : 'player-0', online = true) {
  return mount(BattlemapBoard, { props: { battlemap: snapshot, imageSource: 'blob:test', currentParticipantId, isMaster, online } })
}

describe('right-click on a battlemap token (spec 010)', () => {
  it('opens the creature sheet directly for the master, without the token menu', async () => {
    const wrapper = board(true)
    await pointer(wrapper.get('[data-participant-id="goblin-1"]').element, 'pointerdown', 2)
    expect(wrapper.emitted('open-creature-sheet')?.[0]).toEqual(['goblin-1', 'goblin', 'Goblin 1'])
    expect(wrapper.findComponent(AttackAreaPanel).exists()).toBe(false)
  })

  it('closes the menu of that token if it was open, so the sheet is not hidden behind it', async () => {
    const wrapper = board(true)
    const token = wrapper.get('[data-participant-id="goblin-1"]')
    await token.trigger('pointerdown'); await token.trigger('pointerup')
    expect(wrapper.findComponent(AttackAreaPanel).exists()).toBe(true)
    await pointer(token.element, 'pointerdown', 2)
    expect(wrapper.findComponent(AttackAreaPanel).exists()).toBe(false)
    expect(wrapper.emitted('open-creature-sheet')).toHaveLength(1)
  })

  it('right-click with ANOTHER token\'s menu open closes that menu too (no menu comes back after the sheet)', async () => {
    const wrapper = board(true)
    const player = wrapper.get('[data-participant-id="player-0"]')
    await player.trigger('pointerdown'); await player.trigger('pointerup')
    expect(wrapper.findComponent(AttackAreaPanel).exists()).toBe(true)
    await pointer(wrapper.get('[data-participant-id="goblin-1"]').element, 'pointerdown', 2)
    expect(wrapper.emitted('open-creature-sheet')).toHaveLength(1)
    expect(wrapper.findComponent(AttackAreaPanel).exists()).toBe(false)
    // The next click on the board must not move the player that was selected before.
    await wrapper.get('[data-board-surface]').trigger('click', { clientX: 300, clientY: 300 })
    expect(wrapper.emitted('move')).toBeUndefined()
  })

  it('the browser\'s own context menu never opens over a token', () => {
    const wrapper = board(true)
    const event = new MouseEvent('contextmenu', { bubbles: true, cancelable: true })
    wrapper.get('[data-participant-id="goblin-1"]').element.dispatchEvent(event)
    expect(event.defaultPrevented).toBe(true)
  })

  it('a token without a sheet keeps the old behavior: right-click opens its menu', async () => {
    const wrapper = board(true)
    await pointer(wrapper.get('[data-participant-id="player-0"]').element, 'pointerdown', 2)
    expect(wrapper.emitted('open-creature-sheet')).toBeUndefined()
    expect(wrapper.findComponent(AttackAreaPanel).exists()).toBe(true)
  })

  it('a player never opens a creature sheet (sheets are the master\'s for now)', async () => {
    const onGoblin = board(false)
    await pointer(onGoblin.get('[data-participant-id="goblin-1"]').element, 'pointerdown', 2)
    await onGoblin.get('[data-participant-id="goblin-1"]').trigger('keydown', { key: 'F10', shiftKey: true })
    expect(onGoblin.emitted('open-creature-sheet')).toBeUndefined()
    const wrapper = board(false)
    await pointer(wrapper.get('[data-participant-id="player-0"]').element, 'pointerdown', 2)
    expect(wrapper.emitted('open-creature-sheet')).toBeUndefined()
    expect(wrapper.findComponent(AttackAreaPanel).exists()).toBe(true)
  })

  it('left click still opens the menu and right-click never starts a drag', async () => {
    const wrapper = board(true)
    const token = wrapper.get('[data-participant-id="goblin-1"]')
    const surface = wrapper.get('[data-board-surface]').element
    await pointer(token.element, 'pointerdown', 2, 100, 100)
    await pointer(surface, 'pointermove', 2, 400, 400) // several cells away
    await pointer(surface, 'pointerup', 2, 400, 400)
    expect(wrapper.emitted('move')).toBeUndefined()
    expect(wrapper.find('[data-participant-id="goblin-1"]').classes()).not.toContain('dragging')
    await token.trigger('pointerdown'); await token.trigger('pointerup')
    expect(wrapper.findComponent(AttackAreaPanel).exists()).toBe(true)
  })

  it('keyboard: Shift+F10 and the Menu key on a focused token do the same', async () => {
    const wrapper = board(true)
    const token = wrapper.get('[data-participant-id="goblin-1"]')
    await token.trigger('keydown', { key: 'F10', shiftKey: true })
    await token.trigger('keydown', { key: 'ContextMenu' })
    expect(wrapper.emitted('open-creature-sheet')).toHaveLength(2)
    await token.trigger('keydown', { key: 'F10', shiftKey: true, ctrlKey: true })
    expect(wrapper.emitted('open-creature-sheet')).toHaveLength(2)
  })

  it('keyboard and mouse share the guards: nothing in fog mode, nothing offline', async () => {
    const fogged = board(true)
    await fogged.get('[data-fog-toggle]').trigger('click')
    const token = fogged.get('[data-participant-id="goblin-1"]')
    await token.trigger('keydown', { key: 'F10', shiftKey: true })
    await pointer(token.element, 'pointerdown', 2)
    expect(fogged.emitted('open-creature-sheet')).toBeUndefined()

    const offline = board(true, 'master', false)
    await offline.get('[data-participant-id="goblin-1"]').trigger('keydown', { key: 'ContextMenu' })
    expect(offline.emitted('open-creature-sheet')).toBeUndefined()
  })

  it('tells the master about the shortcut (mouse and keyboard) only when there is a creature on the board', () => {
    expect(board(true).get('.battlemap-status').text()).toContain('Botão direito (ou Shift+F10) numa criatura abre a ficha.')
    expect(board(false).get('.battlemap-status').text()).not.toContain('Botão direito')
  })

  it('no hint while the only creature is still in the reserve (right-click there does nothing)', () => {
    const reserve = { ...snapshot, tokens: snapshot.tokens.map(token => token.creatureId ? { ...token, column: null, row: null } : token) }
    const wrapper = mount(BattlemapBoard, { props: { battlemap: reserve, imageSource: 'blob:test', currentParticipantId: 'master', isMaster: true, online: true } })
    expect(wrapper.get('.battlemap-status').text()).not.toContain('Botão direito')
  })

  it('a token with a sheet announces the shortcut to assistive technology; others do not', () => {
    const wrapper = board(true)
    expect(wrapper.get('[data-participant-id="goblin-1"]').attributes('aria-label')).toContain('Shift+F10 abre a ficha')
    expect(wrapper.get('[data-participant-id="player-0"]').attributes('aria-label')).not.toContain('Shift+F10')
  })
})

describe('browser context menu stays closed for board right-clicks (spec 010)', () => {
  beforeEach(() => vi.useFakeTimers())
  afterEach(() => vi.useRealTimers())

  /** A contextmenu event anywhere on the page (e.g. over the sheet that just opened); true when cancelled. */
  function browserMenuBlocked(target: EventTarget = document.body) {
    const event = new MouseEvent('contextmenu', { bubbles: true, cancelable: true })
    target.dispatchEvent(event)
    return event.defaultPrevented
  }

  it('right-click on a creature: blocked until 300 ms after the button goes up (Windows fires on release)', async () => {
    const wrapper = board(true)
    wrapper.element.ownerDocument.body.appendChild(wrapper.element)
    await pointer(wrapper.get('[data-participant-id="goblin-1"]').element, 'pointerdown', 2)
    expect(browserMenuBlocked()).toBe(true) // e.g. over the sheet overlay, outside the board
    await pointer(window as unknown as Element, 'pointerup', 2)
    vi.advanceTimersByTime(250)
    expect(browserMenuBlocked()).toBe(true) // the release-time event
    vi.advanceTimersByTime(100)
    expect(browserMenuBlocked()).toBe(false) // the rest of the page keeps its normal menu
    wrapper.unmount()
  })

  it('right-click on a token without a sheet (its menu opens) and right-drag pan on the surface: same', async () => {
    const wrapper = board(true)
    await pointer(wrapper.get('[data-participant-id="player-0"]').element, 'pointerdown', 2)
    expect(browserMenuBlocked()).toBe(true)
    await pointer(window as unknown as Element, 'pointerup', 2)
    vi.advanceTimersByTime(400)
    expect(browserMenuBlocked()).toBe(false)

    await pointer(wrapper.get('[data-board-surface]').element, 'pointerdown', 2)
    expect(browserMenuBlocked()).toBe(true)
    await pointer(window as unknown as Element, 'pointerup', 2)
    vi.advanceTimersByTime(400)
    expect(browserMenuBlocked()).toBe(false)
    wrapper.unmount()
  })

  it('keyboard (Menu key fires contextmenu on key UP, after focus moved into the sheet): blocked until 300 ms after keyup', async () => {
    const wrapper = board(true)
    await wrapper.get('[data-participant-id="goblin-1"]').trigger('keydown', { key: 'ContextMenu' })
    expect(wrapper.emitted('open-creature-sheet')).toHaveLength(1)
    expect(browserMenuBlocked()).toBe(true)
    window.dispatchEvent(new KeyboardEvent('keyup', { key: 'ContextMenu' }))
    vi.advanceTimersByTime(250)
    expect(browserMenuBlocked()).toBe(true)
    vi.advanceTimersByTime(100)
    expect(browserMenuBlocked()).toBe(false)
    wrapper.unmount()
  })

  it('after the release, the release-time menu event or a new press elsewhere ends the guard (fast right-clicks get the normal menu)', async () => {
    const wrapper = board(true)
    const goblin = wrapper.get('[data-participant-id="goblin-1"]').element
    await pointer(goblin, 'pointerdown', 2)
    await pointer(window as unknown as Element, 'pointerup', 2)
    expect(browserMenuBlocked()).toBe(true) // Windows' release-time event
    expect(browserMenuBlocked()).toBe(false) // the next one, right away, is a normal menu

    await pointer(goblin, 'pointerdown', 2)
    await pointer(window as unknown as Element, 'pointerup', 2)
    await pointer(document.body, 'pointerdown', 2) // a new right-click elsewhere, within 300 ms
    expect(browserMenuBlocked()).toBe(false)
  })

  it('a lost release (focus change, pen cancel) does not eat the next right-click elsewhere', async () => {
    const wrapper = board(true)
    await pointer(wrapper.get('[data-participant-id="goblin-1"]').element, 'pointerdown', 2)
    // no pointerup ever arrives
    await pointer(document.body, 'pointerdown', 2)
    expect(browserMenuBlocked()).toBe(false)
  })

  it('a press on the board while armed re-arms (the board gesture stays protected)', async () => {
    const wrapper = board(true)
    const goblin = wrapper.get('[data-participant-id="goblin-1"]').element
    await pointer(goblin, 'pointerdown', 2)
    await pointer(goblin, 'pointerdown', 2)
    expect(browserMenuBlocked()).toBe(true)
  })

  it('keyboard: only the menu key going up ends the gesture (Shift going up does not)', async () => {
    const wrapper = board(true)
    await wrapper.get('[data-participant-id="goblin-1"]').trigger('keydown', { key: 'F10', shiftKey: true })
    window.dispatchEvent(new KeyboardEvent('keyup', { key: 'Shift' }))
    vi.advanceTimersByTime(1_000)
    expect(browserMenuBlocked()).toBe(true)
    window.dispatchEvent(new KeyboardEvent('keyup', { key: 'F10' }))
    vi.advanceTimersByTime(400)
    expect(browserMenuBlocked()).toBe(false)
  })

  it('chorded right button (pressed during a left drag, reported only as a move) is guarded too', async () => {
    const wrapper = board(true)
    await pointer(wrapper.get('[data-board-surface]').element, 'pointermove', 2)
    expect(browserMenuBlocked()).toBe(true)
    await pointer(window as unknown as Element, 'pointerup', 2)
    vi.advanceTimersByTime(400)
    expect(browserMenuBlocked()).toBe(false)
  })

  it('left button and other buttons releasing do not arm or end the guard', async () => {
    const wrapper = board(true)
    await pointer(wrapper.get('[data-participant-id="goblin-1"]').element, 'pointerdown', 0)
    expect(browserMenuBlocked()).toBe(false)
    await pointer(wrapper.get('[data-participant-id="goblin-1"]').element, 'pointerdown', 2)
    await pointer(window as unknown as Element, 'pointerup', 0) // a left release does not end the right gesture
    vi.advanceTimersByTime(1_000)
    expect(browserMenuBlocked()).toBe(true)
    wrapper.unmount()
  })

  it('a release we never hear about still frees the menu after 10 s; unmounting frees it at once', async () => {
    const lost = board(true)
    await pointer(lost.get('[data-participant-id="goblin-1"]').element, 'pointerdown', 2)
    vi.advanceTimersByTime(9_900)
    expect(browserMenuBlocked()).toBe(true)
    vi.advanceTimersByTime(200)
    expect(browserMenuBlocked()).toBe(false)
    lost.unmount()

    const gone = board(true)
    await pointer(gone.get('[data-participant-id="goblin-1"]').element, 'pointerdown', 2)
    gone.unmount()
    expect(browserMenuBlocked()).toBe(false)
    expect(vi.getTimerCount()).toBe(0)
  })
})

describe('creature roll result hold (spec 010)', () => {
  const goblin = {
    id: 'goblin', name: 'Goblin', strength: 8, dexterity: 14, constitution: 10, intelligence: 10, wisdom: 8, charisma: 8,
    initiativeModifier: 2, proficiencyBonus: 2
  } as unknown as Creature
  const api = { rollDice: vi.fn(), revealDiceRoll: vi.fn() }
  vi.stubGlobal('useCompanionApi', () => api)
  vi.stubGlobal('useCampaignStore', () => useCampaignStore())

  function ready() {
    setActivePinia(createPinia())
    const campaign = useCampaignStore()
    campaign.session = { campaignId: 'c1', participantToken: 't', role: 'Master' } as never
    campaign.connection = 'online'
    campaign.snapshot = { diceRolls: [] } as never
    let n = 0
    api.rollDice.mockReset().mockImplementation(() => Promise.resolve({ id: `r${++n}`, results: [{ sequence: 1, terms: [], modifier: 0, total: 17 }] }))
    api.revealDiceRoll.mockReset().mockImplementation((_s: unknown, id: string) => Promise.resolve({ id, total: 17, expression: '1d20', results: [{ sequence: 1, terms: [], modifier: 0, total: 17 }] }))
    return mount(CreatureRollPanel, { props: { creature: goblin, preset: { count: 1, die: 20, ability: null, bonus: 0 } as never, label: 'Goblin 1' } })
  }

  beforeEach(() => vi.useFakeTimers())
  afterEach(() => vi.useRealTimers())

  async function rollAndReveal(wrapper: ReturnType<typeof ready>) {
    await wrapper.get('.roll-go').trigger('click')
    await vi.advanceTimersByTimeAsync(800) // spin + settle
    await flushPromises()
  }

  it('keeps the settled dice and total on screen for 2 s, then shows the last result line', async () => {
    const wrapper = ready()
    await rollAndReveal(wrapper)
    expect(wrapper.emitted('rolled')).toHaveLength(1) // history and preset are updated at once
    expect(useCampaignStore().snapshot!.diceRolls.map(roll => roll.id)).toEqual(['r1'])
    expect(wrapper.get('.creature-roll').attributes('data-roll-showing')).toBe('result')
    await vi.advanceTimersByTimeAsync(RESULT_HOLD_MS - 50)
    expect(wrapper.find('.roll-last').exists()).toBe(false)
    await vi.advanceTimersByTimeAsync(100)
    expect(wrapper.find('.roll-last').text()).toContain('17')
    expect(RESULT_HOLD_MS).toBe(2000)
  })

  it('the button is free during the hold and a new roll replaces the held result', async () => {
    const wrapper = ready()
    await rollAndReveal(wrapper)
    expect(wrapper.get('.roll-go').attributes('disabled')).toBeUndefined()
    await rollAndReveal(wrapper)
    expect(api.rollDice).toHaveBeenCalledTimes(2)
    await vi.advanceTimersByTimeAsync(RESULT_HOLD_MS - 100)
    expect(wrapper.find('.roll-last').exists()).toBe(false) // the second roll restarted the hold
    await vi.advanceTimersByTimeAsync(200)
    expect(wrapper.find('.roll-last').exists()).toBe(true)
  })

  it('a failed roll clears the animation at once and shows the error', async () => {
    const wrapper = ready()
    api.rollDice.mockRejectedValueOnce(new Error('Falhou'))
    await wrapper.get('.roll-go').trigger('click')
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain('Falhou')
    expect(wrapper.find('.roll-last').exists()).toBe(false)
    expect(wrapper.get('.creature-roll').attributes('data-roll-showing')).toBeUndefined()
  })

  it('closing the sheet mid-roll (before the reveal) still reveals the roll but leaves no timer', async () => {
    const wrapper = ready()
    await wrapper.get('.roll-go').trigger('click')
    await vi.advanceTimersByTimeAsync(100) // dice spinning, reveal not yet
    wrapper.unmount()
    await vi.advanceTimersByTimeAsync(1_000)
    await flushPromises()
    expect(api.revealDiceRoll).toHaveBeenCalledTimes(1) // the server roll is still made public
    expect(vi.getTimerCount()).toBe(0)
  })

  it('closing the sheet during the hold leaves no timer behind', async () => {
    const wrapper = ready()
    await rollAndReveal(wrapper)
    wrapper.unmount()
    expect(vi.getTimerCount()).toBe(0)
  })
})
