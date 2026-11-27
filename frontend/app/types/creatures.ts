export type CreatureSize = 'Tiny' | 'Small' | 'Medium' | 'Large' | 'Huge' | 'Gargantuan'
export type CreatureOrigin = 'Compendium' | 'Edited' | 'Custom'
export type AbilityKey = 'str' | 'dex' | 'con' | 'int' | 'wis' | 'cha'

export interface CreatureAction { name: string; description: string }

export interface CreatureSummary {
  id: string
  name: string
  nameEn: string | null
  size: CreatureSize
  type: string
  challengeRating: string
  armorClass: number
  hitPoints: number
  origin: CreatureOrigin
  imageUrl: string | null
}

/** Spec 016: what the board token picker gets of a creature (no stat block). */
export interface CreatureTokenOption {
  id: string
  name: string
  size: CreatureSize
  type: string
  imageUrl: string | null
}
export interface CreatureTokenOptionPage { items: CreatureTokenOption[]; total: number; page: number; pageSize: number }

export interface CreatureFields {
  name: string
  nameEn: string | null
  size: CreatureSize
  type: string
  alignment: string | null
  armorClass: number
  armorClassNote: string | null
  hitPoints: number
  hitDice: string | null
  speed: string | null
  initiative: number | null
  strength: number
  dexterity: number
  constitution: number
  intelligence: number
  wisdom: number
  charisma: number
  savingThrows: string | null
  skills: string | null
  damageVulnerabilities: string | null
  damageResistances: string | null
  damageImmunities: string | null
  conditionImmunities: string | null
  senses: string | null
  languages: string | null
  challengeRating: string
  experiencePoints: number | null
  proficiencyBonus: number | null
  traits: CreatureAction[]
  actions: CreatureAction[]
  bonusActions: CreatureAction[]
  reactions: CreatureAction[]
  legendaryActions: CreatureAction[]
  legendaryActionsIntro: string | null
  notes: string | null
}

export interface Creature extends CreatureFields {
  id: string
  baseCreatureId: string | null
  origin: CreatureOrigin
  sourceBook: string
  sourceUrl: string | null
  imageUrl: string | null
  hasImage: boolean
  footprintSize: number
  initiativeModifier: number
  revision: string
}

export interface CreaturePage { items: CreatureSummary[]; total: number; page: number; pageSize: number }

export interface CreatureQuery {
  q?: string
  size?: CreatureSize | ''
  type?: string
  crMin?: number | null
  crMax?: number | null
  origin?: CreatureOrigin | ''
  page?: number
  pageSize?: number
}

export type SaveCreatureRequest = CreatureFields & { expectedRevision: string | null }

export interface RollPreset { count: number; die: number; ability: AbilityKey | null; bonus: number }

export const CREATURE_SIZE_LABELS: Record<CreatureSize, string> = {
  Tiny: 'Miúdo', Small: 'Pequeno', Medium: 'Médio', Large: 'Grande', Huge: 'Enorme', Gargantuan: 'Imenso'
}

export const ORIGIN_LABELS: Record<CreatureOrigin, string> = { Compendium: 'Compêndio', Edited: 'Editada', Custom: 'Minha' }
