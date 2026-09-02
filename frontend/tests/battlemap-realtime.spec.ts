import { createPinia, setActivePinia } from 'pinia'
import { useCampaignStore } from '~/stores/campaign'
import type { BattlemapSnapshot, CampaignEvent } from '~/types/campaign'

const moveBattlemapToken = vi.fn()
vi.stubGlobal('useCompanionApi', () => ({ moveBattlemapToken }))

const battlemap: BattlemapSnapshot = {
  id: 'map', encounterId: 'encounter', columns: 10, rows: 10, isGridVisible: true, revision: 'r1',
  imageRevision: 'image-1', imageUrl: '/image', imageMediaType: 'image/png',
  imageFileName: 'map.png', updatedAt: new Date().toISOString(),
  tokens: [{ participantId: 'player', displayName: 'Lia', initials: 'L', colorKey: 1, column: 1, row: 1, positionVersion: 'v1' }]
}

function readyStore() {
  setActivePinia(createPinia())
  const store = useCampaignStore()
  store.session = { campaignId: 'campaign', playerCode: '1234', masterCode: null, participantId: 'player', displayName: 'Lia', role: 'Player', participantToken: 'token' }
  store.snapshot = {
    campaign: { id: 'campaign', playerCode: '1234', name: 'Mesa' },
    currentParticipant: { id: 'player', displayName: 'Lia', role: 'Player', isOnline: true, initiativeModifier: 0, currentHitPoints: 10 },
    participants: [], diceRolls: [],
    activeEncounter: { id: 'encounter', status: 'Preparing', round: 0, currentTurnIndex: null, combatants: [] }
  }
  store.battlemap = structuredClone(battlemap)
  return store
}

describe('battlemap realtime resilience', () => {
  it('refreshes the authorized battlemap after invalidation', async () => {
    const store = readyStore()
    const refresh = vi.spyOn(store, 'refreshBattlemap').mockResolvedValue()
    const event: CampaignEvent = {
      eventId: 'event-1', campaignId: 'campaign', type: 'BattlemapInvalidated',
      occurredAt: new Date().toISOString(), payload: { encounterId: 'encounter', revision: 'r2' }
    }
    await store.applyEvent(event)
    expect(refresh).toHaveBeenCalledOnce()
  })

  it('does not submit a move while offline', async () => {
    const store = readyStore()
    store.connection = 'offline'
    await store.moveBattlemapToken('player', 2, 2)
    expect(moveBattlemapToken).not.toHaveBeenCalled()
    expect(store.battlemap?.tokens[0]?.column).toBe(1)
    expect(store.error).toContain('Conexão necessária')
  })
})
