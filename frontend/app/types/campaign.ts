export type ParticipantRole = 'Master' | 'Player'
export type EncounterStatus = 'Preparing' | 'Active' | 'Ended'

export interface Session {
  campaignId: string
  campaignCode: string
  participantId: string
  displayName: string
  role: ParticipantRole
  participantToken: string
}

export interface Campaign { id: string; code: string; name: string }
export interface Participant { id: string; displayName: string; role: ParticipantRole; isOnline: boolean }
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
  name: string
  initiativeModifier: number
  initiative: number | null
  creationOrder: number
  isCurrent: boolean
}
export interface Encounter {
  id: string
  status: EncounterStatus
  round: number
  currentTurnIndex: number | null
  combatants: Combatant[]
}
export interface CampaignSnapshot {
  campaign: Campaign
  currentParticipant: Participant
  participants: Participant[]
  diceRolls: DiceRoll[]
  activeEncounter: Encounter | null
}
export interface CampaignEvent<T = unknown> {
  eventId: string
  campaignId: string
  type: string
  occurredAt: string
  payload: T
}
