import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import CampaignAccess from '~/components/CampaignAccess.vue'
import UserMenu from '~/components/UserMenu.vue'
import { useCompanionApi } from '~/composables/useCompanionApi'
import { useCampaignStore } from '~/stores/campaign'
import { useImageCacheStore } from '~/stores/imageCache'
import type { Session } from '~/types/campaign'

// Spec 011, User Story 2b: conta pelo nome e "Sair da campanha" que encerra só este aparelho.

const session: Session = { campaignId: 'c1', playerCode: '1234', masterCode: null, participantId: 'p1', displayName: 'Loren', role: 'Player', participantToken: 'token-do-celular' }
const leaveSession = vi.fn()

function setupStore() {
  setActivePinia(createPinia())
  const store = useCampaignStore()
  store.setSession(session)
  return store
}

beforeEach(() => {
  leaveSession.mockReset()
  vi.stubGlobal('useRuntimeConfig', () => ({ public: { apiBase: '/api' } }))
  vi.stubGlobal('useCompanionApi', () => ({ leaveSession, createCampaign: vi.fn(), joinCampaign: vi.fn() }))
  vi.stubGlobal('useCampaignStore', useCampaignStore)
  vi.stubGlobal('useImageCacheStore', () => useImageCacheStore())
})

afterEach(() => vi.unstubAllGlobals())

describe('CampaignAccess: dica do nome', () => {
  it('mostra a dica junto do campo de nome, ao criar e ao entrar com código', async () => {
    setActivePinia(createPinia())
    const wrapper = mount(CampaignAccess)
    const hint = wrapper.get('#display-name-hint')
    expect(hint.text()).toBe('Use o mesmo nome para voltar ao seu personagem.')
    expect(wrapper.get('input[autocomplete="nickname"]').attributes('aria-describedby')).toBe('display-name-hint')

    await wrapper.get('[role="tablist"] button:nth-child(2)').trigger('click')
    expect(wrapper.get('#display-name-hint').text()).toBe('Use o mesmo nome para voltar ao seu personagem.')
  })
})

describe('Sair da campanha', () => {
  it('apaga a sessão no servidor antes de limpar o navegador', async () => {
    const store = setupStore()
    const clearSession = vi.spyOn(store, 'clearSession')
    let sessionWhenCalled: Session | null = null
    leaveSession.mockImplementation(async () => {
      sessionWhenCalled = store.session
      expect(clearSession).not.toHaveBeenCalled()
    })

    await store.leaveCampaign()

    expect(leaveSession).toHaveBeenCalledTimes(1)
    expect(leaveSession).toHaveBeenCalledWith(session)
    expect(sessionWhenCalled).toEqual(session)
    // clearSession é quem remove a sessão do localStorage; ele roda depois do DELETE.
    expect(clearSession).toHaveBeenCalledTimes(1)
    expect(store.session).toBeNull()
  })

  it('falha de rede não impede a saída local', async () => {
    const store = setupStore()
    leaveSession.mockRejectedValue(new TypeError('Failed to fetch'))

    await expect(store.leaveCampaign()).resolves.toBeUndefined()

    expect(store.session).toBeNull()
  })

  it('sessão já recusada pelo servidor (401) também sai localmente', async () => {
    const store = setupStore()
    leaveSession.mockRejectedValue(Object.assign(new Error('A sessão não pertence a esta campanha.'), { status: 401 }))

    await store.leaveCampaign()

    expect(store.session).toBeNull()
  })

  it('sem sessão, não chama o servidor', async () => {
    setActivePinia(createPinia())
    const store = useCampaignStore()

    await store.leaveCampaign()

    expect(leaveSession).not.toHaveBeenCalled()
  })

  it('o item do menu do usuário pede a saída', async () => {
    const store = setupStore()
    store.connection = 'online'
    const wrapper = mount(UserMenu, { attachTo: document.body })
    await wrapper.get('.user-menu-trigger').trigger('click')
    const item = wrapper.findAll('[role="menuitem"]').find(button => button.text().includes('Sair da campanha'))!

    await item.trigger('click')

    expect(wrapper.emitted('leave')).toHaveLength(1)
    wrapper.unmount()
  })
})

describe('useCompanionApi().leaveSession', () => {
  it('envia DELETE para a sessão do participante com o token deste aparelho', async () => {
    const fetchMock = vi.fn(() => Promise.resolve(new Response(null, { status: 204 })))
    vi.stubGlobal('fetch', fetchMock)

    await expect(useCompanionApi().leaveSession(session)).resolves.toBeUndefined()

    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit]
    expect(url).toBe('/api/campaigns/c1/participants/me/session')
    expect(init.method).toBe('DELETE')
    expect(new Headers(init.headers).get('X-Participant-Token')).toBe('token-do-celular')
    expect(init.signal).toBeInstanceOf(AbortSignal)
  })
})
