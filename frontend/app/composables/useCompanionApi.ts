import type {
  AdminCampaign, AttackResult, BattlemapSnapshot, CampaignSnapshot, DiceRoll, Encounter,
  MoveBattlemapTokenRequest, Participant, ReplaceBattlemapResult, Session, SetAttackAreaRequest
} from '~/types/campaign'
import { createUuid } from '~/utils/createUuid'

interface ProblemDetails { title?: string; detail?: string }

export function useCompanionApi() {
  const config = useRuntimeConfig()

  async function problem(response: Response) {
    const details = await response.json().catch(() => ({})) as ProblemDetails
    const error = new Error(details.detail || details.title || 'Não foi possível concluir a operação.')
    ;(error as Error & { status?: number }).status = response.status
    return error
  }

  async function request<T>(path: string, options: RequestInit = {}, token?: string): Promise<T> {
    const headers = new Headers(options.headers)
    if (!(options.body instanceof FormData) && !headers.has('Content-Type')) {
      headers.set('Content-Type', 'application/json')
    }
    if (token) headers.set('X-Participant-Token', token)
    const response = await fetch(`${config.public.apiBase}${path}`, { ...options, headers })
    if (!response.ok) throw await problem(response)
    if (response.status === 204) return undefined as T
    return response.json() as Promise<T>
  }

  async function battlemapImage(session: Session, imageUrl: string) {
    const headers = new Headers({ 'X-Participant-Token': session.participantToken })
    const response = await fetch(imageUrl, { headers })
    if (!response.ok) throw await problem(response)
    return response.blob()
  }

  async function participantAvatar(session: Session, participantId: string, revision: string) {
    const headers = new Headers({ 'X-Participant-Token': session.participantToken })
    const response = await fetch(`${config.public.apiBase}/campaigns/${session.campaignId}/participants/${participantId}/avatar?revision=${encodeURIComponent(revision)}`, { headers })
    if (!response.ok) throw await problem(response)
    return response.blob()
  }

  function uploadAvatar(session: Session, image: Blob, fileName = 'avatar.webp') {
    const form = new FormData()
    form.append('image', image, fileName)
    return request<Participant>(`/campaigns/${session.campaignId}/participants/me/avatar`, { method: 'PUT', body: form }, session.participantToken)
  }

  return {
    createCampaign: (campaignName: string, displayName: string) => request<Session>('/campaigns', { method: 'POST', body: JSON.stringify({ campaignName, displayName }) }),
    joinCampaign: (code: string, displayName: string) => request<Session>('/campaigns/join', { method: 'POST', body: JSON.stringify({ code, displayName }) }),
    snapshot: (session: Session) => request<CampaignSnapshot>(`/campaigns/${session.campaignId}/snapshot`, {}, session.participantToken),
    updateInitiativeModifier: (session: Session, initiativeModifier: number | null) => request<Participant>(`/campaigns/${session.campaignId}/participants/me/initiative-modifier`, { method: 'PUT', body: JSON.stringify({ initiativeModifier }) }, session.participantToken),
    updateCombatProfile: (session: Session, initiativeModifier: number | null, currentHitPoints: number | null) => request<Participant>(`/campaigns/${session.campaignId}/participants/me/combat-profile`, { method: 'PUT', body: JSON.stringify({ initiativeModifier, currentHitPoints }) }, session.participantToken),
    rollDice: (session: Session, expression: string, publish = true) => request<DiceRoll>(`/campaigns/${session.campaignId}/dice-rolls`, { method: 'POST', headers: { 'Idempotency-Key': createUuid() }, body: JSON.stringify({ expression, publish }) }, session.participantToken),
    revealDiceRoll: (session: Session, rollId: string) => request<DiceRoll>(`/campaigns/${session.campaignId}/dice-rolls/${rollId}/reveal`, { method: 'POST' }, session.participantToken),
    createEncounter: (session: Session) => request<Encounter>(`/campaigns/${session.campaignId}/encounters`, { method: 'POST' }, session.participantToken),
    addCombatant: (session: Session, encounterId: string, name: string, initiativeModifier: number, currentHitPoints: number | null) => request<Encounter>(`/campaigns/${session.campaignId}/encounters/${encounterId}/combatants`, { method: 'POST', body: JSON.stringify({ name, initiativeModifier, currentHitPoints }) }, session.participantToken),
    rollInitiative: (session: Session, encounterId: string, combatantId: string | null = null) => request<Encounter>(`/campaigns/${session.campaignId}/encounters/${encounterId}/initiative`, { method: 'POST', body: JSON.stringify({ combatantId }) }, session.participantToken),
    updateCombatant: (session: Session, encounterId: string, combatantId: string, name: string, initiativeModifier: number, currentHitPoints: number | null) => request<Encounter>(`/campaigns/${session.campaignId}/encounters/${encounterId}/combatants/${combatantId}`, { method: 'PUT', body: JSON.stringify({ name, initiativeModifier, currentHitPoints }) }, session.participantToken),
    updateHitPoints: (session: Session, encounterId: string, combatantId: string, currentHitPoints: number) => request<Encounter>(`/campaigns/${session.campaignId}/encounters/${encounterId}/combatants/${combatantId}/hit-points`, { method: 'PUT', body: JSON.stringify({ currentHitPoints }) }, session.participantToken),
    resolveAttack: (session: Session, encounterId: string, diceRollId: string, targetCombatantIds: string[], requestId = createUuid()) => request<AttackResult>(`/campaigns/${session.campaignId}/encounters/${encounterId}/attacks`, { method: 'POST', headers: { 'Idempotency-Key': requestId }, body: JSON.stringify({ diceRollId, targetCombatantIds }) }, session.participantToken),
    removeCombatant: (session: Session, encounterId: string, combatantId: string) => request<Encounter>(`/campaigns/${session.campaignId}/encounters/${encounterId}/combatants/${combatantId}`, { method: 'DELETE' }, session.participantToken),
    startEncounter: (session: Session, encounterId: string) => request<Encounter>(`/campaigns/${session.campaignId}/encounters/${encounterId}/start`, { method: 'POST' }, session.participantToken),
    nextTurn: (session: Session, encounterId: string) => request<Encounter>(`/campaigns/${session.campaignId}/encounters/${encounterId}/next-turn`, { method: 'POST' }, session.participantToken),
    endEncounter: (session: Session, encounterId: string) => request<Encounter>(`/campaigns/${session.campaignId}/encounters/${encounterId}/end`, { method: 'POST' }, session.participantToken),
    getBattlemap: (session: Session, encounterId: string) => request<BattlemapSnapshot>(`/campaigns/${session.campaignId}/encounters/${encounterId}/battlemap`, {}, session.participantToken),
    configureBattlemap: (session: Session, encounterId: string, image: File, columns: number, rows: number, isGridVisible: boolean, expectedRevision: string | null) => {
      const form = new FormData()
      form.append('image', image)
      form.append('columns', String(columns))
      form.append('rows', String(rows))
      form.append('isGridVisible', String(isGridVisible))
      if (expectedRevision) form.append('expectedRevision', expectedRevision)
      return request<ReplaceBattlemapResult>(`/campaigns/${session.campaignId}/encounters/${encounterId}/battlemap`, { method: 'PUT', body: form }, session.participantToken)
    },
    moveBattlemapToken: (session: Session, encounterId: string, participantId: string, body: MoveBattlemapTokenRequest) =>
      request<BattlemapSnapshot>(`/campaigns/${session.campaignId}/encounters/${encounterId}/battlemap/tokens/${participantId}/position`, { method: 'PUT', body: JSON.stringify(body) }, session.participantToken),
    setAttackArea: (session: Session, encounterId: string, tokenId: string, body: SetAttackAreaRequest) =>
      request<BattlemapSnapshot>(`/campaigns/${session.campaignId}/encounters/${encounterId}/battlemap/tokens/${tokenId}/attack-area`, { method: 'PUT', body: JSON.stringify(body) }, session.participantToken),
    removeAttackArea: (session: Session, encounterId: string, tokenId: string) =>
      request<BattlemapSnapshot>(`/campaigns/${session.campaignId}/encounters/${encounterId}/battlemap/tokens/${tokenId}/attack-area`, { method: 'DELETE' }, session.participantToken),
    battlemapImage,
    participantAvatar,
    uploadAvatar,
    removeAvatar: (session: Session) => request<Participant>(`/campaigns/${session.campaignId}/participants/me/avatar`, { method: 'DELETE' }, session.participantToken),
    listAdminCampaigns: (adminKey: string) => request<AdminCampaign[]>('/admin/campaigns', { headers: { 'X-Admin-Key': adminKey } }),
    deleteAdminCampaign: (adminKey: string, campaignId: string) => request<void>(`/admin/campaigns/${campaignId}`, { method: 'DELETE', headers: { 'X-Admin-Key': adminKey } })
  }
}
