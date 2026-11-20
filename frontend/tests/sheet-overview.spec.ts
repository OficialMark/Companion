import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useCharacterStore } from '~/stores/character'
import SheetSummaryBar from '~/components/SheetSummaryBar.vue'
import SheetOverviewModal from '~/components/SheetOverviewModal.vue'
import { makeCharacter } from './character-fixture'

// Spec 012, US3: the foot of the Mesa shows the player's sheet summary; the overview shows the numbers
// that matter and changes hit points through the sheet's write queue.
const characterApi = { getCharacter: vi.fn(), saveSheet: vi.fn() }
const go = vi.fn()
const store = reactive({
  isMaster: false,
  connection: 'online',
  session: { campaignId: 'c', participantId: 'p1', role: 'Player', participantToken: 't', displayName: 'Lia' } as Record<string, string> | null
})
vi.stubGlobal('useCampaignStore', () => store)
vi.stubGlobal('useCharacterApi', () => characterApi)
vi.stubGlobal('useCharacterStore', () => useCharacterStore())
vi.stubGlobal('useAppView', () => ({ view: ref('mesa'), go }))

function sheetCharacter() {
  const character = makeCharacter()
  Object.assign(character.sheet, { characterName: 'Lia da Torre', className: 'Guerreira', level: 3, currentHitPoints: 20, temporaryHitPoints: 4 })
  character.stats.maxHitPoints = { value: 30, parts: [] }
  character.stats.armorClass = { value: 16, parts: [] }
  character.stats.initiative = { value: 2, parts: [] }
  return character
}

beforeEach(() => {
  setActivePinia(createPinia())
  store.isMaster = false; store.connection = 'online'
  characterApi.getCharacter.mockReset().mockResolvedValue(sheetCharacter())
  // The server echoes what was saved, like the real route.
  characterApi.saveSheet.mockReset().mockImplementation(async (_session, data) => ({ ...sheetCharacter(), sheet: data, version: 'v2' }))
  go.mockReset()
})

describe('SheetSummaryBar', () => {
  it('loads the sheet and shows name, HP, AC and initiative; opening it shows the overview', async () => {
    const wrapper = mount(SheetSummaryBar, { attachTo: document.body })
    await flushPromises()

    expect(characterApi.getCharacter).toHaveBeenCalledTimes(1)
    expect(wrapper.get('[data-summary-hp]').text()).toBe('20/30 +4')
    expect(wrapper.get('[data-summary-ac]').text()).toBe('CA 16')
    expect(wrapper.get('[data-summary-initiative]').text()).toBe('+2')
    expect(wrapper.find('input').exists()).toBe(false) // no editable initiative or HP at the foot any more

    await wrapper.get('[data-open-overview]').trigger('click')
    expect(wrapper.find('[data-sheet-overview]').exists()).toBe(true)
    wrapper.unmount()
  })

  it('renders nothing for the master', () => {
    store.isMaster = true
    expect(mount(SheetSummaryBar).find('[data-sheet-summary]').exists()).toBe(false)
    expect(characterApi.getCharacter).not.toHaveBeenCalled()
  })
})

describe('SheetOverviewModal', () => {
  async function opened() {
    await useCharacterStore().load()
    const wrapper = mount(SheetOverviewModal, { attachTo: document.body })
    await flushPromises()
    return wrapper
  }

  it('shows the key numbers of the sheet', async () => {
    const wrapper = await opened()
    expect(wrapper.get('h2').text()).toBe('Lia da Torre')
    expect(wrapper.text()).toContain('Guerreira · nível 3')
    expect(wrapper.get('[data-overview-hp]').text()).toBe('20')
    expect(wrapper.get('[data-overview-temp-hp]').text()).toBe('+4 temp.')
    expect(wrapper.get('[data-overview-ac]').text()).toBe('16')
    expect(wrapper.findAll('[data-overview-abilities] tbody tr')).toHaveLength(6)
    wrapper.unmount()
  })

  it('damage spends temporary HP first, saves the sheet and announces the result', async () => {
    const wrapper = await opened()
    await wrapper.get('[data-hp-amount]').setValue(7)
    await wrapper.get('[data-hp-damage]').trigger('submit')
    await flushPromises()

    expect(wrapper.get('[data-overview-hp]').text()).toBe('17')
    expect(wrapper.find('[data-overview-temp-hp]').exists()).toBe(false)
    expect(characterApi.saveSheet).toHaveBeenCalledTimes(1)
    expect(characterApi.saveSheet.mock.calls[0]![1]).toMatchObject({ currentHitPoints: 17, temporaryHitPoints: 0 })
    expect(wrapper.text()).toContain('7 de dano. Vida 17 de 30.')
    expect((wrapper.get('[data-hp-amount]').element as HTMLInputElement).value).toBe('')
    expect(document.activeElement).toBe(wrapper.get('[data-hp-amount]').element) // focus stays in the dialog
    wrapper.unmount()
  })

  it('healing stops at the maximum, and +1 / −1 change one point', async () => {
    const wrapper = await opened()
    await wrapper.get('[data-hp-amount]').setValue(50)
    await wrapper.get('[data-hp-heal]').trigger('click')
    await flushPromises()
    expect(wrapper.get('[data-overview-hp]').text()).toBe('30')
    expect((wrapper.get('[data-hp-plus]').element as HTMLButtonElement).disabled).toBe(true)

    await wrapper.get('[data-hp-minus]').trigger('click') // takes the temporary point first
    await wrapper.get('[data-hp-minus]').trigger('click')
    await flushPromises()
    expect(wrapper.get('[data-overview-temp-hp]').text()).toBe('+2 temp.')
    wrapper.unmount()
  })

  it('offline: damage and healing are disabled with a notice', async () => {
    const wrapper = await opened()
    store.connection = 'offline'
    await wrapper.get('[data-hp-amount]').setValue(5)
    expect((wrapper.get('[data-hp-damage]').element as HTMLButtonElement).disabled).toBe(true)
    expect((wrapper.get('[data-hp-heal]').element as HTMLButtonElement).disabled).toBe(true)
    expect(wrapper.text()).toContain('Conexão necessária para mudar a vida.')
    wrapper.unmount()
  })

  it('Esc closes; "Abrir ficha completa" goes to the Ficha', async () => {
    const wrapper = await opened()
    await wrapper.get('[data-sheet-overview]').trigger('keydown', { key: 'Escape' })
    expect(wrapper.emitted('close')).toHaveLength(1)
    await wrapper.get('[data-open-sheet]').trigger('click')
    expect(go).toHaveBeenCalledWith('ficha')
    wrapper.unmount()
  })
})
