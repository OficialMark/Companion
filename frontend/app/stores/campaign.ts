import { defineStore } from 'pinia'
import type {
  AttackAreaShape, BattlemapInvalidationEvent, BattlemapSnapshot, CampaignEvent, CampaignSnapshot,
  DamageAppliedEvent, DiceRoll, Encounter, Participant, Session
} from '~/types/campaign'

const SESSION_KEY = 'companion.session.v1'

export const useCampaignStore = defineStore('campaign', {
  state: () => ({
    session: null as Session | null,
    snapshot: null as CampaignSnapshot | null,
    battlemap: null as BattlemapSnapshot | null,
    battlemapImageSource: '' as string,
    battlemapLoading: false,
    pendingBattlemapTokenId: null as string | null,
    connection: 'offline' as 'offline' | 'connecting' | 'online',
    error: '' as string,
    seenEvents: [] as string[],
    damageEffects: {} as Record<string, { amount: number; nonce: string }>,
    /** Loaded profile photos, keyed by participant id (object URLs of the fetched blobs). */
    avatars: {} as Record<string, { revision: string; url: string }>
  }),
  getters: {
    isMaster: state => state.session?.role === 'Master',
    ready: state => Boolean(state.session && state.snapshot),
    /** participantId -> object URL of the photo, for every participant that has one loaded. */
    avatarUrls: state => Object.fromEntries(Object.entries(state.avatars).map(([id, avatar]) => [id, avatar.url])) as Record<string, string>
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
      this.clearBattlemap()
      this.clearAvatars()
      if (import.meta.client) localStorage.setItem(SESSION_KEY, JSON.stringify(session))
    },
    clearBattlemap() {
      if (import.meta.client && this.battlemapImageSource.startsWith('blob:')) {
        URL.revokeObjectURL(this.battlemapImageSource)
      }
      this.battlemap = null
      this.battlemapImageSource = ''
      this.pendingBattlemapTokenId = null
    },
    clearAvatars() {
      if (typeof window !== 'undefined') for (const avatar of Object.values(this.avatars)) URL.revokeObjectURL(avatar.url)
      this.avatars = {}
    },
    /** Fetches photos whose revision changed and drops the ones that were removed. */
    async syncAvatars() {
      if (typeof window === 'undefined' || !this.session || !this.snapshot) return
      const wanted = new Map<string, string>()
      for (const participant of [...this.snapshot.participants, this.snapshot.currentParticipant]) {
        if (participant.avatarRevision) wanted.set(participant.id, participant.avatarRevision)
      }
      for (const [id, avatar] of Object.entries(this.avatars)) {
        if (wanted.get(id) !== avatar.revision) {
          URL.revokeObjectURL(avatar.url)
          delete this.avatars[id]
        }
      }
      const session = this.session
      await Promise.all([...wanted].filter(([id]) => !this.avatars[id]).map(async ([id, revision]) => {
        try {
          const blob = await useCompanionApi().participantAvatar(session, id, revision)
          // Another sync may have raced us; keep whichever matches the current revision.
          if (this.session !== session || this.avatars[id]?.revision === revision) return
          this.avatars[id] = { revision, url: URL.createObjectURL(blob) }
        } catch {
          // A missing photo just falls back to the initials.
        }
      }))
    },
    /** Applies the participant returned by the API after changing one's own profile. */
    applyOwnParticipant(updated: Participant) {
      if (!this.snapshot) return
      this.snapshot.currentParticipant = { ...updated, currentHitPoints: updated.currentHitPoints ?? this.snapshot.currentParticipant.currentHitPoints }
      const index = this.snapshot.participants.findIndex(participant => participant.id === updated.id)
      if (index >= 0) this.snapshot.participants[index] = { ...this.snapshot.participants[index]!, avatarRevision: updated.avatarRevision }
      void this.syncAvatars()
    },
    clearSession() {
      this.session = null
      this.snapshot = null
      this.seenEvents = []
      this.clearBattlemap()
      this.clearAvatars()
      if (import.meta.client) localStorage.removeItem(SESSION_KEY)
    },
    async refresh() {
      if (!this.session) return
      try {
        const next = await useCompanionApi().snapshot(this.session)
        if (this.snapshot) {
          this.updateActiveEncounter(next.activeEncounter)
          next.activeEncounter = this.snapshot.activeEncounter
        }
        this.snapshot = next
        void this.syncAvatars()
        await this.refreshBattlemap()
        this.error = ''
      } catch (error) {
        if ((error as Error & { status?: number }).status === 401) this.clearSession()
        this.error = (error as Error).message
        throw error
      }
    },
    updateActiveEncounter(next: Encounter | null) {
      if (!this.snapshot || !next || this.snapshot.activeEncounter?.id !== next.id) {
        if (this.snapshot) this.snapshot.activeEncounter = next
        return
      }
      const current = this.snapshot.activeEncounter
      const currentById = new Map(current.combatants.map(combatant => [combatant.id, combatant]))
      const combatants = next.combatants.map(combatant => {
        const existing = currentById.get(combatant.id)
        if (!existing) return combatant
        Object.assign(existing, combatant)
        return existing
      })
      current.status = next.status
      current.round = next.round
      current.currentTurnIndex = next.currentTurnIndex
      current.combatants.splice(0, current.combatants.length, ...combatants)
    },    async refreshBattlemap() {
      const encounter = this.snapshot?.activeEncounter
      if (!this.session || !encounter) {
        this.clearBattlemap()
        return
      }
      this.battlemapLoading = true
      try {
        const next = await useCompanionApi().getBattlemap(this.session, encounter.id)
        const imageChanged = this.battlemap?.imageRevision !== next.imageRevision || !this.battlemapImageSource
        this.battlemap = next
        if (imageChanged && import.meta.client) {
          const blob = await useCompanionApi().battlemapImage(this.session, next.imageUrl)
          if (this.battlemapImageSource.startsWith('blob:')) URL.revokeObjectURL(this.battlemapImageSource)
          this.battlemapImageSource = URL.createObjectURL(blob)
        }
      } catch (error) {
        if ((error as Error & { status?: number }).status === 404) {
          this.clearBattlemap()
          return
        }
        throw error
      } finally {
        this.battlemapLoading = false
      }
    },
    async moveBattlemapToken(participantId: string, column: number | null, row: number | null) {
      if (!this.session || !this.battlemap) return
      if (this.connection !== 'online') {
        this.error = 'Conexão necessária para mover pinos.'
        return
      }
      const token = this.battlemap.tokens.find(item => item.participantId === participantId)
      if (!token) return
      this.pendingBattlemapTokenId = participantId
      try {
        this.battlemap = await useCompanionApi().moveBattlemapToken(
          this.session,
          this.battlemap.encounterId,
          participantId,
          { column, row, expectedPositionVersion: token.positionVersion }
        )
        this.error = ''
      } catch (error) {
        this.error = (error as Error).message
        if ((error as Error & { status?: number }).status === 409) await this.refreshBattlemap()
      } finally {
        this.pendingBattlemapTokenId = null
      }
    },
    async setAttackArea(tokenId: string, shape: AttackAreaShape, radius: number, width: number | null, directionDegrees: number | null) {
      if (!this.session || !this.battlemap) return
      try {
        this.battlemap = await useCompanionApi().setAttackArea(
          this.session,
          this.battlemap.encounterId,
          tokenId,
          { shape, radius, width, directionDegrees }
        )
        this.error = ''
      } catch (error) {
        this.error = (error as Error).message
      }
    },
    async removeAttackArea(tokenId: string) {
      if (!this.session || !this.battlemap) return
      try {
        this.battlemap = await useCompanionApi().removeAttackArea(this.session, this.battlemap.encounterId, tokenId)
        this.error = ''
      } catch (error) {
        this.error = (error as Error).message
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
      if (event.type === 'BattlemapInvalidated') {
        const payload = event.payload as BattlemapInvalidationEvent
        if (payload.encounterId === this.snapshot.activeEncounter?.id) await this.refreshBattlemap()
        return
      }
      if (event.type === 'DamageApplied') {
        const payload = event.payload as DamageAppliedEvent
        for (const target of payload.targets) {
          this.damageEffects[target.combatantId] = { amount: target.damageApplied, nonce: event.eventId }
        }
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
        this.updateActiveEncounter(event.payload as Encounter)
        await this.refreshBattlemap()
      } else if (event.type === 'ParticipantUpdated') {
        const participant = event.payload as Participant
        const index = this.snapshot.participants.findIndex(x => x.id === participant.id)
        if (index >= 0) this.snapshot.participants[index] = participant
        if (this.snapshot.currentParticipant.id === participant.id) {
          this.snapshot.currentParticipant = {
            ...participant,
            currentHitPoints: participant.currentHitPoints ?? this.snapshot.currentParticipant.currentHitPoints
          }
        }
        void this.syncAvatars()
      } else if (event.type === 'EncounterEnded') {
        this.snapshot.activeEncounter = null
        this.clearBattlemap()
      }
    },
    clearDamageEffect(combatantId: string, nonce?: string) {
      if (this.damageEffects[combatantId]?.nonce === nonce) delete this.damageEffects[combatantId]
    },
    applyParticipants(event: CampaignEvent<Participant[]>) {
      if (!this.snapshot || this.seenEvents.includes(event.eventId)) return
      this.seenEvents = [event.eventId, ...this.seenEvents].slice(0, 200)
      this.snapshot.participants = event.payload
      void this.syncAvatars()
    }
  }
})
