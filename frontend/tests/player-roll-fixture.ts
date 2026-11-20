import type { Character, CharacterSpell, SavedRoll, SavedRollData, StatPart } from '~/types/character'
import { makeCharacter } from './character-fixture'

const part = (label: string, value: number, source = label): StatPart => ({ label, value, source, itemId: null })

/**
 * US4 example as the server calculates it: level 5 fighter (proficiency +3), FOR 16 (+3), DES 14 (+2),
 * an equipped, proficient Espada Longa +1 (versatile 1d10), a Arco Longo not equipped, Furtividade
 * proficient, DES save proficient, INT 18 (+4) as spellcasting ability, Raio de Fogo (attack) and
 * Bola de Fogo (save, 8d6). Every number below is what the server would send; the client adds none.
 */
export function makeFighter(overrides: Partial<Character> = {}): Character {
  const character = makeCharacter()
  const stats = character.stats
  stats.proficiencyBonus = 3
  stats.abilities.str = { ...stats.abilities.str, code: 'Str', abbreviation: 'FOR', base: 16, final: 16, modifier: 3 }
  stats.abilities.dex = { ...stats.abilities.dex, code: 'Dex', abbreviation: 'DES', base: 14, final: 14, modifier: 2 }
  stats.abilities.int = { ...stats.abilities.int, code: 'Int', abbreviation: 'INT', base: 18, final: 18, modifier: 4 }
  stats.skills.furtividade = { key: 'furtividade', name: 'Furtividade', ability: 'Dex', training: 'Proficient', bonus: 5, parts: [part('DES', 2), part('prof', 3)] }
  stats.skills.atletismo = { key: 'atletismo', name: 'Atletismo', ability: 'Str', training: 'None', bonus: 3, parts: [part('FOR', 3)] }
  stats.skills.arcanismo = { key: 'arcanismo', name: 'Arcanismo', ability: 'Int', training: 'None', bonus: 4, parts: [part('INT', 4)] }
  stats.savingThrows.dex = { key: 'dex', name: 'Destreza', ability: 'Dex', training: 'Proficient', bonus: 5, parts: [part('DES', 2), part('prof', 3)] }
  stats.savingThrows.str = { key: 'str', name: 'Força', ability: 'Str', training: 'Proficient', bonus: 6, parts: [part('FOR', 3), part('prof', 3)] }
  stats.weaponRolls = [
    {
      itemId: 'sword', name: 'Espada Longa', equipped: true, ability: 'Str', attackBonus: 7,
      attack: [part('FOR', 3), part('prof', 3), part('arma', 1)],
      damage: { dice: '1d8', damageType: 'cortante', parts: [part('FOR', 3), part('arma', 1)] },
      versatile: { dice: '1d10', damageType: 'cortante', parts: [part('FOR', 3), part('arma', 1)] }
    },
    {
      itemId: 'bow', name: 'Arco Longo', equipped: false, ability: 'Dex', attackBonus: 2,
      attack: [part('DES', 2)],
      damage: { dice: '1d8', damageType: 'perfurante', parts: [part('DES', 2)] },
      versatile: null
    }
  ]
  stats.spellRolls = [
    { spellId: 'firebolt', name: 'Raio de Fogo', attackBonus: 7, attack: [part('INT', 4), part('prof', 3)], saveDc: null, saveAbility: null, damage: { dice: '2d10', damageType: 'fogo', parts: [] } },
    { spellId: 'fireball', name: 'Bola de Fogo', attackBonus: null, attack: null, saveDc: 15, saveAbility: 'Dex', damage: { dice: '8d6', damageType: 'fogo', parts: [] } }
  ]
  const spell = (id: string, name: string, attackKind: 'Attack' | 'Save'): CharacterSpell => ({
    id, sortOrder: 0, catalogSpellId: null,
    data: {
      name, level: 0, school: null, castingTime: null, range: null, components: null, duration: null, ritual: false, concentration: false,
      description: null, attackKind, saveAbility: attackKind === 'Save' ? 'Dex' : null, damageDice: null, damageType: null, addAbilityToDamage: false
    }
  })
  character.spells = [spell('firebolt', 'Raio de Fogo', 'Attack'), spell('fireball', 'Bola de Fogo', 'Save')]
  character.sheet = { ...character.sheet, level: 5, strength: 16, dexterity: 14, intelligence: 18, spellcastingAbility: 'Int' }
  return { ...character, ...overrides }
}

export function savedRollData(overrides: Partial<SavedRollData> = {}): SavedRollData {
  return {
    name: 'Ataque com espada', icon: 'lucide:sword', kind: 'Attack', dice: {}, ability: null, skill: null, savingThrow: null,
    inventoryItemId: 'sword', characterSpellId: null, versatile: false, extraModifiers: [], advantage: 'None', repetitions: 1, ...overrides
  }
}

export function savedRoll(id: string, overrides: Partial<SavedRollData> = {}, incomplete = false, sortOrder = 0): SavedRoll {
  return { id, sortOrder, incomplete, data: savedRollData(overrides) }
}
