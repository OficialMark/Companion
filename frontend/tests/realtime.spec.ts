import { createPinia, setActivePinia } from 'pinia'
import { useCampaignStore } from '~/stores/campaign'
import type { CampaignEvent, DiceRoll } from '~/types/campaign'
describe('campaign realtime state', () => {
  it('deduplicates repeated events', () => { setActivePinia(createPinia()); const store = useCampaignStore(); store.snapshot = { campaign: { id: 'c', playerCode: '1234', name: 'Mesa' }, currentParticipant: { id: 'p', displayName: 'Mestre', role: 'Master', isOnline: true, initiativeModifier: null, currentHitPoints: null }, participants: [], diceRolls: [], activeEncounter: null }; const roll: DiceRoll = { id: 'r', participantId: 'p', displayName: 'Mestre', expression: '1d20', values: [12], modifier: 0, total: 12, results: [{ sequence: 1, terms: [{ expression: '1d20', values: [12], subtotal: 12 }], modifier: 0, total: 12 }], createdAt: new Date().toISOString() }; const event: CampaignEvent = { eventId: 'e', campaignId: 'c', type: 'DiceRolled', occurredAt: new Date().toISOString(), payload: roll }; store.applyEvent(event); store.applyEvent(event); expect(store.snapshot.diceRolls).toHaveLength(1) })
  it('stores public damage feedback without exposing hit points', async () => { setActivePinia(createPinia()); const store = useCampaignStore(); store.snapshot = { campaign: { id: 'c', playerCode: '1234', name: 'Mesa' }, currentParticipant: { id: 'p', displayName: 'Lia', role: 'Player', isOnline: true, initiativeModifier: 2, currentHitPoints: 20 }, participants: [], diceRolls: [], activeEncounter: null }; const event: CampaignEvent = { eventId: 'damage-1', campaignId: 'c', type: 'DamageApplied', occurredAt: new Date().toISOString(), payload: { encounterId: 'e1', targets: [{ combatantId: 'target-1', damageApplied: 7 }] } }; await store.applyEvent(event); expect(store.damageEffects['target-1']).toEqual({ amount: 7, nonce: 'damage-1' }); expect(event.payload).not.toHaveProperty('hitPointsAfter'); store.clearDamageEffect('target-1', 'damage-1'); expect(store.damageEffects['target-1']).toBeUndefined() })
  it('reconciles encounter damage without replacing combatant row identities', () => {
    setActivePinia(createPinia())
    const store = useCampaignStore()
    const combatant = { id: 'target-1', participantId: null, name: 'Goblin', initiativeModifier: 2, initiative: 12, creationOrder: 0, isCurrent: true, currentHitPoints: 10, isDefeated: false }
    const encounter = { id: 'e1', status: 'Active' as const, round: 1, currentTurnIndex: 0, combatants: [combatant] }
    store.snapshot = { campaign: { id: 'c', playerCode: '1234', name: 'Mesa' }, currentParticipant: { id: 'p', displayName: 'Mestre', role: 'Master', isOnline: true, initiativeModifier: null, currentHitPoints: null }, participants: [], diceRolls: [], activeEncounter: encounter }
    const currentEncounter = store.snapshot.activeEncounter
    const currentCombatant = currentEncounter.combatants[0]!

    store.updateActiveEncounter({ ...encounter, combatants: [{ ...combatant, currentHitPoints: 3 }] })

    expect(store.snapshot.activeEncounter).toBe(currentEncounter)
    expect(store.snapshot.activeEncounter?.combatants[0]).toBe(currentCombatant)
    expect(currentCombatant.currentHitPoints).toBe(3)
  })
  it('refreshes an authorized snapshot once for a sensitive encounter invalidation', async () => { setActivePinia(createPinia()); const store = useCampaignStore(); store.session = { campaignId: 'c', playerCode: '1234', masterCode: null, participantId: 'p', displayName: 'Lia', role: 'Player', participantToken: 'token' }; store.snapshot = { campaign: { id: 'c', playerCode: '1234', name: 'Mesa' }, currentParticipant: { id: 'p', displayName: 'Lia', role: 'Player', isOnline: true, initiativeModifier: 2, currentHitPoints: 20 }, participants: [], diceRolls: [], activeEncounter: null }; const refresh = vi.spyOn(store, 'refresh').mockResolvedValue(); const event: CampaignEvent = { eventId: 'hp-1', campaignId: 'c', type: 'EncounterInvalidated', occurredAt: new Date().toISOString(), payload: { encounterId: 'e1' } }; await store.applyEvent(event); await store.applyEvent(event); expect(refresh).toHaveBeenCalledTimes(1) })
})