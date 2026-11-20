import { mount } from '@vue/test-utils'
import DiceRoller from '~/components/DiceRoller.vue'
import QuickDiceRoller from '~/components/QuickDiceRoller.vue'
import { scrollToResult } from '~/utils/scrollToResult'

const campaignStore = { session: { campaignId: 'c', participantToken: 't' }, connection: 'online', snapshot: { diceRolls: [] as unknown[] } }
const roll = { id: 'r1', results: [{ total: 12, terms: [{ expression: '1d20', values: [12] }] }] }
const rollDice = vi.fn()
const revealDiceRoll = vi.fn()
vi.stubGlobal('useCampaignStore', () => campaignStore)
vi.stubGlobal('useCompanionApi', () => ({ rollDice, revealDiceRoll }))

function stubReducedMotion(reduce: boolean) {
  vi.stubGlobal('matchMedia', (query: string) => ({ matches: reduce && query.includes('reduce') }))
  window.matchMedia = globalThis.matchMedia as typeof window.matchMedia
}

let scrolls: ScrollIntoViewOptions[] = []
beforeEach(() => {
  scrolls = []
  Element.prototype.scrollIntoView = function (options?: boolean | ScrollIntoViewOptions) { scrolls.push(options as ScrollIntoViewOptions) }
  stubReducedMotion(false)
  rollDice.mockReset().mockResolvedValue(roll)
  revealDiceRoll.mockReset().mockResolvedValue({ ...roll, revealed: true })
})

describe('scrollToResult (spec 012, FR-007)', () => {
  it('centers the element smoothly', () => {
    scrollToResult(document.createElement('div'))
    expect(scrolls).toEqual([{ block: 'center', inline: 'nearest', behavior: 'smooth' }])
  })

  it('jumps without animation when the user asked for reduced motion', () => {
    stubReducedMotion(true)
    scrollToResult(document.createElement('div'))
    expect(scrolls[0]?.behavior).toBe('auto')
  })

  it('ignores a missing element', () => {
    expect(() => scrollToResult(null)).not.toThrow()
    expect(scrolls).toEqual([])
  })

  it.each([
    ['DiceRoller', DiceRoller, '.die-button'],
    ['QuickDiceRoller', QuickDiceRoller, '.die-button']
  ])('%s brings the dice into view when a roll starts', async (_, component, button) => {
    vi.useFakeTimers()
    const wrapper = mount(component, { global: { stubs: { DiceRollAnimation: { template: '<div />' } } } })
    await wrapper.get(button).trigger('click')
    await nextTick(); await nextTick()
    expect(wrapper.find('[data-roll-stage]').exists()).toBe(true)
    expect(scrolls.length).toBeGreaterThan(0)
    expect(scrolls[0]?.block).toBe('center')
    await vi.runAllTimersAsync()
    vi.useRealTimers()
    wrapper.unmount()
  })
})
