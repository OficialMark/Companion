import { createPinia, setActivePinia } from 'pinia'
import { useCampaignStore } from '~/stores/campaign'
import { AUTOSAVE_DELAY_MS, useCharacterStore } from '~/stores/character'
import { makeCharacter } from './character-fixture'

// T079: a late answer of the previous session never lands in the store of the next one (same device,
// "Sair" and another player enters before A's request comes back).
const api = { getCharacter: vi.fn(), saveSheet: vi.fn(), equipItem: vi.fn() }
vi.stubGlobal('useCharacterApi', () => api)
vi.stubGlobal('useCampaignStore', () => useCampaignStore())
vi.stubGlobal('useCharacterStore', () => useCharacterStore())

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (error: unknown) => void
  const promise = new Promise<T>((res, rej) => { resolve = res; reject = rej })
  return { promise, resolve, reject }
}

function signIn(token: string, participantId: string) {
  useCampaignStore().session = { campaignId: 'c1', playerCode: '1234', masterCode: null, participantId, displayName: participantId, role: 'Player', participantToken: token } as never
}

/** What index.vue does when the session token changes. */
function switchAccount() {
  signIn('token-b', 'pB')
  useCharacterStore().reset()
}

beforeEach(() => {
  setActivePinia(createPinia())
  signIn('token-a', 'pA')
  api.getCharacter.mockReset()
  api.saveSheet.mockReset()
  api.equipItem.mockReset()
})

describe('store da ficha: troca de sessão no meio de um pedido', () => {
  it('gravação pendente de A resolvida depois da troca não vira a ficha de B', async () => {
    const store = useCharacterStore()
    store.apply(makeCharacter({ participantId: 'pA' }))
    store.editSheet({ characterName: 'Ana' })
    const save = deferred<ReturnType<typeof makeCharacter>>()
    api.saveSheet.mockReturnValue(save.promise)
    const flushing = store.flush()

    switchAccount()
    save.resolve(makeCharacter({ participantId: 'pA' }))
    await flushing

    expect(store.character).toBeNull()
    expect(store.draft).toBeNull()
    expect(store.saveState).toBe('idle')
    expect(store.inFlight).toBe(false)
  })

  it('erro atrasado de A não aparece para B', async () => {
    const store = useCharacterStore()
    store.apply(makeCharacter({ participantId: 'pA' }))
    store.editSheet({ characterName: 'Ana' })
    const save = deferred<never>()
    // Depending on timing the request may never be sent (the queue sees the switch first): no unhandled rejection.
    save.promise.catch(() => {})
    api.saveSheet.mockReturnValue(save.promise)
    const flushing = store.flush()

    switchAccount()
    save.reject(Object.assign(new Error('A ficha mudou.'), { status: 409 }))
    await flushing

    expect(store.error).toBe('')
    expect(store.saveState).toBe('idle')
  })

  it('carga atrasada de A é descartada e a de B, iniciada depois, vale', async () => {
    const store = useCharacterStore()
    const loadA = deferred<ReturnType<typeof makeCharacter>>()
    api.getCharacter.mockReturnValueOnce(loadA.promise)
    const loadingA = store.load()

    switchAccount()
    api.getCharacter.mockResolvedValueOnce(makeCharacter({ participantId: 'pB' }))
    await store.load()
    loadA.resolve(makeCharacter({ participantId: 'pA' }))
    await loadingA

    expect(store.character!.participantId).toBe('pB')
    expect(store.loading).toBe(false)
  })

  it('ação de item de A que volta depois da troca não é aplicada', async () => {
    const store = useCharacterStore()
    store.apply(makeCharacter({ participantId: 'pA' }))
    const equip = deferred<ReturnType<typeof makeCharacter>>()
    api.equipItem.mockReturnValue(equip.promise)
    const running = store.run((characterApi, session) => characterApi.equipItem(session, 'i1', true))

    switchAccount()
    equip.resolve(makeCharacter({ participantId: 'pA' }))

    expect(await running).toBe(false)
    expect(store.character).toBeNull()
  })

  it('troca de token sem reset também derruba a resposta', async () => {
    const store = useCharacterStore()
    const load = deferred<ReturnType<typeof makeCharacter>>()
    api.getCharacter.mockReturnValue(load.promise)
    const loading = store.load()
    signIn('token-b', 'pB')
    load.resolve(makeCharacter({ participantId: 'pA' }))
    await loading
    expect(store.character).toBeNull()
  })

  it('sem troca, a resposta é aplicada normalmente', async () => {
    const store = useCharacterStore()
    api.getCharacter.mockResolvedValue(makeCharacter({ participantId: 'pA' }))
    await store.load()
    expect(store.character!.participantId).toBe('pA')
  })
})

