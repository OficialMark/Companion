import { mount } from '@vue/test-utils'
import BattlemapBoard from '~/components/battlemap/BattlemapBoard.vue'
import type { BattlemapSnapshot, Combatant, Participant } from '~/types/campaign'

function combatant(overrides: Partial<Combatant> = {}): Combatant {
  return {
    id: 'combatant-1',
    participantId: null,
    name: 'Combatant',
    initiativeModifier: 0,
    initiative: null,
    creationOrder: 0,
    isCurrent: false,
    currentHitPoints: null,
    maxHitPoints: null,
    ...overrides
  }
}

function participant(overrides: Partial<Participant> = {}): Participant {
  return {
    id: 'participant-1',
    displayName: 'Participant',
    role: 'Player',
    isOnline: true,
    initiativeModifier: null,
    currentHitPoints: null,
    maxHitPoints: null,
    ...overrides
  }
}

const snapshot: BattlemapSnapshot = {
  id: 'map-1',
  encounterId: 'encounter-1',
  columns: 100,
  rows: 100,
  isGridVisible: true,
  revision: 'revision',
  imageRevision: 'image-revision',
  imageUrl: 'blob:test',
  imageMediaType: 'image/png',
  imageFileName: 'map.png',
  updatedAt: new Date().toISOString(),
  tokens: Array.from({ length: 20 }, (_, index) => ({
    participantId: `player-${index}`,
    displayName: `Player ${index}`,
    initials: `P${index}`.slice(0, 3),
    colorKey: index % 12,
    column: index,
    row: index,
    positionVersion: `v${index}`
  }))
}

