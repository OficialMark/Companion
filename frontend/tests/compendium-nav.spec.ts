import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { reactive } from 'vue'
import { useCampaignStore } from '~/stores/campaign'
import { useCreatureStore } from '~/stores/creatures'
import { useImageCacheStore } from '~/stores/imageCache'
import { useAppView } from '~/composables/useAppView'
import ViewNav from '~/components/ViewNav.vue'
import CreatureLibrary from '~/components/creatures/CreatureLibrary.vue'

// Spec 009, US3: the master's Mesa/Compêndio navigation in the top bar, kept in the URL.
const route = reactive({ query: {} as Record<string, string> })
const router = { replace: vi.fn(({ query }: { query: Record<string, string> }) => { route.query = query; return Promise.resolve() }) }
const api = { listCreatures: vi.fn(), creatureTypes: vi.fn() }
vi.stubGlobal('useRoute', () => route)
vi.stubGlobal('useRouter', () => router)
vi.stubGlobal('useAppView', useAppView)
vi.stubGlobal('useCompanionApi', () => api)
vi.stubGlobal('useCampaignStore', () => useCampaignStore())
vi.stubGlobal('useCreatureStore', () => useCreatureStore())
vi.stubGlobal('useImageCacheStore', () => useImageCacheStore())

function session(role: 'Master' | 'Player') {
  setActivePinia(createPinia())
  const campaign = useCampaignStore()
  campaign.session = { campaignId: 'c1', playerCode: '1234', masterCode: role === 'Master' ? '9999' : null, participantId: 'p', displayName: 'X', role, participantToken: 't' } as never
  return campaign
}

beforeEach(() => {
  route.query = {}
  router.replace.mockClear()
  api.listCreatures.mockReset().mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 24 })
  api.creatureTypes.mockReset().mockResolvedValue([])
  vi.spyOn(window, 'scrollTo').mockImplementation(() => {})
})

describe('ViewNav (top bar)', () => {
  it('shows Mesa and Compêndio to the master, Mesa marked as the current page', () => {
    session('Master')
    const wrapper = mount(ViewNav)
    expect(wrapper.get('[data-view="mesa"]').attributes('aria-current')).toBe('page')
    expect(wrapper.get('[data-view="compendio"]').attributes('aria-current')).toBeUndefined()
  })

  it('is not rendered for a player', () => {
    session('Player')
    expect(mount(ViewNav).find('[data-master-nav]').exists()).toBe(false)
  })

  it('Compêndio writes ?tela=compendio (replace, no history step), keeps other params and scrolls to the top', async () => {
    session('Master')
    route.query = { foo: 'bar' }
    const wrapper = mount(ViewNav)
    await wrapper.get('[data-view="compendio"]').trigger('click')
    expect(router.replace).toHaveBeenCalledWith({ query: { foo: 'bar', tela: 'compendio' } })
    expect(window.scrollTo).toHaveBeenCalledWith({ top: 0 })
    await flushPromises()
    expect(wrapper.get('[data-view="compendio"]').attributes('aria-current')).toBe('page')
    await wrapper.get('[data-view="mesa"]').trigger('click')
    expect(router.replace).toHaveBeenLastCalledWith({ query: { foo: 'bar' } })
  })

  it('clicking the screen already open does nothing', async () => {
    session('Master')
    const wrapper = mount(ViewNav)
    await wrapper.get('[data-view="mesa"]').trigger('click')
    expect(router.replace).not.toHaveBeenCalled()
  })
})

describe('useAppView', () => {
  it('a reload with ?tela=compendio reopens the Compendium for the master', () => {
    session('Master')
    route.query = { tela: 'compendio' }
    expect(useAppView().view.value).toBe('compendio')
  })

  it('a player asking for ?tela=compendio stays on the Mesa', () => {
    session('Player')
    route.query = { tela: 'compendio' }
    expect(useAppView().view.value).toBe('mesa')
  })

  it('an unknown screen falls back to the Mesa', () => {
    session('Master')
    route.query = { tela: 'qualquer' }
    expect(useAppView().view.value).toBe('mesa')
  })
})

describe('Compendium screen', () => {
  it('"Minhas criaturas" toggles the Custom origin filter and mirrors it', async () => {
    session('Master')
    const wrapper = mount(CreatureLibrary)
    await flushPromises()
    const mine = wrapper.get('[data-mine]')
    expect(mine.attributes('aria-pressed')).toBe('false')
    await mine.trigger('click')
    await flushPromises()
    expect(api.listCreatures).toHaveBeenLastCalledWith(expect.anything(), expect.objectContaining({ origin: 'Custom', page: 1 }))
    expect(mine.attributes('aria-pressed')).toBe('true')
    expect((wrapper.get('[aria-label="Origem"]').element as HTMLSelectElement).value).toBe('Custom')
    expect(wrapper.find('[data-mine-empty]').exists()).toBe(true)
    await mine.trigger('click')
    await flushPromises()
    expect(api.listCreatures).toHaveBeenLastCalledWith(expect.anything(), expect.objectContaining({ origin: '' }))
    expect(mine.attributes('aria-pressed')).toBe('false')
  })

  it('choosing "Criadas" in the origin select also lights up "Minhas criaturas"', async () => {
    session('Master')
    const wrapper = mount(CreatureLibrary)
    await flushPromises()
    await wrapper.get('[aria-label="Origem"]').setValue('Custom')
    await flushPromises()
    expect(wrapper.get('[data-mine]').attributes('aria-pressed')).toBe('true')
  })

  it('has the Compêndio title and the Nova criatura action', async () => {
    session('Master')
    const wrapper = mount(CreatureLibrary)
    await flushPromises()
    expect(wrapper.get('h1').text()).toBe('Compêndio')
    await wrapper.get('[data-new-creature]').trigger('click')
    expect(useCreatureStore().editing).toBe(true)
  })
})
