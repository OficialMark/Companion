import { createPinia, setActivePinia } from 'pinia'
import { useCampaignStore } from '~/stores/campaign'
import type { BattlemapSnapshot, Encounter } from '~/types/campaign'

const setFog = vi.fn()
const setHitPointsVisibility = vi.fn()
const setAllHitPointsVisibility = vi.fn()
const getBattlemap = vi.fn()
vi.stubGlobal('useCompanionApi', () => ({ setFog, setHitPointsVisibility, setAllHitPointsVisibility, getBattlemap }))

const battlemap: BattlemapSnapshot = {
  id: 'map', encounterId: 'encounter', columns: 4, rows: 4, isGridVisible: true, revision: 'r1',
  imageRevision: 'image-1', imageUrl: '/image', imageMediaType: 'image/png',
  imageFileName: 'map.png', updatedAt: new Date().toISOString(), tokens: [], fogMask: null
}
const encounter: Encounter = { id: 'encounter', status: 'Active', round: 1, currentTurnIndex: 0, combatants: [] }

function readyStore() {
  setActivePinia(createPinia())
  const store = useCampaignStore()
  store.session = { campaignId: 'campaign', playerCode: '1234', masterCode: '9999', participantId: 'master', displayName: 'Mestre', role: 'Master', participantToken: 'token' }
  store.snapshot = {
    campaign: { id: 'campaign', playerCode: '1234', name: 'Mesa' },
    currentParticipant: { id: 'master', displayName: 'Mestre', role: 'Master', isOnline: true, initiativeModifier: null, currentHitPoints: null, maxHitPoints: null },
    participants: [], diceRolls: [], activeEncounter: structuredClone(encounter)
  }
  store.battlemap = structuredClone(battlemap)
  store.connection = 'online'
  return store
}

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (error: unknown) => void
  const promise = new Promise<T>((res, rej) => { resolve = res; reject = rej })
  return { promise, resolve, reject }
}

beforeEach(() => { setFog.mockReset(); getBattlemap.mockReset(); setHitPointsVisibility.mockReset(); setAllHitPointsVisibility.mockReset() })

describe('fog saves', () => {
  it('serializes saves and sends only the latest queued mask after the in-flight one', async () => {
    const store = readyStore()
    const first = deferred<BattlemapSnapshot>()
    setFog.mockImplementationOnce(() => first.promise).mockImplementation((_s, _e, mask) => Promise.resolve({ ...battlemap, fogMask: mask }))

    const saving = store.setFog('A')
    expect(store.fogSaving).toBe(true)
    await store.setFog('B')
    await store.setFog('C')
    expect(setFog).toHaveBeenCalledTimes(1)

    first.resolve({ ...battlemap, fogMask: 'A' })
    await saving
    expect(setFog.mock.calls.map(call => call[2])).toEqual(['A', 'C'])
    expect(store.battlemap?.fogMask).toBe('C')
    expect(store.fogSaving).toBe(false)
  })

  it('falls back to the server state when a save fails', async () => {
    const store = readyStore()
    setFog.mockRejectedValue(new Error('boom'))
    getBattlemap.mockResolvedValue({ ...battlemap, fogMask: 'SERVER' })
    expect(await store.setFog('A')).toBe(false)
    expect(store.error).toContain('A névoa não foi salva')
    expect(getBattlemap).toHaveBeenCalled()
    expect(store.fogSaving).toBe(false)
  })

  it('refuses to save while offline', async () => {
    const store = readyStore()
    store.connection = 'offline'
    getBattlemap.mockResolvedValue(battlemap)
    expect(await store.setFog('A')).toBe(false)
    expect(setFog).not.toHaveBeenCalled()
  })
})

describe('HP visibility', () => {
  it('uses the per-creature route or the bulk route and applies the returned encounter', async () => {
    const store = readyStore()
    setHitPointsVisibility.mockResolvedValue({ ...encounter, round: 2 })
    await store.setHitPointsVisibility('goblin', true)
    expect(setHitPointsVisibility).toHaveBeenCalledWith(store.session, 'encounter', 'goblin', true)
    expect(store.snapshot?.activeEncounter?.round).toBe(2)

    setAllHitPointsVisibility.mockResolvedValue({ ...encounter, round: 3 })
    await store.setHitPointsVisibility(null, false)
    expect(setAllHitPointsVisibility).toHaveBeenCalledWith(store.session, 'encounter', false)
    expect(store.snapshot?.activeEncounter?.round).toBe(3)
  })
})
