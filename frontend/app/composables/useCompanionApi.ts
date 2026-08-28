import type { AdminCampaign, CampaignSnapshot, DiceRoll, Encounter, Participant, Session } from '~/types/campaign'
import { createUuid } from '~/utils/createUuid'

interface ProblemDetails { title?: string; detail?: string }

export function useCompanionApi() {
  const config = useRuntimeConfig()

  async function request<T>(path: string, options: RequestInit = {}, token?: string): Promise<T> {
    const headers = new Headers(options.headers)
    headers.set('Content-Type', 'application/json')
    if (token) headers.set('X-Participant-Token', token)
    const response = await fetch(`${config.public.apiBase}${path}`, { ...options, headers })
    if (!response.ok) {
      const problem = await response.json().catch(() => ({})) as ProblemDetails
      const error = new Error(problem.detail || problem.title || 'Não foi possível concluir a operação.')
      ;(error as Error & { status?: number }).status = response.status
      throw error
    }
    if (response.status === 204) return undefined as T
    return response.json() as Promise<T>
  }

  return {
    createCampaign: (campaignName: string, displayName: string) => request<Session>('/campaigns', {
      method: 'POST', body: JSON.stringify({ campaignName, displayName })
    }),
    joinCampaign: (code: string, displayName: string) => request<Session>('/campaigns/join', {
      method: 'POST', body: JSON.stringify({ code, displayName })
    }),
    snapshot: (session: Session) => request<CampaignSnapshot>(`/campaigns/${session.campaignId}/snapshot`, {}, session.participantToken),
    updateInitiativeModifier: (session: Session, initiativeModifier: number | null) => request<Participant>(
      `/campaigns/${session.campaignId}/participants/me/initiative-modifier`,
      { method: 'PUT', body: JSON.stringify({ initiativeModifier }) }, session.participantToken),
    rollDice: (session: Session, expression: string, publish = true) => request<DiceRoll>(`/campaigns/${session.campaignId}/dice-rolls`, {
      method: 'POST', headers: { 'Idempotency-Key': createUuid() }, body: JSON.stringify({ expression, publish })
    }, session.participantToken),
    revealDiceRoll: (session: Session, rollId: string) => request<DiceRoll>(
      `/campaigns/${session.campaignId}/dice-rolls/${rollId}/reveal`, { method: 'POST' }, session.participantToken),
    createEncounter: (session: Session) => request<Encounter>(`/campaigns/${session.campaignId}/encounters`, { method: 'POST' }, session.participantToken),
    addCombatant: (session: Session, encounterId: string, name: string, initiativeModifier: number) => request<Encounter>(
      `/campaigns/${session.campaignId}/encounters/${encounterId}/combatants`,
      { method: 'POST', body: JSON.stringify({ name, initiativeModifier }) }, session.participantToken),
    rollInitiative: (session: Session, encounterId: string, combatantId: string | null = null) => request<Encounter>(
      `/campaigns/${session.campaignId}/encounters/${encounterId}/initiative`,
      { method: 'POST', body: JSON.stringify({ combatantId }) }, session.participantToken),
    updateCombatant: (session: Session, encounterId: string, combatantId: string, name: string, initiativeModifier: number) => request<Encounter>(
      `/campaigns/${session.campaignId}/encounters/${encounterId}/combatants/${combatantId}`,
      { method: 'PUT', body: JSON.stringify({ name, initiativeModifier }) }, session.participantToken),
    removeCombatant: (session: Session, encounterId: string, combatantId: string) => request<Encounter>(
      `/campaigns/${session.campaignId}/encounters/${encounterId}/combatants/${combatantId}`,
      { method: 'DELETE' }, session.participantToken),
    startEncounter: (session: Session, encounterId: string) => request<Encounter>(
      `/campaigns/${session.campaignId}/encounters/${encounterId}/start`, { method: 'POST' }, session.participantToken),
    nextTurn: (session: Session, encounterId: string) => request<Encounter>(
      `/campaigns/${session.campaignId}/encounters/${encounterId}/next-turn`, { method: 'POST' }, session.participantToken),
    endEncounter: (session: Session, encounterId: string) => request<Encounter>(
      `/campaigns/${session.campaignId}/encounters/${encounterId}/end`, { method: 'POST' }, session.participantToken),
    listAdminCampaigns: (adminKey: string) => request<AdminCampaign[]>('/admin/campaigns', {
      headers: { 'X-Admin-Key': adminKey }
    }),
    deleteAdminCampaign: (adminKey: string, campaignId: string) => request<void>(`/admin/campaigns/${campaignId}`, {
      method: 'DELETE', headers: { 'X-Admin-Key': adminKey }
    })
  }
}
