import { mount } from '@vue/test-utils'
import BattlemapBoard from '~/components/battlemap/BattlemapBoard.vue'
import AttackAreaPanel from '~/components/battlemap/AttackAreaPanel.vue'
import type { BattlemapSnapshot } from '~/types/campaign'

function baseSnapshot(overrides: Partial<BattlemapSnapshot> = {}): BattlemapSnapshot {
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
      { participantId: 'player-0', displayName: 'Lia', initials: 'LI', colorKey: 0, column: 2, row: 2, positionVersion: 'v1' },
      { participantId: 'goblin-1', displayName: 'Goblin', initials: 'GO', colorKey: 3, column: 5, row: 5, positionVersion: 'v1', isCreature: true }
    ],
    ...overrides
  }
}

describe('AttackAreaPanel', () => {
  it('shows the 4 shape options and defaults to Circle with a radius slider', () => {
    const wrapper = mount(AttackAreaPanel, {
      props: { draft: { shape: 'Circle', radius: 2, width: null, directionDegrees: null }, hasExisting: false }
    })
    const shapeButtons = wrapper.findAll('.attack-area-shape')
    expect(shapeButtons).toHaveLength(4)
    expect(wrapper.find('.attack-area-shape.selected').text()).toContain('Círculo')
    expect(wrapper.findAll('input[type="range"]')).toHaveLength(1)
    expect(wrapper.find('.attack-area-actions .button.ghost.danger').exists()).toBe(false)
  })

  it('shows width and direction sliders for Cone/Cylinder, and emits change on radius drag', async () => {
    const wrapper = mount(AttackAreaPanel, {
      props: { draft: { shape: 'Cone', radius: 4, width: 2, directionDegrees: 90 }, hasExisting: false }
    })
    expect(wrapper.findAll('input[type="range"]')).toHaveLength(3)
    await wrapper.get('input[type="range"]').setValue('6')
    expect(wrapper.emitted('change')?.[0]?.[0]).toMatchObject({ shape: 'Cone', radius: 6 })
  })

  it('commits on slider release (change event) and shows Remover when an area already exists', async () => {
    const wrapper = mount(AttackAreaPanel, {
      props: { draft: { shape: 'Circle', radius: 2, width: null, directionDegrees: null }, hasExisting: true }
    })
    await wrapper.get('input[type="range"]').trigger('change')
    expect(wrapper.emitted('commit')).toBeTruthy()
    await wrapper.get('.attack-area-actions .button.ghost.danger').trigger('click')
    expect(wrapper.emitted('remove')).toBeTruthy()
    expect(wrapper.emitted('close')).toBeTruthy()
  })

  it('selecting a shape emits change and commit immediately', async () => {
    const wrapper = mount(AttackAreaPanel, {
      props: { draft: { shape: 'Circle', radius: 2, width: null, directionDegrees: null }, hasExisting: false }
    })
    await wrapper.findAll('.attack-area-shape')[2]!.trigger('click')
    expect(wrapper.emitted('change')?.[0]?.[0]).toMatchObject({ shape: 'Cone' })
    expect(wrapper.emitted('commit')?.[0]?.[0]).toMatchObject({ shape: 'Cone' })
  })
})

describe('BattlemapBoard attack areas', () => {
  it('opens the AttackAreaPanel when the player clicks their own token (US1)', async () => {
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: baseSnapshot(), imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: true }
    })
    expect(wrapper.findComponent(AttackAreaPanel).exists()).toBe(false)
    const token = wrapper.get('[data-participant-id="player-0"]')
    await token.trigger('pointerdown')
    await token.trigger('pointerup')
    expect(wrapper.findComponent(AttackAreaPanel).exists()).toBe(true)
  })

  it('does not open the panel for a token the player does not control (US3)', async () => {
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: baseSnapshot(), imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: true }
    })
    const goblin = wrapper.get('[data-participant-id="goblin-1"]')
    await goblin.trigger('pointerdown')
    await goblin.trigger('pointerup')
    expect(wrapper.findComponent(AttackAreaPanel).exists()).toBe(false)
  })

  it('lets the master open the panel for any token, including creatures (US3)', async () => {
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: baseSnapshot(), imageSource: 'blob:test', currentParticipantId: 'master', isMaster: true, online: true }
    })
    const goblin = wrapper.get('[data-participant-id="goblin-1"]')
    await goblin.trigger('pointerdown')
    await goblin.trigger('pointerup')
    expect(wrapper.findComponent(AttackAreaPanel).exists()).toBe(true)
  })

  it('closes the panel (keeping the drawn shape) after committing, and emits attack-area-commit (US1)', async () => {
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: baseSnapshot(), imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: true }
    })
    const token = wrapper.get('[data-participant-id="player-0"]')
    await token.trigger('pointerdown')
    await token.trigger('pointerup')
    await wrapper.findComponent(AttackAreaPanel).get('input[type="range"]').trigger('change')
    expect(wrapper.emitted('attack-area-commit')?.[0]).toEqual(['player-0', { shape: 'Circle', radius: 2, width: null, directionDegrees: null }])
    await wrapper.findComponent(AttackAreaPanel).get('.attack-area-actions .button.primary').trigger('click')
    expect(wrapper.findComponent(AttackAreaPanel).exists()).toBe(false)
  })

  it('reopens the panel pre-loaded and allows removal when clicking an existing drawn area (US2)', async () => {
    const withArea = baseSnapshot({
      tokens: [{
        participantId: 'player-0', displayName: 'Lia', initials: 'LI', colorKey: 0, column: 2, row: 2, positionVersion: 'v1',
        attackArea: { id: 'a1', shape: 'Square', radius: 3, width: null, directionDegrees: null, updatedAt: new Date().toISOString() }
      }]
    })
    const wrapper = mount(BattlemapBoard, {
      props: { battlemap: withArea, imageSource: 'blob:test', currentParticipantId: 'player-0', isMaster: false, online: true }
    })
    await wrapper.get('.attack-area-shape-drawing.interactive').trigger('click')
    const panel = wrapper.findComponent(AttackAreaPanel)
    expect(panel.exists()).toBe(true)
    expect(panel.props('draft')).toEqual({ shape: 'Square', radius: 3, width: null, directionDegrees: null })
    expect(panel.props('hasExisting')).toBe(true)
    await panel.get('.attack-area-actions .button.ghost.danger').trigger('click')
    expect(wrapper.emitted('attack-area-remove')?.[0]).toEqual(['player-0'])
  })
})
