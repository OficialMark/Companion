// Spec 011: player character sheet, inventory, spells, saved rolls and catalogs.
// Mirrors backend/src/Companion.Domain/Characters and the DTOs of contracts/character-api.md.

export type AbilityCode = 'Str' | 'Dex' | 'Con' | 'Int' | 'Wis' | 'Cha'
export type AbilityKey = 'str' | 'dex' | 'con' | 'int' | 'wis' | 'cha'
export type SkillTraining = 'None' | 'Proficient' | 'Expertise'
export type ArmorType = 'Light' | 'Medium' | 'Heavy'
export type ItemCategory = 'Weapon' | 'Armor' | 'Shield' | 'Gear' | 'Misc' | 'Potion'
export type WeaponKind = 'Melee' | 'Ranged'
export type WeaponClass = 'Simple' | 'Martial'
export type SpellAttackKind = 'None' | 'Attack' | 'Save'
export type EffectType = 'Ability' | 'MaxHp' | 'ArmorClass' | 'Speed' | 'Vision' | 'Resistance' | 'Immunity'
  | 'Condition' | 'AttackBonus' | 'DamageBonus' | 'SaveBonus' | 'SkillBonus' | 'Initiative' | 'Text'
export type EffectOp = 'Add' | 'Min'
export type RollKind = 'Check' | 'Attack' | 'Damage' | 'Free'
export type AdvantageMode = 'None' | 'Advantage' | 'Disadvantage'
export type DieKey = 'd4' | 'd6' | 'd8' | 'd10' | 'd12' | 'd20' | 'd100'

export interface GeneralProficiencies {
  armor: ArmorType[]
  shields: boolean
  simpleWeapons: boolean
  martialWeapons: boolean
  other: string | null
}

export interface CharacterSheetData {
  characterName: string | null
  className: string | null
  subclass: string | null
  species: string | null
  background: string | null
  alignment: string | null
  level: number
  experience: number
  strength: number
  dexterity: number
  constitution: number
  intelligence: number
  wisdom: number
  charisma: number
  savingThrowProficiencies: AbilityCode[]
  skills: Record<string, SkillTraining>
  generalProficiencies: GeneralProficiencies
  baseMaxHitPoints: number
  currentHitPoints: number
  temporaryHitPoints: number
  unarmoredBaseArmorClass: number
  unarmoredAbility: AbilityCode | null
  baseSpeed: number
  baseSenses: Record<string, number>
  spellcastingAbility: AbilityCode | null
  traits: string | null
  notes: string | null
}

export interface ItemEffect { type: EffectType; target: string | null; op: EffectOp; value: number | null }

export interface WeaponData {
  damageDice: string
  damageType: string | null
  versatileDice: string | null
  kind: WeaponKind
  class: WeaponClass
  properties: string[]
  rangeNormal: number | null
  rangeLong: number | null
  abilityOverride: AbilityCode | null
  mastery: string | null
}

export interface ArmorData { baseAc: number; type: ArmorType; strengthMin: number | null; stealthDisadvantage: boolean }
export interface ShieldData { acBonus: number }

export interface InventoryItemData {
  name: string
  category: ItemCategory
  quantity: number
  weight: number | null
  description: string | null
  notes: string | null
  equipped: boolean
  proficient: boolean
  magicBonus: number
  weapon: WeaponData | null
  armor: ArmorData | null
  shield: ShieldData | null
  effects: ItemEffect[]
}

export interface InventoryItem {
  id: string
  sortOrder: number
  catalogItemId: string | null
  version: string
  data: InventoryItemData
}

export interface CharacterSpellData {
  name: string
  level: number
  school: string | null
  castingTime: string | null
  range: string | null
  components: string | null
  duration: string | null
  ritual: boolean
  concentration: boolean
  description: string | null
  attackKind: SpellAttackKind
  saveAbility: AbilityCode | null
  damageDice: string | null
  damageType: string | null
  addAbilityToDamage: boolean
}

export interface CharacterSpell { id: string; sortOrder: number; catalogSpellId: string | null; data: CharacterSpellData }

export interface RollModifier { label: string | null; value: number }

