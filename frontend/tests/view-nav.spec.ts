import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { reactive } from 'vue'
import { useCampaignStore } from '~/stores/campaign'
import { useAppView } from '~/composables/useAppView'
import ViewNav from '~/components/ViewNav.vue'

// Spec 011, FR-001/FR-002 and US2 scenario 4: the player's Mesa/Ficha navigation, kept in the URL,
// and each role confined to its own screens.
const route = reactive({ query: {} as Record<string, string> })
const router = { replace: vi.fn(({ query }: { query: Record<string, string> }) => { route.query = query; return Promise.resolve() }) }
vi.stubGlobal('useRoute', () => route)
vi.stubGlobal('useRouter', () => router)
vi.stubGlobal('useAppView', useAppView)
vi.stubGlobal('useCampaignStore', () => useCampaignStore())

function session(role: 'Master' | 'Player') {
  setActivePinia(createPinia())
  const campaign = useCampaignStore()
  campaign.session = { campaignId: 'c1', playerCode: '1234', masterCode: role === 'Master' ? '9999' : null, participantId: 'p', displayName: 'X', role, participantToken: 't' } as never
  return campaign
}

beforeEach(() => {
  route.query = {}
  router.replace.mockClear()
  vi.spyOn(window, 'scrollTo').mockImplementation(() => {})
})

describe('ViewNav do jogador', () => {
  it('mostra Mesa e Ficha ao jogador, sem Compêndio', () => {
    session('Player')
    const wrapper = mount(ViewNav)
    expect(wrapper.find('[data-player-nav]').exists()).toBe(true)
    expect(wrapper.find('[data-master-nav]').exists()).toBe(false)
    expect(wrapper.find('[data-view="compendio"]').exists()).toBe(false)
    expect(wrapper.get('[data-view="mesa"]').attributes('aria-current')).toBe('page')
    expect(wrapper.get('[data-view="ficha"]').text()).toBe('Ficha')
  })

  it('o mestre não vê Ficha', () => {
    session('Master')
    const wrapper = mount(ViewNav)
    expect(wrapper.find('[data-view="ficha"]').exists()).toBe(false)
    expect(wrapper.find('[data-player-nav]').exists()).toBe(false)
  })

  it('Ficha escreve ?tela=ficha sem passo no histórico e volta à Mesa', async () => {
    session('Player')
    route.query = { foo: 'bar' }
    const wrapper = mount(ViewNav)
    await wrapper.get('[data-view="ficha"]').trigger('click')
    expect(router.replace).toHaveBeenCalledWith({ query: { foo: 'bar', tela: 'ficha' } })
    await flushPromises()
    expect(wrapper.get('[data-view="ficha"]').attributes('aria-current')).toBe('page')
    await wrapper.get('[data-view="mesa"]').trigger('click')
    expect(router.replace).toHaveBeenLastCalledWith({ query: { foo: 'bar' } })
  })

  it('não aparece sem sessão', () => {
    setActivePinia(createPinia())
    expect(mount(ViewNav).find('nav').exists()).toBe(false)
  })
})

describe('useAppView: cada papel só abre as próprias telas', () => {
  it('recarregar com ?tela=ficha reabre a Ficha do jogador', () => {
    session('Player')
    route.query = { tela: 'ficha' }
    expect(useAppView().view.value).toBe('ficha')
  })

  it('o mestre que abre ?tela=ficha cai na Mesa', () => {
    session('Master')
    route.query = { tela: 'ficha' }
    expect(useAppView().view.value).toBe('mesa')
  })

  it('o jogador que abre ?tela=compendio cai na Mesa', () => {
    session('Player')
    route.query = { tela: 'compendio' }
    expect(useAppView().view.value).toBe('mesa')
  })
})
