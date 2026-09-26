import { mount } from '@vue/test-utils'
import DiceRoller from '~/components/DiceRoller.vue'
import RollHistory from '~/components/RollHistory.vue'
import QuickDiceRoller from '~/components/QuickDiceRoller.vue'
import { createUuid } from '~/utils/createUuid'
import { diceForAnimation, diceWithResults } from '~/utils/diceAnimation'
import DiceRollAnimation from '~/components/DiceRollAnimation.vue'
import { useQuickRollModifier } from '~/composables/useQuickRollModifier'

const campaignStore: { session: null | Record<string, string>; connection: string; snapshot: null | { diceRolls: Record<string, unknown>[] } } = {
  session: null,
  connection: 'online',
  snapshot: null
}
const rollDice = vi.fn()
const revealDiceRoll = vi.fn()

vi.stubGlobal('useCampaignStore', () => campaignStore)
vi.stubGlobal('useCompanionApi', () => ({ rollDice, revealDiceRoll }))

describe('DiceRoller', () => {
  it('offers common dice and a manual expression', () => {
    const wrapper = mount(DiceRoller)
    expect(wrapper.text()).toContain('D20')
    expect(wrapper.text()).toContain('D6')
    expect(wrapper.get('input[placeholder="2d6+3"]' ).attributes('placeholder')).toBe('2d6+3')
  })

  it('creates a UUID without depending on crypto.randomUUID', () => {
    expect(createUuid()).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/)
  })

  it('expands every die in compound and repeated expressions, including non-standard faces', () => {
    const dice = diceForAnimation('2#2d4+d6+d8+d10+d12+d20+d100+1d7')
    expect(dice.map(die => die.faces)).toEqual([
      4, 4, 4, 4, 6, 6, 8, 8, 10, 10, 12, 12, 20, 20, 100, 100, 7, 7
    ])
  })

  it('regression: a non-standard die (e.g. d27) still produces an animated die instead of being dropped', () => {
    const dice = diceForAnimation('1d27')
    expect(dice.map(die => die.faces)).toEqual([27])
  })

  it('regression: a standard die mixed with a non-standard one animates both terms', () => {
    const dice = diceForAnimation('1d20+1d27')
    expect(dice.map(die => die.faces)).toEqual([20, 27])
  })

  it('applies the generic fallback shape to non-standard faces and keeps the dedicated shape for standard ones', () => {
    const dice = diceForAnimation('1d20+1d27')
    const wrapper = mount(DiceRollAnimation, { props: { dice, settled: false, totals: [] } })
    expect(wrapper.find('[data-die="d20"] .poly-die').classes()).toContain('poly-die-d20')
    expect(wrapper.find('[data-die="d27"] .poly-die').classes()).toContain('poly-die-generic')
    expect(wrapper.find('[data-die="d27"] .poly-die').classes()).not.toContain('poly-die-d27')
    wrapper.unmount()
  })

  it('renders one animated object for each expanded die', () => {
    const dice = diceForAnimation('2d6+d20+d100')
    const wrapper = mount(DiceRollAnimation, { props: { dice, settled: false, totals: [] } })
    expect(wrapper.findAll('[data-die="d6"]')).toHaveLength(2)
    expect(wrapper.findAll('[data-die="d20"]')).toHaveLength(1)
    expect(wrapper.findAll('[data-die="d100"]')).toHaveLength(1)
    expect(wrapper.attributes('aria-label')).toBe('Dados rolando')
    wrapper.unmount()
  })

  it('places server results on the matching top faces', () => {
    const dice = diceWithResults(diceForAnimation('2d6+d20'), [{
      terms: [{ expression: '2d6', values: [2, 5] }, { expression: '1d20', values: [17] }]
    }])
    const wrapper = mount(DiceRollAnimation, { props: { dice, settled: true, totals: [24] } })
    expect(wrapper.findAll('.die-face-top').map(face => face.text())).toEqual(['2', '5', '17'])
    expect(wrapper.text()).toContain('Resultado definido')
    expect(wrapper.find('.dice-total').text()).toContain('Total24')
    wrapper.unmount()
  })

  it('reveals the prepared roll only after falling and settling', async () => {
    vi.useFakeTimers()
    campaignStore.session = { campaignId: 'campaign-1' }
    campaignStore.snapshot = { diceRolls: [] }
    const result = {
      id: 'roll-1', results: [{ terms: [{ expression: '1d20', values: [17] }] }]
    }
    rollDice.mockResolvedValue(result)
    revealDiceRoll.mockResolvedValue(result)
    const wrapper = mount(DiceRoller, {
      global: { stubs: { DiceRollAnimation: { template: '<div data-testid="dice-animation" />' } } }
    })

    try {
      await wrapper.get('form').trigger('submit')
      expect(wrapper.find('[data-testid="dice-animation"]').exists()).toBe(true)
      expect(rollDice).toHaveBeenCalledWith(campaignStore.session, 'd20', false)
      expect(revealDiceRoll).not.toHaveBeenCalled()

      await vi.advanceTimersByTimeAsync(999)
      expect(revealDiceRoll).not.toHaveBeenCalled()

      await vi.advanceTimersByTimeAsync(1)
      expect(revealDiceRoll).toHaveBeenCalledWith(campaignStore.session, 'roll-1')
      expect(campaignStore.snapshot.diceRolls).toEqual([])

      await vi.advanceTimersByTimeAsync(1000)
      expect(campaignStore.snapshot.diceRolls).toEqual([result])
    } finally {
      wrapper.unmount()
      campaignStore.session = null
      campaignStore.snapshot = null
      rollDice.mockReset()
      revealDiceRoll.mockReset()
      vi.useRealTimers()
    }
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

describe('useQuickRollModifier', () => {
  afterEach(() => { const modifier = useQuickRollModifier(); modifier.value.value = 0 })

  it('increments and decrements, allowing positive, negative and zero values', () => {
    const modifier = useQuickRollModifier()
    modifier.increment(); modifier.increment(); modifier.increment()
    expect(modifier.value.value).toBe(3)
    modifier.decrement(); modifier.decrement(); modifier.decrement(); modifier.decrement()
    expect(modifier.value.value).toBe(-1)
  })

  it('formats the roll expression for each quick die using the current modifier', () => {
    const modifier = useQuickRollModifier()
    expect(modifier.expressionFor(20)).toBe('1d20')
    modifier.increment(); modifier.increment(); modifier.increment()
    expect(modifier.expressionFor(20)).toBe('1d20+3')
    expect(modifier.expressionFor(8)).toBe('1d8+3')
    modifier.decrement(); modifier.decrement(); modifier.decrement(); modifier.decrement(); modifier.decrement()
    expect(modifier.expressionFor(6)).toBe('1d6-2')
  })
})

describe('QuickDiceRoller', () => {
  afterEach(() => {
    const modifier = useQuickRollModifier(); modifier.value.value = 0
    campaignStore.session = null; campaignStore.snapshot = null
    rollDice.mockReset(); revealDiceRoll.mockReset()
  })

  it('applies the current modifier automatically and keeps it across consecutive rolls', async () => {
    vi.useFakeTimers()
    campaignStore.session = { campaignId: 'campaign-1' }
    campaignStore.snapshot = { diceRolls: [] }
    const d8Result = { id: 'roll-1', total: 11, results: [{ terms: [{ expression: '1d8', values: [8] }] }] }
    const d20Result = { id: 'roll-2', total: 23, results: [{ terms: [{ expression: '1d20', values: [20] }] }] }
    rollDice.mockResolvedValueOnce(d8Result).mockResolvedValueOnce(d20Result)
    revealDiceRoll.mockResolvedValueOnce(d8Result).mockResolvedValueOnce(d20Result)
    const modifier = useQuickRollModifier(); modifier.increment(); modifier.increment(); modifier.increment()

    const wrapper = mount(QuickDiceRoller, {
      global: { stubs: { DiceRollAnimation: true } }
    })

    try {
      await wrapper.findAll('.die-button')[1]!.trigger('click')
      await vi.runAllTimersAsync()
      expect(rollDice).toHaveBeenNthCalledWith(1, campaignStore.session, '1d8+3', false)

      await wrapper.findAll('.die-button')[0]!.trigger('click')
      await vi.runAllTimersAsync()
      expect(rollDice).toHaveBeenNthCalledWith(2, campaignStore.session, '1d20+3', false)
      expect(wrapper.emitted('rolled-d20')?.[0]).toEqual([d20Result])
    } finally {
      wrapper.unmount()
      vi.useRealTimers()
    }
  })
})
