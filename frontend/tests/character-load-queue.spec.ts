import { createPinia, setActivePinia } from 'pinia'
import { useCampaignStore } from '~/stores/campaign'
import { useCharacterStore } from '~/stores/character'
import { makeCharacter } from './character-fixture'

// Spec 011, T079 follow-up: "Recarregar ficha" (load) waits for this device's pending writes, so a
// reload never shows a sheet that the pending write's answer then silently replaces.
const api = { getCharacter: vi.fn(), equipItem: vi.fn() }
vi.stubGlobal('useCharacterApi', () => api)
vi.stubGlobal('useCampaignStore', () => useCampaignStore())

beforeEach(() => {
  setActivePinia(createPinia())
  useCampaignStore().session = { campaignId: 'c1', playerCode: '1234', masterCode: null, participantId: 'p1', displayName: 'Loren', role: 'Player', participantToken: 't' } as never
  api.getCharacter.mockReset()
  api.equipItem.mockReset()
})

describe('load na fila de escritas', () => {
  it('o GET do recarregar só sai depois da escrita pendente e mostra o estado final', async () => {
    const store = useCharacterStore()
    store.character = makeCharacter()
    let finishEquip!: (value: unknown) => void
    const afterEquip = makeCharacter({ version: 'AAAAAAAAB9I=' })
    api.equipItem.mockReturnValue(new Promise(resolve => { finishEquip = resolve }))
    api.getCharacter.mockResolvedValue(afterEquip)

    const equip = store.run((characterApi, session) => characterApi.equipItem(session, 'i1', true) as never)
    const reload = store.load()
    await Promise.resolve()
    await Promise.resolve()
    expect(api.getCharacter).not.toHaveBeenCalled()

    finishEquip(makeCharacter({ version: 'AAAAAAAAB9H=' }))
    await equip
    await reload

    expect(api.getCharacter).toHaveBeenCalledTimes(1)
    expect(store.character!.version).toBe('AAAAAAAAB9I=')
  })
})
