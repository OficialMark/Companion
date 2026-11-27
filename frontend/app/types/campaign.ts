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
  maxHitPoints: number | null
  /** Revision of the profile photo; null/undefined when the participant has none. */
  avatarRevision?: string | null
  isHiddenFromPanel?: boolean
}
export interface DiceTermResult { expression: string; values: number[]; subtotal: number }
/** Constante da expressão (spec 011): `(FOR) 3` vira `{ label: 'FOR', value: 3 }`. */
export interface DiceModifier { label: string | null; value: number }
/** Uma das duas execuções de `2#(…) maior|menor`. */
export interface DiceExecution { terms: DiceTermResult[]; modifier: number; total: number }
export interface DiceDamageComponentRequest { label: string; damageType: string; termCount: number }
export interface DiceDamageBreakdownRequest { baseType: string | null; baseTermCount: number; components: DiceDamageComponentRequest[] }
export interface DiceDamageBreakdown { baseType: string | null; baseSubtotal: number | null; components: Array<{ label: string; damageType: string; subtotal: number }> }
export interface DiceRollResult {
  sequence: number
  /** Termos, modificador e total da execução escolhida (o que clientes antigos leem). */
  terms: DiceTermResult[]
  modifier: number
  total: number
  /** Só em expressões com rótulo, colchetes ou seleção. */
  modifiers?: DiceModifier[]
  selection?: 'maior' | 'menor' | null
  /** Só com seleção: as duas execuções, na ordem em que rodaram. */
  executions?: DiceExecution[]
  selectedExecution?: number | null
  damageBreakdown?: DiceDamageBreakdown | null
}
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
  /** Who the roll was for, e.g. "Goblin 2" (spec 008). */
  label?: string | null
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
  maxHitPoints: number | null
  isDefeated?: boolean
  /** Master hid this creature's HP from players; players receive null HP when true. */
  isHitPointsHidden?: boolean
  creatureId?: string | null
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
export type AttackAreaShape = 'Circle' | 'Square' | 'Cone' | 'Cylinder'
export interface AttackArea {
  id: string
  shape: AttackAreaShape
  radius: number
  width: number | null
  directionDegrees: number | null
  updatedAt: string
}
export interface SetAttackAreaRequest {
  shape: AttackAreaShape
  radius: number
  width: number | null
  directionDegrees: number | null
}
export type StatusCondition =
  | 'Stunned' | 'Restrained' | 'Blinded' | 'Poisoned' | 'Invisible' | 'Prone'
  | 'Paralyzed' | 'Bleeding' | 'Grappled' | 'Deafened' | 'Frightened' | 'Charmed'
  | 'Unconscious' | 'Petrified' | 'Incapacitated' | 'Exhausted' | 'Concentrating' | 'Burning'
export interface BattlemapToken {
  participantId: string
  displayName: string
  initials: string
  colorKey: number
  column: number | null
  row: number | null
  positionVersion: string | null
  isCreature?: boolean
  isMarker?: boolean
  ownerParticipantId?: string | null
  attackArea?: AttackArea | null
  footprintSize?: number
  statusConditions?: StatusCondition[]
  /** Spec 008: library creature image (authenticated API path) and link. */
  imageUrl?: string | null
  creatureId?: string | null
  rollPreset?: { count: number; die: number; ability: 'str' | 'dex' | 'con' | 'int' | 'wis' | 'cha' | null; bonus: number } | null
  /** Spec 016: a marker taken from the creature compendium or the item catalog (null = free text). */
  sourceKind?: 'creature' | 'item' | null
  sourceId?: string | null
}
export interface SetTokenAppearanceRequest {
  footprintSize: number
  statusConditions: StatusCondition[]
}
/** Spec 016: free text (label), or a compendium creature / catalog item (label optional: defaults to its name). */
export interface CreateMarkerRequest {
  label?: string | null
  creatureId?: string | null
  itemId?: string | null
}
export interface BattlemapSnapshot {
  id: string
  encounterId: string
  columns: number
  rows: number
  isGridVisible: boolean
  revision: string
  imageRevision: string
  imageUrl: string
  imageMediaType: 'image/png' | 'image/jpeg' | 'image/webp'
  imageFileName: string
  updatedAt: string
  tokens: BattlemapToken[]
  /** Fog of war: base64 row-major bitset (1 = covered); null = fully revealed. */
  fogMask?: string | null
}
export interface ReplaceBattlemapResult { battlemap: BattlemapSnapshot; returnedToReserve: string[] }
export interface MoveBattlemapTokenRequest { column: number | null; row: number | null; expectedPositionVersion: string | null }
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
