import type {
  AdminCampaign, AttackResult, BattlemapSnapshot, CampaignSnapshot, DiceRoll, Encounter,
  CreateMarkerRequest, MoveBattlemapTokenRequest, Participant, ReplaceBattlemapResult, Session, SetAttackAreaRequest, SetTokenAppearanceRequest
} from '~/types/campaign'
import type { Creature, CreaturePage, CreatureQuery, RollPreset, SaveCreatureRequest } from '~/types/creatures'
import { createUuid } from '~/utils/createUuid'

interface ProblemDetails { title?: string; detail?: string }

export function useCompanionApi() {
  const config = useRuntimeConfig()

  async function problem(response: Response) {
    const details = await response.json().catch(() => ({})) as ProblemDetails
    // The rate limiters (join: 12/min per IP; admin: 8/min per IP; 1-minute fixed windows)
    // answer 429 with an empty body, so "wait a minute" is always true.
    const fallback = response.status === 429
      ? 'Muitas tentativas em pouco tempo. Aguarde um minuto e tente de novo.'
      : 'Não foi possível concluir a operação.'
    const error = new Error(details.detail || details.title || fallback)
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
    /** "Sair da campanha" (spec 011, FR-043): apaga só a sessão deste aparelho. Desiste em 5 s para não prender a saída local. */
    leaveSession: (session: Session) => request<void>(`/campaigns/${session.campaignId}/participants/me/session`, { method: 'DELETE', signal: AbortSignal.timeout(5000) }, session.participantToken),
    snapshot: (session: Session) => request<CampaignSnapshot>(`/campaigns/${session.campaignId}/snapshot`, {}, session.participantToken),
    updateInitiativeModifier: (session: Session, initiativeModifier: number | null) => request<Participant>(`/campaigns/${session.campaignId}/participants/me/initiative-modifier`, { method: 'PUT', body: JSON.stringify({ initiativeModifier }) }, session.participantToken),
    updateCombatProfile: (session: Session, initiativeModifier: number | null, currentHitPoints: number | null, maxHitPoints: number | null = null) => request<Participant>(`/campaigns/${session.campaignId}/participants/me/combat-profile`, { method: 'PUT', body: JSON.stringify({ initiativeModifier, currentHitPoints, maxHitPoints }) }, session.participantToken),
    rollDice: (session: Session, expression: string, publish = true, label: string | null = null) => request<DiceRoll>(`/campaigns/${session.campaignId}/dice-rolls`, { method: 'POST', headers: { 'Idempotency-Key': createUuid() }, body: JSON.stringify({ expression, publish, label }) }, session.participantToken),
    revealDiceRoll: (session: Session, rollId: string) => request<DiceRoll>(`/campaigns/${session.campaignId}/dice-rolls/${rollId}/reveal`, { method: 'POST' }, session.participantToken),
    createEncounter: (session: Session) => request<Encounter>(`/campaigns/${session.campaignId}/encounters`, { method: 'POST' }, session.participantToken),
    addCombatant: (session: Session, encounterId: string, name: string, initiativeModifier: number, currentHitPoints: number | null, maxHitPoints: number | null = null) => request<Encounter>(`/campaigns/${session.campaignId}/encounters/${encounterId}/combatants`, { method: 'POST', body: JSON.stringify({ name, initiativeModifier, currentHitPoints, maxHitPoints }) }, session.participantToken),
    rollInitiative: (session: Session, encounterId: string, combatantId: string | null = null) => request<Encounter>(`/campaigns/${session.campaignId}/encounters/${encounterId}/initiative`, { method: 'POST', body: JSON.stringify({ combatantId }) }, session.participantToken),
    updateCombatant: (session: Session, encounterId: string, combatantId: string, name: string, initiativeModifier: number, currentHitPoints: number | null, maxHitPoints: number | null = null) => request<Encounter>(`/campaigns/${session.campaignId}/encounters/${encounterId}/combatants/${combatantId}`, { method: 'PUT', body: JSON.stringify({ name, initiativeModifier, currentHitPoints, maxHitPoints }) }, session.participantToken),
    updateHitPoints: (session: Session, encounterId: string, combatantId: string, currentHitPoints: number) => request<Encounter>(`/campaigns/${session.campaignId}/encounters/${encounterId}/combatants/${combatantId}/hit-points`, { method: 'PUT', body: JSON.stringify({ currentHitPoints }) }, session.participantToken),
    resolveAttack: (session: Session, encounterId: string, diceRollId: string, targetCombatantIds: string[], requestId = createUuid()) => request<AttackResult>(`/campaigns/${session.campaignId}/encounters/${encounterId}/attacks`, { method: 'POST', headers: { 'Idempotency-Key': requestId }, body: JSON.stringify({ diceRollId, targetCombatantIds }) }, session.participantToken),
    removeCombatant: (session: Session, encounterId: string, combatantId: string) => request<Encounter>(`/campaigns/${session.campaignId}/encounters/${encounterId}/combatants/${combatantId}`, { method: 'DELETE' }, session.participantToken),
    startEncounter: (session: Session, encounterId: string) => request<Encounter>(`/campaigns/${session.campaignId}/encounters/${encounterId}/start`, { method: 'POST' }, session.participantToken),
    nextTurn: (session: Session, encounterId: string) => request<Encounter>(`/campaigns/${session.campaignId}/encounters/${encounterId}/next-turn`, { method: 'POST' }, session.participantToken),
    endEncounter: (session: Session, encounterId: string) => request<Encounter>(`/campaigns/${session.campaignId}/encounters/${encounterId}/end`, { method: 'POST' }, session.participantToken),
    getBattlemap: (session: Session, encounterId: string) => request<BattlemapSnapshot>(`/campaigns/${session.campaignId}/encounters/${encounterId}/battlemap`, {}, session.participantToken),
    /** fogMask: undefined = legacy (server keeps its rules), null = publish revealed, string = initial fog. */
    configureBattlemap: (session: Session, encounterId: string, image: File, columns: number, rows: number, isGridVisible: boolean, expectedRevision: string | null, fogMask?: string | null) => {
      const form = new FormData()
      form.append('image', image)
      form.append('columns', String(columns))
      form.append('rows', String(rows))
      form.append('isGridVisible', String(isGridVisible))
      if (expectedRevision) form.append('expectedRevision', expectedRevision)
      if (fogMask !== undefined) form.append('fogMask', fogMask ?? '')
      return request<ReplaceBattlemapResult>(`/campaigns/${session.campaignId}/encounters/${encounterId}/battlemap`, { method: 'PUT', body: form }, session.participantToken)
    },
    moveBattlemapToken: (session: Session, encounterId: string, participantId: string, body: MoveBattlemapTokenRequest) =>
      request<BattlemapSnapshot>(`/campaigns/${session.campaignId}/encounters/${encounterId}/battlemap/tokens/${participantId}/position`, { method: 'PUT', body: JSON.stringify(body) }, session.participantToken),
    setAttackArea: (session: Session, encounterId: string, tokenId: string, body: SetAttackAreaRequest) =>
      request<BattlemapSnapshot>(`/campaigns/${session.campaignId}/encounters/${encounterId}/battlemap/tokens/${tokenId}/attack-area`, { method: 'PUT', body: JSON.stringify(body) }, session.participantToken),
    removeAttackArea: (session: Session, encounterId: string, tokenId: string) =>
      request<BattlemapSnapshot>(`/campaigns/${session.campaignId}/encounters/${encounterId}/battlemap/tokens/${tokenId}/attack-area`, { method: 'DELETE' }, session.participantToken),
    setTokenAppearance: (session: Session, encounterId: string, tokenId: string, body: SetTokenAppearanceRequest) =>
      request<BattlemapSnapshot>(`/campaigns/${session.campaignId}/encounters/${encounterId}/battlemap/tokens/${tokenId}/appearance`, { method: 'PUT', body: JSON.stringify(body) }, session.participantToken),
    createMarker: (session: Session, encounterId: string, body: CreateMarkerRequest) =>
      request<BattlemapSnapshot>(`/campaigns/${session.campaignId}/encounters/${encounterId}/battlemap/markers`, { method: 'POST', body: JSON.stringify(body) }, session.participantToken),
    removeMarker: (session: Session, encounterId: string, markerId: string) =>
      request<BattlemapSnapshot>(`/campaigns/${session.campaignId}/encounters/${encounterId}/battlemap/markers/${markerId}`, { method: 'DELETE' }, session.participantToken),
    setFog: (session: Session, encounterId: string, mask: string | null) =>
      request<BattlemapSnapshot>(`/campaigns/${session.campaignId}/encounters/${encounterId}/battlemap/fog`, { method: 'PUT', body: JSON.stringify({ mask }) }, session.participantToken),
    setHitPointsVisibility: (session: Session, encounterId: string, combatantId: string, hidden: boolean) =>
      request<Encounter>(`/campaigns/${session.campaignId}/encounters/${encounterId}/combatants/${combatantId}/hit-points-visibility`, { method: 'PUT', body: JSON.stringify({ hidden }) }, session.participantToken),
    setAllHitPointsVisibility: (session: Session, encounterId: string, hidden: boolean) =>
      request<Encounter>(`/campaigns/${session.campaignId}/encounters/${encounterId}/hit-points-visibility`, { method: 'PUT', body: JSON.stringify({ hidden }) }, session.participantToken),
    battlemapImage,
    // Creature library (spec 008)
    listCreatures: (session: Session, query: CreatureQuery) => {
      const params = new URLSearchParams()
      for (const [key, value] of Object.entries(query)) if (value !== undefined && value !== null && value !== '') params.set(key, String(value))
      return request<CreaturePage>(`/campaigns/${session.campaignId}/creatures?${params}`, {}, session.participantToken)
    },
    creatureTypes: (session: Session) => request<string[]>(`/campaigns/${session.campaignId}/creatures/types`, {}, session.participantToken),
    getCreature: (session: Session, id: string) => request<Creature>(`/campaigns/${session.campaignId}/creatures/${id}`, {}, session.participantToken),
    createCreature: (session: Session, body: SaveCreatureRequest) => request<Creature>(`/campaigns/${session.campaignId}/creatures`, { method: 'POST', body: JSON.stringify(body) }, session.participantToken),
    saveCreature: (session: Session, id: string, body: SaveCreatureRequest) => request<Creature>(`/campaigns/${session.campaignId}/creatures/${id}`, { method: 'PUT', body: JSON.stringify(body) }, session.participantToken),
    saveCreatureNotes: (session: Session, id: string, notes: string | null, expectedRevision: string | null) => request<Creature>(`/campaigns/${session.campaignId}/creatures/${id}/notes`, { method: 'PUT', body: JSON.stringify({ notes, expectedRevision }) }, session.participantToken),
    /** Custom: deleted (undefined). Edited: the restored compendium original. */
    deleteCreature: (session: Session, id: string) => request<Creature | undefined>(`/campaigns/${session.campaignId}/creatures/${id}`, { method: 'DELETE' }, session.participantToken),
    uploadCreatureImage: (session: Session, id: string, file: File) => request<Creature>(`/campaigns/${session.campaignId}/creatures/${id}/image`, { method: 'PUT', headers: { 'Content-Type': file.type }, body: file }, session.participantToken),
    removeCreatureImage: (session: Session, id: string) => request<Creature>(`/campaigns/${session.campaignId}/creatures/${id}/image`, { method: 'DELETE' }, session.participantToken),
    addCreaturesToEncounter: (session: Session, encounterId: string, creatureId: string, count: number) => request<Encounter>(`/campaigns/${session.campaignId}/encounters/${encounterId}/creatures`, { method: 'POST', body: JSON.stringify({ creatureId, count }) }, session.participantToken),
    setRollPreset: (session: Session, encounterId: string, tokenId: string, preset: RollPreset) => request<BattlemapSnapshot>(`/campaigns/${session.campaignId}/encounters/${encounterId}/battlemap/tokens/${tokenId}/roll-preset`, { method: 'PUT', body: JSON.stringify(preset) }, session.participantToken),
    participantAvatar,
    uploadAvatar,
    removeAvatar: (session: Session) => request<Participant>(`/campaigns/${session.campaignId}/participants/me/avatar`, { method: 'DELETE' }, session.participantToken),
    listAdminCampaigns: (adminKey: string) => request<AdminCampaign[]>('/admin/campaigns', { headers: { 'X-Admin-Key': adminKey } }),
    deleteAdminCampaign: (adminKey: string, campaignId: string) => request<void>(`/admin/campaigns/${campaignId}`, { method: 'DELETE', headers: { 'X-Admin-Key': adminKey } })
  }
}