export interface SavedRollData {
  name: string
  icon: string
  kind: RollKind
  dice: Partial<Record<DieKey, number>>
  ability: AbilityCode | null
  skill: string | null
  savingThrow: AbilityCode | null
  inventoryItemId: string | null
  characterSpellId: string | null
  /** Two-handed damage of a versatile weapon. Only true with kind Damage and a weapon (SavedRollData.cs). */
  versatile?: boolean
  extraModifiers: RollModifier[]
  advantage: AdvantageMode
  repetitions: number
}

export interface SavedRoll { id: string; sortOrder: number; incomplete: boolean; data: SavedRollData }

export interface StatPart { label: string; value: number; source: string; itemId: string | null }
export interface ValueStat { value: number; parts: StatPart[] }
export interface AbilityStat { code: AbilityCode; abbreviation: string; base: number; final: number; modifier: number; parts: StatPart[] }
export interface CheckStat { key: string; name: string; ability: AbilityCode; training: SkillTraining; bonus: number; parts: StatPart[] }
export interface DamageRoll { dice: string; damageType: string | null; parts: StatPart[] }
export interface WeaponRoll { itemId: string; name: string; equipped: boolean; ability: AbilityCode; attackBonus: number; attack: StatPart[]; damage: DamageRoll; versatile: DamageRoll | null }
export interface SpellRoll { spellId: string; name: string; attackBonus: number | null; attack: StatPart[] | null; saveDc: number | null; saveAbility: AbilityCode | null; damage: DamageRoll | null }
export interface SuppressedEffect { itemId: string; itemName: string; effectKey: string; keptItemId: string; keptItemName: string }

export interface CharacterStats {
  abilities: Record<AbilityKey, AbilityStat>
  proficiencyBonus: number
  savingThrows: Record<AbilityKey, CheckStat>
  skills: Record<string, CheckStat>
  passivePerception: ValueStat
  initiative: ValueStat
  armorClass: ValueStat
  maxHitPoints: ValueStat
  speed: Record<string, { value: number; parts: StatPart[] }>
  senses: Record<string, { value: number; parts: StatPart[] }>
  resistances: string[]
  immunities: string[]
  conditions: string[]
  textEffects: string[]
  spellSaveDc: ValueStat | null
  spellAttackBonus: ValueStat | null
  weaponRolls: WeaponRoll[]
  spellRolls: SpellRoll[]
  warnings: string[]
  suppressedEffects: SuppressedEffect[]
  rollDefaults: { disadvantageOnStrDex: boolean; reason: string | null }
}

export interface Character {
  participantId: string
  sheet: CharacterSheetData
  version: string
  items: InventoryItem[]
  spells: CharacterSpell[]
  savedRolls: SavedRoll[]
  stats: CharacterStats
  notices: string[]
  /** Class, species and feat features with an optional use counter (spec 013, US2). */
  features: CharacterFeature[]
}

export type FeatureRecovery = 'None' | 'ShortRest' | 'LongRest'
export type RestKind = 'Short' | 'Long'

export interface CharacterFeatureData {
  name: string
  source: string | null
  description: string | null
  /** Null: no counter. Otherwise 1..999. */
  maxUses: number | null
  currentUses: number
  recovery: FeatureRecovery
  /** Spec 014: mechanical effects, always active (absent on features saved before it). */
  effects?: ItemEffect[] | null
}

export interface CharacterFeature { id: string; sortOrder: number; data: CharacterFeatureData }

export const RECOVERY_LABEL: Record<FeatureRecovery, string> = {
  None: 'Não recupera',
  ShortRest: 'Descanso curto',
  LongRest: 'Descanso longo'
}

/** Where a catalog entry comes from (spec 013): the global compendium, the table's edit of it, or the table's own. */
export type CatalogOrigin = 'Compendium' | 'Edited' | 'Custom'

export const ORIGIN_LABEL: Record<CatalogOrigin, string> = { Compendium: 'Compêndio', Edited: 'Editada', Custom: 'Personalizada' }

export interface CatalogItemSummary {
  id: string
  name: string
  nameEn: string | null
  category: ItemCategory
  subcategory: string | null
  rarity: string | null
  requiresAttunement: boolean
  magicBonus: number
  /** Generic magic weapon or armor ("Arma +1"): adding it asks for a mundane base item. */
  requiresBaseItem: boolean
  origin?: CatalogOrigin
}

