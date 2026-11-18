import { abilityModifier, buildExpression, clampPreset, describeRoll, modifierFor, parseActionRolls } from '~/utils/creatureRoll'

const goblin = { strength: 8, dexterity: 14, constitution: 10, intelligence: 10, wisdom: 8, charisma: 8 }

describe('creature rolls (spec 008)', () => {
  it('uses the D&D modifier table (same rule as the server)', () => {
    expect([1, 8, 9, 10, 11, 14, 30].map(abilityModifier)).toEqual([-5, -1, -1, 0, 0, 2, 10])
    expect(modifierFor(goblin, 'dex')).toBe(2)
    expect(modifierFor(goblin, 'wis')).toBe(-1)
    expect(modifierFor(goblin, null)).toBe(0)
  })

  it('builds the server expression and a readable preview, dropping zero terms', () => {
    expect(buildExpression({ count: 1, die: 20, ability: 'dex', bonus: 1 }, 2)).toBe('1d20+2+1')
    expect(buildExpression({ count: 1, die: 20, ability: 'wis', bonus: -2 }, -1)).toBe('1d20-1-2')
    expect(buildExpression({ count: 2, die: 6, ability: null, bonus: 0 }, 0)).toBe('2d6')
    expect(describeRoll({ count: 1, die: 20, ability: 'dex', bonus: 1 }, 2)).toBe('1d20 + 2 (DES) + 1')
    expect(describeRoll({ count: 1, die: 20, ability: 'wis', bonus: -2 }, -1)).toBe('1d20 − 1 (SAB) − 2')
  })

  it('clamps presets to the allowed ranges', () => {
    expect(clampPreset({ count: 50, die: 7, ability: 'luck' as never, bonus: 99 })).toEqual({ count: 20, die: 20, ability: null, bonus: 20 })
    expect(clampPreset({ count: 0, die: 100, ability: 'cha', bonus: -99 })).toEqual({ count: 1, die: 100, ability: 'cha', bonus: -20 })
  })

  it('reads hit and damage shortcuts from Pocket DM action text (2014 and 2024 styles)', () => {
    const scimitar = parseActionRolls('Ataque com Arma Corpo a Corpo: +4 para acertar, alcance 1,5 m (5 ft), um alvo. 5 (1d6 + 2) de dano cortante.')
    expect(scimitar).toEqual([
      { kind: 'hit', label: 'Acerto +4', preset: { count: 1, die: 20, ability: null, bonus: 4 } },
      { kind: 'damage', label: 'Dano 1d6+2', preset: { count: 1, die: 6, ability: null, bonus: 2 } }
    ])
    const tentacle = parseActionRolls('Ataque Corpo a Corpo: +9, alcance 4,5 m (15 ft). 12 (2d6 + 5) de dano de concussão.')
    expect(tentacle.map(item => item.label)).toEqual(['Acerto +9', 'Dano 2d6+5'])
    expect(parseActionRolls('Salvaguarda de Inteligência: CD 16. Falha: 10 (3d6) de dano psíquico.').map(item => item.label)).toEqual(['Dano 3d6'])
    expect(parseActionRolls('O goblin pode Desengajar como ação bônus.')).toEqual([])
  })
})