describe('store da ficha: escritas do mesmo aparelho em fila', () => {
  it('equipar pendente + edição da ficha: o saveSheet espera o equip e usa a versão nova, sem "conflict"', async () => {
    vi.useFakeTimers()
    try {
      const store = useCharacterStore()
      store.apply(makeCharacter({ participantId: 'pA', version: 'v1' }), { resetDraft: true })
      const equip = deferred<ReturnType<typeof makeCharacter>>()
      api.equipItem.mockReturnValue(equip.promise)
      // The server would answer 409 to a save carrying the old version, as it does for a real conflict.
      api.saveSheet.mockImplementation(async (_session, sheet, version: string) => {
        if (version !== 'v2') throw Object.assign(new Error('A ficha mudou.'), { status: 409 })
        return makeCharacter({ participantId: 'pA', version: 'v3', sheet: JSON.parse(JSON.stringify(sheet)) })
      })

      const equipping = store.run((characterApi, session) => characterApi.equipItem(session, 'i1', true))
      store.editSheet({ currentHitPoints: 4 })
      await vi.advanceTimersByTimeAsync(AUTOSAVE_DELAY_MS)
      expect(api.saveSheet).not.toHaveBeenCalled()

      equip.resolve(makeCharacter({ participantId: 'pA', version: 'v2' }))
      expect(await equipping).toBe(true)
      await vi.runAllTimersAsync()

      expect(api.saveSheet).toHaveBeenCalledTimes(1)
      expect(api.saveSheet.mock.calls[0]![2]).toBe('v2')
      expect(api.saveSheet.mock.calls[0]![1].currentHitPoints).toBe(4)
      expect(store.saveState).toBe('saved')
      expect(store.character!.version).toBe('v3')
      expect(store.error).toBe('')
    } finally {
      vi.useRealTimers()
    }
  })

  it('a ação de item espera o autosave em andamento e parte da versão que ele devolveu', async () => {
    const store = useCharacterStore()
    store.apply(makeCharacter({ participantId: 'pA', version: 'v1' }), { resetDraft: true })
    const save = deferred<ReturnType<typeof makeCharacter>>()
    api.saveSheet.mockReturnValue(save.promise)
    const order: string[] = []
    api.equipItem.mockImplementation(async () => { order.push(`equip depois de ${store.character!.version}`); return makeCharacter({ participantId: 'pA', version: 'v3' }) })

    store.editSheet({ notes: 'x' })
    const flushing = store.flush()
    const equipping = store.run((characterApi, session) => characterApi.equipItem(session, 'i1', true))
    await Promise.resolve()
    expect(api.equipItem).not.toHaveBeenCalled()

    save.resolve(makeCharacter({ participantId: 'pA', version: 'v2' }))
    await flushing
    expect(await equipping).toBe(true)
    expect(order).toEqual(['equip depois de v2'])
    expect(store.character!.version).toBe('v3')
  })

  it('um 409 real (outro aparelho) continua virando "conflict"', async () => {
    const store = useCharacterStore()
    store.apply(makeCharacter({ participantId: 'pA', version: 'v1' }), { resetDraft: true })
    api.saveSheet.mockRejectedValue(Object.assign(new Error('A ficha mudou.'), { status: 409 }))
    store.editSheet({ notes: 'x' })
    await store.flush()
    expect(store.saveState).toBe('conflict')
  })

  it('recarrega uma alteração remota recebida enquanto o autosave local aguardava a resposta', async () => {
    const store = useCharacterStore()
    const initial = makeCharacter({ participantId: 'pA', version: 'v1' })
    store.apply(initial, { resetDraft: true })
    const save = deferred<ReturnType<typeof makeCharacter>>()
    api.saveSheet.mockReturnValue(save.promise)
    api.getCharacter.mockResolvedValue(makeCharacter({ participantId: 'pA', version: 'v3', sheet: { ...initial.sheet, notes: 'mudança remota' } }))

    store.editSheet({ characterName: 'Ana' })
    const flushing = store.flush()
    await Promise.resolve()
    await store.onRemoteChange('pA')
    expect(store.remoteChangePending).toBe(true)

    save.resolve(makeCharacter({ participantId: 'pA', version: 'v2', sheet: { ...initial.sheet, characterName: 'Ana' } }))
    await flushing

    expect(api.getCharacter).toHaveBeenCalledTimes(1)
    expect(store.character!.version).toBe('v3')
    expect(store.character!.sheet.notes).toBe('mudança remota')
    expect(store.remoteChangePending).toBe(false)
    expect(store.saveState, store.error).toBe('saved')
  })

  it('uma escrita que falha não trava a fila', async () => {
    const store = useCharacterStore()
    store.apply(makeCharacter({ participantId: 'pA' }), { resetDraft: true })
    api.equipItem.mockRejectedValueOnce(new Error('Falha de rede')).mockResolvedValueOnce(makeCharacter({ participantId: 'pA', version: 'v9' }))
    expect(await store.run((characterApi, session) => characterApi.equipItem(session, 'i1', true))).toBe(false)
    expect(await store.run((characterApi, session) => characterApi.equipItem(session, 'i1', true))).toBe(true)
    expect(store.character!.version).toBe('v9')
  })
})

describe('store da ficha: loadExisting (FR-010)', () => {
  it('pede a ficha com ifExists e não cria nada: 404 vira "missing"', async () => {
    const store = useCharacterStore()
    api.getCharacter.mockRejectedValue(Object.assign(new Error('Não encontrado'), { status: 404 }))
    expect(await store.loadExisting()).toBe('missing')
    expect(api.getCharacter).toHaveBeenCalledWith(expect.objectContaining({ participantToken: 'token-a' }), { ifExists: true })
    expect(store.character).toBeNull()
    expect(store.error).toBe('')
  })

  it('com ficha, carrega no store', async () => {
    const store = useCharacterStore()
    api.getCharacter.mockResolvedValue(makeCharacter({ participantId: 'pA' }))
    expect(await store.loadExisting()).toBe('found')
    expect(store.character!.participantId).toBe('pA')
    expect(store.draft).not.toBeNull()
  })

  it('outra falha não é confundida com "sem ficha"', async () => {
    api.getCharacter.mockRejectedValue(Object.assign(new Error('Falha de rede'), { status: 500 }))
    expect(await useCharacterStore().loadExisting()).toBe('error')
  })
})