export interface CatalogItem {
  id: string
  name: string
  nameEn: string | null
  category: ItemCategory
  subcategory: string | null
  rarity: string | null
  requiresAttunement: boolean
  cost: string | null
  requiresBaseItem: boolean
  template: InventoryItemData
  origin?: CatalogOrigin
  /** The global entry an edited entry replaces in this campaign. */
  baseId?: string | null
  /** The replaced global entry left the compendium: deleting the edit restores nothing. */
  baseRemoved?: boolean
}

/** The master's catalog item form (spec 013): the item itself plus the catalog-only fields. */
export interface SaveCatalogItem {
  template: InventoryItemData
  nameEn: string | null
  subcategory: string | null
  rarity: string | null
  requiresAttunement: boolean
  cost: string | null
}

export interface SaveCatalogSpell {
  template: CharacterSpellData
  classes: string[]
  higherLevels: string | null
  nameEn: string | null
}

export interface CatalogSpellSummary {
  id: string
  name: string
  /** Official English name (spec 012); null when unknown. */
  nameEn?: string | null
  level: number
  school: string | null
  ritual: boolean
  concentration: boolean
  classes: string[]
  origin?: CatalogOrigin
}

export interface CatalogSpell {
  id: string
  name: string
  nameEn?: string | null
  level: number
  classes: string[]
  higherLevels: string | null
  template: CharacterSpellData
  origin?: CatalogOrigin
  baseId?: string | null
  baseRemoved?: boolean
}

export interface CatalogPage<T> { items: T[]; total: number; page: number; pageSize: number; attribution: string | null }

export interface CatalogItemQuery { q?: string; category?: string; rarity?: string; page?: number; pageSize?: number; mundane?: boolean; origin?: CatalogOrigin | '' }
export interface CatalogSpellQuery { q?: string; level?: number | null; school?: string; class?: string; ritual?: boolean | null; concentration?: boolean | null; page?: number; pageSize?: number; origin?: CatalogOrigin | '' }

export const ABILITY_ORDER: AbilityCode[] = ['Str', 'Dex', 'Con', 'Int', 'Wis', 'Cha']
export const ABILITY_LABEL: Record<AbilityCode, string> = { Str: 'FOR', Dex: 'DES', Con: 'CON', Int: 'INT', Wis: 'SAB', Cha: 'CAR' }
export const ABILITY_NAME: Record<AbilityCode, string> = { Str: 'Força', Dex: 'Destreza', Con: 'Constituição', Int: 'Inteligência', Wis: 'Sabedoria', Cha: 'Carisma' }
export const abilityKey = (code: AbilityCode) => code.toLowerCase() as AbilityKey

export const SKILLS: { key: string; name: string; ability: AbilityCode }[] = [
  { key: 'acrobacia', name: 'Acrobacia', ability: 'Dex' },
  { key: 'adestrarAnimais', name: 'Adestrar Animais', ability: 'Wis' },
  { key: 'arcanismo', name: 'Arcanismo', ability: 'Int' },
  { key: 'atletismo', name: 'Atletismo', ability: 'Str' },
  { key: 'atuacao', name: 'Atuação', ability: 'Cha' },
  { key: 'enganacao', name: 'Enganação', ability: 'Cha' },
  { key: 'furtividade', name: 'Furtividade', ability: 'Dex' },
  { key: 'historia', name: 'História', ability: 'Int' },
  { key: 'intimidacao', name: 'Intimidação', ability: 'Cha' },
  { key: 'intuicao', name: 'Intuição', ability: 'Wis' },
  { key: 'investigacao', name: 'Investigação', ability: 'Int' },
  { key: 'medicina', name: 'Medicina', ability: 'Wis' },
  { key: 'natureza', name: 'Natureza', ability: 'Int' },
  { key: 'percepcao', name: 'Percepção', ability: 'Wis' },
  { key: 'persuasao', name: 'Persuasão', ability: 'Cha' },
  { key: 'prestidigitacao', name: 'Prestidigitação', ability: 'Dex' },
  { key: 'religiao', name: 'Religião', ability: 'Int' },
  { key: 'sobrevivencia', name: 'Sobrevivência', ability: 'Wis' }
]

export const CATEGORY_LABEL: Record<ItemCategory, string> = {
  Weapon: 'Arma', Armor: 'Armadura', Shield: 'Escudo', Gear: 'Equipamento', Misc: 'Diversos', Potion: 'Poção'
}
