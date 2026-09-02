import { mount } from '@vue/test-utils'
import BattlemapBoard from '~/components/battlemap/BattlemapBoard.vue'
import type { BattlemapSnapshot } from '~/types/campaign'

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
    await wrapper.get('[data-participant-id="player-0"]').trigger('click')
    await wrapper.get('[data-board-surface]').trigger('click', { clientX: 120, clientY: 120 })
    expect(wrapper.emitted('move')).toBeTruthy()
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
    await wrapper.get('[data-participant-id="player-0"]').trigger('click')
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
    await wrapper.get('[data-battlemap-token]').trigger('pointerdown')
    await surface.trigger('pointerup')
    expect(wrapper.emitted('move')).toContainEqual(['player-0', null, null])
  })
})
