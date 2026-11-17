import { mount } from '@vue/test-utils'
import BattlemapBoard from '~/components/battlemap/BattlemapBoard.vue'
import AttackAreaPanel from '~/components/battlemap/AttackAreaPanel.vue'
import type { BattlemapSnapshot } from '~/types/campaign'

function markerSnapshot(overrides: Partial<BattlemapSnapshot> = {}): BattlemapSnapshot {
  return {
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
      { participantId: 'marker-1', displayName: 'Lobo', initials: 'LO', colorKey: 1, column: 3, row: 3, positionVersion: 'v1', isMarker: true, ownerParticipantId: 'player-0' }
    ],
    ...overrides
  }
}

describe('BattlemapBoard marker tokens', () => {
  it('lets the owning player control their own marker', async () => {
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: markerSnapshot(), imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: true }
    })
    const marker = wrapper.get('[data-participant-id="marker-1"]')
    await marker.trigger('pointerdown')
    await marker.trigger('pointerup')
    expect(wrapper.findComponent(AttackAreaPanel).exists()).toBe(true)
  })

  it('does not let another player control someone else\'s marker', async () => {
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: markerSnapshot(), imageSource: 'blob:test', currentParticipantId: 'player-1', isMaster: false, online: true }
    })
    const marker = wrapper.get('[data-participant-id="marker-1"]')
    await marker.trigger('pointerdown')
    await marker.trigger('pointerup')
    expect(wrapper.findComponent(AttackAreaPanel).exists()).toBe(false)
  })

  it('lets the master control any marker', async () => {
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: markerSnapshot(), imageSource: 'blob:test', currentParticipantId: 'master', isMaster: true, online: true }
    })
    const marker = wrapper.get('[data-participant-id="marker-1"]')
    await marker.trigger('pointerdown')
    await marker.trigger('pointerup')
    expect(wrapper.findComponent(AttackAreaPanel).exists()).toBe(true)
  })

  it('shows an "Inserir token" control that emits create-marker with the entered label', async () => {
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: markerSnapshot({ tokens: [] }), imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: true }
    })
    await wrapper.get('[data-new-marker-label]').setValue('Familiar')
    await wrapper.get('[data-new-marker-submit]').trigger('click')
    expect(wrapper.emitted('create-marker')?.[0]).toEqual(['Familiar'])
  })

  it('offers a "Remover token" action in the panel only for a marker, emitting marker-remove', async () => {
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: markerSnapshot(), imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: true }
    })
    const marker = wrapper.get('[data-participant-id="marker-1"]')
    await marker.trigger('pointerdown')
    await marker.trigger('pointerup')
    const panel = wrapper.findComponent(AttackAreaPanel)
    expect(panel.props('isMarker')).toBe(true)
    await panel.get('.marker-remove-button').trigger('click')
    expect(wrapper.emitted('marker-remove')?.[0]).toEqual(['marker-1'])
  })

  it('does not offer "Remover token" for a non-marker token', async () => {
    const nonMarker = markerSnapshot({
      tokens: [{ participantId: 'player-0', displayName: 'Lia', initials: 'LI', colorKey: 0, column: 2, row: 2, positionVersion: 'v1' }]
    })
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: nonMarker, imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: true }
    })
    const token = wrapper.get('[data-participant-id="player-0"]')
    await token.trigger('pointerdown')
    await token.trigger('pointerup')
    expect(wrapper.findComponent(AttackAreaPanel).props('isMarker')).toBe(false)
  })
})