describe('BattlemapBoard', () => {
  it('renders at most rows + columns + 2 grid lines and all tokens', () => {
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: snapshot, imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: true }
    })
    expect(wrapper.findAll('[data-grid-line]')).toHaveLength(202)
    expect(wrapper.findAll('[data-battlemap-token]')).toHaveLength(20)
  })

  it('selects the controlled token and emits a cell move', async () => {
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: { ...snapshot, columns: 10, rows: 10 }, imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: true }
    })
    const token = wrapper.get('[data-participant-id="player-0"]')
    await token.trigger('pointerdown')
    await token.trigger('pointerup')
    await wrapper.get('[data-board-surface]').trigger('click', { clientX: 120, clientY: 120 })
    expect(wrapper.emitted('move')).toBeTruthy()
  })

  it('keeps a token selected after a plain tap (press and release without dragging)', async () => {
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: { ...snapshot, columns: 10, rows: 10 }, imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: true }
    })
    const token = wrapper.get('[data-participant-id="player-0"]')
    await token.trigger('pointerdown')
    await token.trigger('pointerup')
    expect(wrapper.emitted('move')).toBeFalsy()
    expect(wrapper.text()).toContain('Selecione a célula de destino para Player 0')
  })

  it('preserves zoom when a synchronized snapshot updates token positions', async () => {
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: snapshot, imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: true }
    })
    await wrapper.get('[aria-label="Aumentar zoom"]').trigger('click')
    const zoom = wrapper.get('.battlemap-toolbar span').text()
    await wrapper.setProps({ battlemap: { ...snapshot, tokens: snapshot.tokens.map(token => ({ ...token, column: token.column === 0 ? 4 : token.column })) } })
    expect(wrapper.get('.battlemap-toolbar span').text()).toBe(zoom)
  })

  it('shows creature pins but only lets the master select them', async () => {
    const creatureMap = {
      ...snapshot,
      tokens: [{ participantId: 'goblin-1', displayName: 'Goblin', initials: 'G', colorKey: 3, column: null, row: null, positionVersion: null, isCreature: true }]
    }
    const player = mount(BattlemapBoard, {
      props: { battlemap: creatureMap, imageSource: 'blob:test', currentParticipantId: 'goblin-1', isMaster: false, online: true }
    })
    expect(player.get('[data-participant-id="goblin-1"]').attributes('disabled')).toBeDefined()

    const master = mount(BattlemapBoard, {
      props: { battlemap: creatureMap, imageSource: 'blob:test', currentParticipantId: 'master', isMaster: true, online: true }
    })
    await master.get('[data-participant-id="goblin-1"]').trigger('click')
    expect(master.text()).toContain('Selecione a célula de destino para Goblin')
  })

  it('blocks movement while offline', async () => {
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: snapshot, imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: false }
    })
    const token = wrapper.get('[data-participant-id="player-0"]')
    await token.trigger('pointerdown')
    await token.trigger('pointerup')
    expect(wrapper.emitted('move')).toBeFalsy()
    expect(wrapper.text()).toContain('Conexão necessária')
  })
  it('hides grid lines when the battlemap grid is invisible', () => {
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: { ...snapshot, isGridVisible: false }, imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: true }
    })
    expect(wrapper.findAll('[data-grid-line]')).toHaveLength(0)
  })

  it('moves a dragged pin to reserve when dropped outside the map', async () => {
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: { ...snapshot, columns: 10, rows: 10 }, imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: true }
    })
    const surface = wrapper.get('[data-board-surface]')
    vi.spyOn(surface.element, 'getBoundingClientRect').mockReturnValue({ left: 10, top: 10, width: 400, height: 400 } as DOMRect)
    // Built natively (not via .trigger's options merge) because jsdom's MouseEvent
    // defines clientX/clientY as getter-only own properties post-construction.
    wrapper.get('[data-battlemap-token]').element.dispatchEvent(
      new MouseEvent('pointerdown', { bubbles: true, cancelable: true, clientX: 20, clientY: 20 }))
    await nextTick()
    surface.element.dispatchEvent(
      new MouseEvent('pointerup', { bubbles: true, cancelable: true, clientX: -500, clientY: -500 }))
    await nextTick()
    expect(wrapper.emitted('move')).toContainEqual(['player-0', null, null])
  })

  it('highlights the token whose turn is current, and only during an active encounter', async () => {
    const combatants = [
      combatant({ id: 'c1', participantId: 'player-0', isCurrent: true }),
      combatant({ id: 'c2', participantId: 'player-1', isCurrent: false })
    ]
    const wrapper = mount(BattlemapBoard, {
      props: {
        battlemap: snapshot,
        imageSource: 'blob:test',
        currentParticipantId: 'player-0',
        isMaster: false,
        online: true,
        combatants,
        encounterStatus: 'Active'
      }
    })
    expect(wrapper.get('[data-participant-id="player-0"]').classes()).toContain('current-turn')
    expect(wrapper.get('[data-participant-id="player-1"]').classes()).not.toContain('current-turn')
  })

  it('moves the turn highlight when the current combatant changes', async () => {
    const combatants = [
      combatant({ id: 'c1', participantId: 'player-0', isCurrent: true }),
      combatant({ id: 'c2', participantId: 'player-1', isCurrent: false })
    ]
    const wrapper = mount(BattlemapBoard, {
      props: {
        battlemap: snapshot,
        imageSource: 'blob:test',
        currentParticipantId: 'player-0',
        isMaster: false,
        online: true,
        combatants,
        encounterStatus: 'Active'
      }
    })
    await wrapper.setProps({
      combatants: [
        combatant({ id: 'c1', participantId: 'player-0', isCurrent: false }),
        combatant({ id: 'c2', participantId: 'player-1', isCurrent: true })
      ]
    })
    expect(wrapper.get('[data-participant-id="player-0"]').classes()).not.toContain('current-turn')
    expect(wrapper.get('[data-participant-id="player-1"]').classes()).toContain('current-turn')
  })

  it('shows no turn highlight outside an active encounter', () => {
    const combatants = [combatant({ id: 'c1', participantId: 'player-0', isCurrent: true })]
    const wrapper = mount(BattlemapBoard, {
      props: {
        battlemap: snapshot,
        imageSource: 'blob:test',
        currentParticipantId: 'player-0',
        isMaster: false,
        online: true,
        combatants,
        encounterStatus: 'Preparing'
      }
    })
    expect(wrapper.get('[data-participant-id="player-0"]').classes()).not.toContain('current-turn')
  })

  it('resolves a creature token turn highlight via the combatant id, not participantId', () => {
    const creatureMap = {
      ...snapshot,
      tokens: [{ participantId: 'goblin-1', displayName: 'Goblin', initials: 'G', colorKey: 3, column: 0, row: 0, positionVersion: 'v1', isCreature: true }]
    }
    const combatants = [combatant({ id: 'goblin-1', participantId: null, isCurrent: true })]
    const wrapper = mount(BattlemapBoard, {
      props: {
        battlemap: creatureMap,
        imageSource: 'blob:test',
        currentParticipantId: 'master',
        isMaster: true,
        online: true,
        combatants,
        encounterStatus: 'Active'
      }
    })
    expect(wrapper.get('[data-participant-id="goblin-1"]').classes()).toContain('current-turn')
  })

  it('renders a health bar proportional to a combatant linked to the token', () => {
    const combatants = [combatant({ id: 'c1', participantId: 'player-0', currentHitPoints: 15, maxHitPoints: 20 })]
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: snapshot, imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: true, combatants }
    })
    const bar = wrapper.get('[data-participant-id="player-0"] [data-health-fill]')
    expect(bar.attributes('width')).toBe('75%')
  })

  it('falls back to the participant baseline HP when no combatant is linked yet', () => {
    const participants = [participant({ id: 'player-0', currentHitPoints: 5, maxHitPoints: 10 })]
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: snapshot, imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: true, participants }
    })
    expect(wrapper.find('[data-participant-id="player-0"] [data-health-fill]').exists()).toBe(true)
  })

  it('shows no health bar when neither a combatant nor a participant provides a max HP', () => {
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: snapshot, imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: true }
    })
    expect(wrapper.find('[data-participant-id="player-0"] [data-health-fill]').exists()).toBe(false)
  })

  it('never shows a health bar for a marker token', () => {
    const markerMap = {
      ...snapshot,
      tokens: [{ participantId: 'marker-1', displayName: 'Lobo', initials: 'L', colorKey: 2, column: 0, row: 0, positionVersion: 'v1', isMarker: true, ownerParticipantId: 'player-0' }]
    }
    const combatants = [combatant({ id: 'marker-1', participantId: null, currentHitPoints: 5, maxHitPoints: 5 })]
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: markerMap, imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: true, combatants }
    })
    // A marker's token id ('marker-1') coincidentally matching a combatant id must not leak a health bar onto it.
    expect(wrapper.find('[data-participant-id="marker-1"] [data-health-fill]').exists()).toBe(false)
  })
})
