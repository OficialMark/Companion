import { mount } from '@vue/test-utils'
import DiceRoller from '~/components/DiceRoller.vue'
import QuickDiceRoller from '~/components/QuickDiceRoller.vue'
import { createResultScrollSession } from '~/utils/scrollToResult'

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
let windowScrolls: ScrollToOptions[] = []
let windowY = 120
beforeEach(() => {
  scrolls = []
  windowScrolls = []
  windowY = 120
  Object.defineProperty(window, 'scrollY', { configurable: true, get: () => windowY })
  Object.defineProperty(window, 'scrollX', { configurable: true, get: () => 0 })
  window.scrollTo = vi.fn((options: ScrollToOptions | number, y?: number) => {
    const value = typeof options === 'number' ? y ?? 0 : options.top ?? windowY
    windowScrolls.push(typeof options === 'number' ? { top: value } : options)
    windowY = value
  }) as typeof window.scrollTo
  Element.prototype.scrollIntoView = function (options?: boolean | ScrollIntoViewOptions) {
    scrolls.push(options as ScrollIntoViewOptions)
    windowY = 500
  }
  Element.prototype.getBoundingClientRect = function () {
    const offscreen = (this as HTMLElement).matches?.('[data-offscreen], [data-roll-stage]')
    const top = offscreen ? 850 : 100
    return { x: 0, y: top, top, left: 0, right: 100, bottom: top + 100, width: 100, height: 100, toJSON: () => ({}) }
  }
  stubReducedMotion(false)
  rollDice.mockReset().mockResolvedValue(roll)
  revealDiceRoll.mockReset().mockResolvedValue({ ...roll, revealed: true })
})

describe('scrollToResult (spec 012, FR-007)', () => {
  it('does nothing when the result is already fully visible', () => {
    createResultScrollSession().show(document.createElement('div'))
    expect(scrolls).toEqual([])
  })

  it('moves only the nearest clipped edge', () => {
    const element = document.createElement('div')
    element.dataset.offscreen = ''
    createResultScrollSession().show(element)
    expect(scrolls).toEqual([{ block: 'nearest', inline: 'nearest', behavior: 'smooth' }])
  })

  it('scrolls far enough when the visible quick sheet covers the bottom of the result', () => {
    const dashboard = document.createElement('div')
    dashboard.className = 'dashboard'
    dashboard.innerHTML = '<div data-roll-stage></div><div data-sheet-summary></div>'
    const stage = dashboard.querySelector<HTMLElement>('[data-roll-stage]')!
    const sheet = dashboard.querySelector<HTMLElement>('[data-sheet-summary]')!
    const height = document.documentElement.clientHeight || window.innerHeight
    stage.getBoundingClientRect = () => new DOMRect(0, height - 160, 100, 110)
    sheet.getBoundingClientRect = () => new DOMRect(0, height - 100, 100, 88)
    const session = createResultScrollSession()

    session.show(stage)

    expect(stage.style.getPropertyValue('--roll-footer-inset')).toBe('100px')
    expect(scrolls).toEqual([{ block: 'nearest', inline: 'nearest', behavior: 'smooth' }])
    session.restore()
  })

  it('jumps without animation when the user asked for reduced motion', () => {
    stubReducedMotion(true)
    const element = document.createElement('div')
    element.dataset.offscreen = ''
    createResultScrollSession().show(element)
    expect(scrolls[0]?.behavior).toBe('auto')
  })

  it('ignores a missing element', () => {
    expect(() => createResultScrollSession().show(null)).not.toThrow()
    expect(scrolls).toEqual([])
  })

  it('returns to the previous position when the animation ends', () => {
    const element = document.createElement('div')
    element.dataset.offscreen = ''
    const session = createResultScrollSession()
    session.show(element)
    session.restore()
    expect(windowScrolls.at(-1)).toMatchObject({ top: 120, behavior: 'smooth' })
  })

  it('does not undo navigation performed by the user', () => {
    const element = document.createElement('div')
    element.dataset.offscreen = ''
    const session = createResultScrollSession()
    session.show(element)
    window.dispatchEvent(new WheelEvent('wheel'))
    session.restore()
    expect(windowScrolls).toEqual([])
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
    expect(scrolls[0]?.block).toBe('nearest')
    await vi.runAllTimersAsync()
    vi.useRealTimers()
    wrapper.unmount()
  })
})
