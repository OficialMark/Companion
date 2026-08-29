export type ParticipantRole = 'Master' | 'Player'
export type EncounterStatus = 'Preparing' | 'Active' | 'Ended'

export interface Session {
  campaignId: string
  playerCode: string
  masterCode: string | null
  participantId: string
  displayName: string
  role: ParticipantRole
  participantToken: string
}

export interface Campaign { id: string; playerCode: string; name: string }
export interface Participant {
  id: string
  displayName: string
  role: ParticipantRole
  isOnline: boolean
  initiativeModifier: number | null
  currentHitPoints: number | null
}
export interface DiceTermResult { expression: string; values: number[]; subtotal: number }
export interface DiceRollResult { sequence: number; terms: DiceTermResult[]; modifier: number; total: number }
export interface DiceRoll {
  id: string
  participantId: string
  displayName: string
  expression: string
  values: number[]
  modifier: number
  total: number
  results: DiceRollResult[]
  createdAt: string
}
export interface Combatant {
  id: string
  participantId: string | null
  name: string
  initiativeModifier: number
  initiative: number | null
  creationOrder: number
  isCurrent: boolean
  currentHitPoints: number | null
  isDefeated?: boolean
}
export interface Encounter {
  id: string
  status: EncounterStatus
  round: number
  currentTurnIndex: number | null
  combatants: Combatant[]
}
export interface AttackTarget {
  combatantId: string
  name: string
  damageApplied: number
  hitPointsBefore: number | null
  hitPointsAfter: number | null
}
export interface CombatAttack {
  id: string
  diceRollId: string
  authorParticipantId: string
  damage: number
  createdAt: string
  targets: AttackTarget[]
}
export interface AttackResult { attack: CombatAttack; encounter: Encounter }
export interface CampaignSnapshot {
  campaign: Campaign
  currentParticipant: Participant
  participants: Participant[]
  diceRolls: DiceRoll[]
  activeEncounter: Encounter | null
}
export interface DamageAppliedEvent { encounterId: string; targets: Array<{ combatantId: string; damageApplied: number }> }
export interface BattlemapToken {
  participantId: string
  displayName: string
  initials: string
  colorKey: number
  column: number | null
  row: number | null
  positionVersion: string | null
  isCreature?: boolean
}
export interface BattlemapSnapshot {
  id: string
  encounterId: string
  columns: number
  rows: number
  revision: string
  imageRevision: string
  imageUrl: string
  imageMediaType: 'image/png' | 'image/jpeg' | 'image/webp'
  imageFileName: string
  updatedAt: string
  tokens: BattlemapToken[]
}
export interface ReplaceBattlemapResult { battlemap: BattlemapSnapshot; returnedToReserve: string[] }
export interface MoveBattlemapTokenRequest { column: number; row: number; expectedPositionVersion: string | null }
export interface BattlemapInvalidationEvent { encounterId: string; revision: string }
export interface CampaignEvent<T = unknown> {
  eventId: string
  campaignId: string
  type: string
  occurredAt: string
  payload: T
}
export interface AdminCampaign {
  id: string
  name: string
  createdAt: string
  participantCount: number
  onlineCount: number
  playerCode: string
  masterCode: string
}