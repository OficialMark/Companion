import { defineStore } from 'pinia'
import type { CampaignEvent, CampaignSnapshot, DamageAppliedEvent, DiceRoll, Encounter, Participant, Session } from '~/types/campaign'

const SESSION_KEY = 'companion.session.v1'

export const useCampaignStore = defineStore('campaign', {
  state: () => ({
    session: null as Session | null,
    snapshot: null as CampaignSnapshot | null,
    connection: 'offline' as 'offline' | 'connecting' | 'online',
    error: '' as string,
    seenEvents: [] as string[],
    damageEffects: {} as Record<string, { amount: number; nonce: string }>
  }),
  getters: {
    isMaster: state => state.session?.role === 'Master',
    ready: state => Boolean(state.session && state.snapshot)
  },
  actions: {
    loadSession() {
      if (!import.meta.client) return
      const raw = localStorage.getItem(SESSION_KEY)
      if (raw) {
        try { this.session = JSON.parse(raw) as Session } catch { localStorage.removeItem(SESSION_KEY) }
      }
    },
    setSession(session: Session) {
      this.session = session
      this.snapshot = null
      if (import.meta.client) localStorage.setItem(SESSION_KEY, JSON.stringify(session))
    },
    clearSession() {
      this.session = null
      this.snapshot = null
      this.seenEvents = []
      if (import.meta.client) localStorage.removeItem(SESSION_KEY)
    },
    async refresh() {
      if (!this.session) return
      try {
        this.snapshot = await useCompanionApi().snapshot(this.session)
        this.error = ''
      } catch (error) {
        if ((error as Error & { status?: number }).status === 401) this.clearSession()
        this.error = (error as Error).message
        throw error
      }
    },
    async applyEvent(event: CampaignEvent) {
      if (event.type === 'CampaignDeleted') {
        this.clearSession()
        this.error = 'Esta mesa foi encerrada pelo administrador.'
        return
      }
      if (!this.snapshot || this.seenEvents.includes(event.eventId)) return
      this.seenEvents = [event.eventId, ...this.seenEvents].slice(0, 200)
      if (event.type === 'DamageApplied') {
        const payload = event.payload as DamageAppliedEvent
        for (const target of payload.targets) this.damageEffects[target.combatantId] = { amount: target.damageApplied, nonce: event.eventId }
        return
      }
      if (event.type === 'EncounterInvalidated') {
        await this.refresh()
        return
      }
      if (event.type === 'DiceRolled') {
        const roll = event.payload as DiceRoll
        if (!this.snapshot.diceRolls.some(x => x.id === roll.id)) {
          this.snapshot.diceRolls = [roll, ...this.snapshot.diceRolls].slice(0, 50)
        }
      } else if (['EncounterStarted', 'CombatantAdded', 'CombatantUpdated', 'CombatantRemoved', 'InitiativeRolled', 'TurnAdvanced'].includes(event.type)) {
        this.snapshot.activeEncounter = event.payload as Encounter
      } else if (event.type === 'ParticipantUpdated') {
        const participant = event.payload as Participant
        const index = this.snapshot.participants.findIndex(x => x.id === participant.id)
        if (index >= 0) this.snapshot.participants[index] = participant
        if (this.snapshot.currentParticipant.id === participant.id) {
          this.snapshot.currentParticipant = { ...participant, currentHitPoints: participant.currentHitPoints ?? this.snapshot.currentParticipant.currentHitPoints }
        }
      } else if (event.type === 'EncounterEnded') {
        this.snapshot.activeEncounter = null
      }
    },
    clearDamageEffect(combatantId: string, nonce?: string) {
      if (this.damageEffects[combatantId]?.nonce === nonce) delete this.damageEffects[combatantId]
    },
    applyParticipants(event: CampaignEvent<Participant[]>) {
      if (!this.snapshot || this.seenEvents.includes(event.eventId)) return
      this.seenEvents = [event.eventId, ...this.seenEvents].slice(0, 200)

      this.snapshot.participants = event.payload
    }
  }
})
