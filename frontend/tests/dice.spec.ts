import { mount } from '@vue/test-utils'
import DiceRoller from '~/components/DiceRoller.vue'
import RollHistory from '~/components/RollHistory.vue'
import { createUuid } from '~/utils/createUuid'

vi.stubGlobal('useCampaignStore', () => ({ session: null, connection: 'online', snapshot: null }))
vi.stubGlobal('useCompanionApi', () => ({ rollDice: vi.fn() }))

describe('DiceRoller', () => {
  it('offers common dice and a manual expression', () => {
    const wrapper = mount(DiceRoller)
    expect(wrapper.text()).toContain('D20')
    expect(wrapper.text()).toContain('D6')
    expect(wrapper.get('#dice-expression').attributes('placeholder')).toBe('2d6+3')
  })

  it('creates a UUID without depending on crypto.randomUUID', () => {
    expect(createUuid()).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/)
  })

  it('shows repeated and compound results as separate attempts', () => {
    const wrapper = mount(RollHistory, { props: { rolls: [{
      id: 'roll-1', participantId: 'p1', displayName: 'Mestre', expression: '2#2d3+1d8+7',
      values: [1, 2, 5], modifier: 7, total: 15, createdAt: new Date().toISOString(),
      results: [
        { sequence: 1, terms: [{ expression: '2d3', values: [1, 2], subtotal: 3 }, { expression: '1d8', values: [5], subtotal: 5 }], modifier: 7, total: 15 },
        { sequence: 2, terms: [{ expression: '2d3', values: [2, 3], subtotal: 5 }, { expression: '1d8', values: [8], subtotal: 8 }], modifier: 7, total: 20 }
      ]
    }] } })
    expect(wrapper.text()).toContain('#1')
    expect(wrapper.text()).toContain('#2')
    expect(wrapper.text()).toContain('2d3 [1, 2]')
    expect(wrapper.text()).toContain('1d8 [8]')
  })
})
