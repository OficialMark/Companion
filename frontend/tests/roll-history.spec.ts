import { mount } from '@vue/test-utils'
import RollHistory from '~/components/RollHistory.vue'
import type { DiceRoll } from '~/types/campaign'

function roll(overrides: Partial<DiceRoll>): DiceRoll {
  return {
    id: 'roll-1', participantId: 'p1', displayName: 'Lia', expression: '1d20',
    values: [], modifier: 0, total: 0, createdAt: new Date().toISOString(), results: [],
    ...overrides
  }
}

describe('RollHistory (spec 011)', () => {
  it('shows both executions of advantage and highlights the selected one', () => {
    const wrapper = mount(RollHistory, { props: { rolls: [roll({
      expression: '2#(d20 + (FOR) 3) maior', values: [17], modifier: 3, total: 20,
      results: [{
        sequence: 1, terms: [{ expression: '1d20', values: [17], subtotal: 17 }], modifier: 3, total: 20,
        modifiers: [{ label: 'FOR', value: 3 }], selection: 'maior', selectedExecution: 1,
        executions: [
          { terms: [{ expression: '1d20', values: [4], subtotal: 4 }], modifier: 3, total: 7 },
          { terms: [{ expression: '1d20', values: [17], subtotal: 17 }], modifier: 3, total: 20 }
        ]
      }]
    })] } })

    const executions = wrapper.findAll('.execution-result')
    expect(executions).toHaveLength(2)
    expect(executions[0]!.text()).toContain('1d20 [4]')
    expect(executions[0]!.text()).toContain('= 7')
    expect(executions[1]!.text()).toContain('= 20')
    const selected = wrapper.findAll('[data-selected-execution]')
    expect(selected).toHaveLength(1)
    expect(selected[0]!.text()).toContain('1d20 [17]')
    expect(wrapper.get('.selection-label').text()).toBe('maior')
    expect(wrapper.text()).toContain('(FOR) 3')
    expect(wrapper.get('.total').text()).toBe('20')
  })

  it('shows "menor" and keeps the first execution on a tie', () => {
    const wrapper = mount(RollHistory, { props: { rolls: [roll({
      expression: '2#(d20) menor', total: 9,
      results: [{
        sequence: 1, terms: [{ expression: '1d20', values: [9], subtotal: 9 }], modifier: 0, total: 9,
        modifiers: [], selection: 'menor', selectedExecution: 0,
        executions: [
          { terms: [{ expression: '1d20', values: [9], subtotal: 9 }], modifier: 0, total: 9 },
          { terms: [{ expression: '1d20', values: [9], subtotal: 9 }], modifier: 0, total: 9 }
        ]
      }]
    })] } })

    expect(wrapper.get('.selection-label').text()).toBe('menor')
    const executions = wrapper.findAll('.execution-result')
    expect(executions[0]!.attributes('data-selected-execution')).toBe('true')
    expect(executions[1]!.attributes('data-selected-execution')).toBeUndefined()
  })

  it('shows labeled and negative modifiers in the order of the expression', () => {
    const wrapper = mount(RollHistory, { props: { rolls: [roll({
      expression: 'd20 + (prof) 2 - (FOR) 1 + 3', total: 14,
      results: [{
        sequence: 1, terms: [{ expression: '1d20', values: [10], subtotal: 10 }], modifier: 4, total: 14,
        modifiers: [{ label: 'prof', value: 2 }, { label: 'FOR', value: -1 }, { label: null, value: 3 }]
      }]
    })] } })

    expect(wrapper.findAll('.modifier-result').map(item => item.text())).toEqual(['+ (prof) 2', '- (FOR) 1', '+3'])
    expect(wrapper.find('.execution-result').exists()).toBe(false)
    expect(wrapper.find('[data-selected-execution]').exists()).toBe(false)
  })

  it('keeps the legacy layout when the new fields are absent', () => {
    const wrapper = mount(RollHistory, { props: { rolls: [roll({
      expression: '1d20+5', total: 12,
      results: [{ sequence: 1, terms: [{ expression: '1d20', values: [7], subtotal: 7 }], modifier: 5, total: 12 }]
    })] } })

    expect(wrapper.findAll('.modifier-result').map(item => item.text())).toEqual(['+5'])
    expect(wrapper.text()).toContain('1d20 [7]')
    expect(wrapper.find('.selection-label').exists()).toBe(false)
  })
})
