import type { Character, CharacterStats, StatPart } from '~/types/character'
import { SKILLS } from '~/types/character'

const part = (label: string, value: number, source = label): StatPart => ({ label, value, source, itemId: null })
const value = (n: number, parts: StatPart[] = [part('base', n)]) => ({ value: n, parts })

/** A server-shaped character (spec 011 CharacterDto) for component tests. */
export function makeCharacter(overrides: Partial<Character> = {}): Character {
  const abilities = Object.fromEntries((['str', 'dex', 'con', 'int', 'wis', 'cha'] as const).map(key => [key, {
    code: (key[0].toUpperCase() + key.slice(1)) as never, abbreviation: key.toUpperCase(), base: 10, final: 10, modifier: 0, parts: [part('base', 10)]
  }])) as CharacterStats['abilities']
  const checks = Object.fromEntries((['str', 'dex', 'con', 'int', 'wis', 'cha'] as const).map(key => [key, {
    key, name: key, ability: 'Str' as const, training: 'None' as const, bonus: 0, parts: []
  }])) as CharacterStats['savingThrows']
  const stats: CharacterStats = {
    abilities,
    proficiencyBonus: 2,
    savingThrows: checks,
    skills: Object.fromEntries(SKILLS.map(skill => [skill.key, { key: skill.key, name: skill.name, ability: skill.ability, training: 'None' as const, bonus: 0, parts: [] }])),
    passivePerception: value(10),
    initiative: value(0),
    armorClass: value(10),
    maxHitPoints: value(10),
    speed: { walk: { value: 9, parts: [part('base', 9)] } },
    senses: {},
    resistances: [],
    immunities: [],
    conditions: [],
    textEffects: [],
    spellSaveDc: null,
    spellAttackBonus: null,
    weaponRolls: [],
    spellRolls: [],
    warnings: [],
    suppressedEffects: [],
    rollDefaults: { disadvantageOnStrDex: false, reason: null }
  }
  return {
    participantId: 'p1',
    version: 'AAAAAAAAB9E=',
    sheet: {
      characterName: null, className: null, subclass: null, species: null, background: null, alignment: null,
      level: 1, experience: 0, strength: 10, dexterity: 10, constitution: 10, intelligence: 10, wisdom: 10, charisma: 10,
      savingThrowProficiencies: [], skills: {}, generalProficiencies: { armor: [], shields: false, simpleWeapons: false, martialWeapons: false, other: null },
      baseMaxHitPoints: 10, currentHitPoints: 10, temporaryHitPoints: 0, unarmoredBaseArmorClass: 10, unarmoredAbility: null,
      baseSpeed: 9, baseSenses: {}, spellcastingAbility: null, traits: null, notes: null
    },
    items: [],
    spells: [],
    savedRolls: [],
    stats,
    notices: [],
    ...overrides
  }
}
