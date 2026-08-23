import { createPinia, setActivePinia } from 'pinia'
import { useCampaignStore } from '~/stores/campaign'
import type { CampaignEvent, DiceRoll } from '~/types/campaign'

describe('campaign realtime state', () => {
  it('deduplicates repeated events', () => {
    setActivePinia(createPinia())
    const store = useCampaignStore()
    store.snapshot = {
      campaign: { id: 'c', code: 'ABC234', name: 'Mesa' },
      currentParticipant: { id: 'p', displayName: 'Mestre', role: 'Master', isOnline: true },
      participants: [], diceRolls: [], activeEncounter: null
    }
    const roll: DiceRoll = { id: 'r', participantId: 'p', displayName: 'Mestre', expression: '1d20', values: [12], modifier: 0, total: 12, results: [{ sequence: 1, terms: [{ expression: '1d20', values: [12], subtotal: 12 }], modifier: 0, total: 12 }], createdAt: new Date().toISOString() }
    const event: CampaignEvent = { eventId: 'e', campaignId: 'c', type: 'DiceRolled', occurredAt: new Date().toISOString(), payload: roll }
    store.applyEvent(event)
    store.applyEvent(event)
    expect(store.snapshot.diceRolls).toHaveLength(1)
  })
})
