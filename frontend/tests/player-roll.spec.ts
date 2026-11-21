import type { DiceRollResult } from '~/types/campaign'
import {
  MISSING_SPELL, MISSING_WEAPON, NO_SPELLCASTING, SPELL_WITHOUT_ATTACK, cloneConfig, configFromSavedRoll, defaultKindFor, diceFromNotation,
  emptyConfig, kindsFor, plannedDice, plannedValues, resolveRoll, savedRollFromConfig, sourceFromKey, sourceKey, usesStrDex,
  weaponDamageExpression, weaponDamageBreakdownPlan, type RollConfig
} from '~/utils/playerRoll'
import { buildExpression } from '~/utils/rollExpression'
import { makeFighter, savedRollData } from './player-roll-fixture'

// Spec 011, US4 and US5: the panel turns references into the canonical expression the server runs.
// Every number comes from the server's stats; the client never adds proficiency or modifiers.
const fighter = makeFighter()
const stats = fighter.stats
const config = (patch: Partial<RollConfig>): RollConfig => ({ ...emptyConfig(), ...patch })
const resolve = (patch: Partial<RollConfig>) => resolveRoll(config(patch), stats, fighter)

describe('diceFromNotation', () => {
  it('reads the dice written on items and spells', () => {
    expect(diceFromNotation('2d6+1d4')).toEqual({ d6: 2, d4: 1 })
    expect(diceFromNotation('1d8')).toEqual({ d8: 1 })
    expect(diceFromNotation('d20')).toEqual({ d20: 1 })
    expect(diceFromNotation(' 8d6 ')).toEqual({ d6: 8 })
    expect(diceFromNotation('1d6+1d6')).toEqual({ d6: 2 })
  })
  it('ignores what the pad cannot hold', () => {
    expect(diceFromNotation('')).toEqual({})
    expect(diceFromNotation(null)).toEqual({})
    expect(diceFromNotation('1d7+2d6')).toEqual({ d6: 2 })
    expect(diceFromNotation('abc')).toEqual({})
  })
})

describe('resolveRoll: US4 sources', () => {
  it('attack with Espada Longa +1, FOR 16, level 5: server parts in order, labeled', () => {
    const roll = resolve({ kind: 'Attack', source: { type: 'weapon', itemId: 'sword' } })
    expect(roll.expression).toBe('d20 + (FOR) 3 + (prof) 3 + (arma) 1')
    expect(roll.label).toBe('Ataque: Espada Longa')
    expect(roll.error).toBeNull()
    expect(roll.dice).toEqual({ d20: 1 })
  })

  it('damage uses the weapon dice and the damage parts, never proficiency', () => {
    const roll = resolve({ kind: 'Damage', source: { type: 'weapon', itemId: 'sword' } })
    expect(roll.expression).toBe('d8 + (FOR) 3 + (arma) 1')
    expect(roll.label).toBe('Dano: Espada Longa')
    expect(roll.expression).not.toContain('prof')
  })

  it('versatile damage uses the two-handed dice when chosen', () => {
    const roll = resolve({ kind: 'Damage', source: { type: 'weapon', itemId: 'sword', versatile: true } })
    expect(roll.expression).toBe('d10 + (FOR) 3 + (arma) 1')
    expect(roll.label).toBe('Dano: Espada Longa (duas mãos)')
    // A weapon without a versatile die keeps its normal damage.
    expect(resolve({ kind: 'Damage', source: { type: 'weapon', itemId: 'bow', versatile: true } }).expression).toBe('d8 + (DES) 2')
  })

  it('does not add proficiency the server did not send (FR-030)', () => {
    // The Arco Longo is not proficient: its attack parts have only DES, and the panel adds nothing.
    const roll = resolve({ kind: 'Attack', source: { type: 'weapon', itemId: 'bow' } })
    expect(roll.expression).toBe('d20 + (DES) 2')
    expect(roll.parts).toEqual([{ label: 'DES', value: 2 }])
  })

  it('skill test, saving throw and ability test', () => {
    expect(resolve({ kind: 'Check', source: { type: 'skill', key: 'furtividade' } })).toMatchObject({
      expression: 'd20 + (DES) 2 + (prof) 3', label: 'Teste: Furtividade', error: null
    })
    expect(resolve({ kind: 'Check', source: { type: 'save', ability: 'Dex' } })).toMatchObject({
      expression: 'd20 + (DES) 2 + (prof) 3', label: 'Salvaguarda: DES', error: null
    })
    expect(resolve({ kind: 'Check', source: { type: 'ability', ability: 'Str' } })).toMatchObject({
      expression: 'd20 + (FOR) 3', label: 'Teste: Força', error: null
    })
  })

  it('spell attack uses the server spell attack parts; spell damage uses the spell dice', () => {
    expect(resolve({ kind: 'Attack', source: { type: 'spell', spellId: 'firebolt' } })).toMatchObject({
      expression: 'd20 + (INT) 4 + (prof) 3', label: 'Ataque: Raio de Fogo', error: null
    })
    expect(resolve({ kind: 'Damage', source: { type: 'spell', spellId: 'fireball' } })).toMatchObject({
      expression: '8d6', label: 'Dano: Bola de Fogo', error: null
    })
  })

  it('spell attack without attack parts explains why', () => {
    expect(resolve({ kind: 'Attack', source: { type: 'spell', spellId: 'fireball' } }).error).toBe(SPELL_WITHOUT_ATTACK)
    const noCasting = makeFighter()
    noCasting.stats.spellRolls[0] = { ...noCasting.stats.spellRolls[0]!, attack: null, attackBonus: null }
    expect(resolveRoll(config({ kind: 'Attack', source: { type: 'spell', spellId: 'firebolt' } }), noCasting.stats, noCasting).error).toBe(NO_SPELLCASTING)
  })

  it('a spell without damage dice takes the pad dice', () => {
    const custom = makeFighter()
    custom.stats.spellRolls[1] = { ...custom.stats.spellRolls[1]!, damage: null }
    const empty = resolveRoll(config({ kind: 'Damage', source: { type: 'spell', spellId: 'fireball' } }), custom.stats, custom)
    expect(empty.expression).toBe('')
    expect(empty.error).toBe('Escolha pelo menos um dado.')
    expect(resolveRoll(config({ kind: 'Damage', source: { type: 'spell', spellId: 'fireball' }, dice: { d6: 3 } }), custom.stats, custom).expression).toBe('3d6')
  })

  it('missing weapon or spell blocks the roll', () => {
    const weapon = resolve({ kind: 'Attack', source: { type: 'weapon', itemId: 'gone' } })
    expect(weapon.error).toBe(MISSING_WEAPON)
    expect(weapon.label).toBe('Ataque: arma ausente')
    expect(resolve({ kind: 'Damage', source: { type: 'spell', spellId: 'gone' } }).error).toBe(MISSING_SPELL)
  })

  it('pad dice are added to the source dice, extras go after the server parts', () => {
    const roll = resolve({
      kind: 'Check', source: { type: 'skill', key: 'furtividade' }, dice: { d4: 1 },
      extraModifiers: [{ label: 'Bênção', value: 0 }, { label: 'Guia', value: 1 }, { label: null, value: -2 }]
    })
    expect(roll.expression).toBe('d20 + d4 + (DES) 2 + (prof) 3 + (Guia) 1 - 2')
    const damage = resolve({ kind: 'Damage', source: { type: 'weapon', itemId: 'sword' }, dice: { d6: 2 } })
    expect(damage.expression).toBe('d8 + 2d6 + (FOR) 3 + (arma) 1')
  })

  it('free roll: pad dice and extras, legacy compact form when nothing is labeled', () => {
    expect(resolve({ kind: 'Free', dice: { d6: 2 }, extraModifiers: [{ label: null, value: 5 }] }).expression).toBe('2d6+5')
    expect(resolve({ kind: 'Free', dice: { d6: 2 } }).label).toBe('Rolagem livre')
    expect(resolve({ kind: 'Free', source: { type: 'ability', ability: 'Dex' }, dice: { d6: 1 } })).toMatchObject({
      expression: 'd6 + (DES) 2', label: 'Rolagem: DES'
    })
  })

  it('every expression equals buildExpression of the same dice and parts', () => {
    const roll = resolve({ kind: 'Attack', source: { type: 'weapon', itemId: 'sword' }, advantage: 'Advantage', repetitions: 2 })
    expect(roll.expression).toBe(buildExpression({ dice: roll.dice, parts: roll.parts, selection: 'advantage', repetitions: 2 }))
  })

  it('rejects sources that do not fit the kind', () => {
    expect(resolve({ kind: 'Damage', source: { type: 'skill', key: 'furtividade' } }).error).toBe('Perícia e salvaguarda só valem para testes.')
    expect(resolve({ kind: 'Check', source: { type: 'weapon', itemId: 'sword' } }).error).toBe('Arma e magia valem para ataque ou dano.')
  })

  it('keeps the history label within 80 characters', () => {
    const long = makeFighter()
    long.stats.weaponRolls[0] = { ...long.stats.weaponRolls[0]!, name: 'Espada'.repeat(20) }
    const roll = resolveRoll(config({ kind: 'Attack', source: { type: 'weapon', itemId: 'sword' } }), long.stats, long)
    expect(roll.label.length).toBeLessThanOrEqual(80)
    expect(roll.label.startsWith('Ataque: Espada')).toBe(true)
  })
})

describe('resolveRoll: US5 advantage and repetitions', () => {
  const base = { kind: 'Free' as const, dice: { d20: 1, d6: 2 }, extraModifiers: [{ label: null, value: 5 }] }

  it('scenario 1: no advantage, one execution (legacy compact)', () => {
    expect(resolve(base).expression).toBe('1d20+2d6+5')
  })
  it('scenario 2: advantage wraps the whole expression', () => {
    expect(resolve({ ...base, advantage: 'Advantage' }).expression).toBe('2#(d20 + 2d6 + 5) maior')
  })
  it('scenario 3: disadvantage', () => {
    expect(resolve({ ...base, advantage: 'Disadvantage' }).expression).toBe('2#(d20 + 2d6 + 5) menor')
  })
  it('scenario 4: 3 repetitions are always bracketed (request 7.1)', () => {
    expect(resolve({ ...base, repetitions: 3 }).expression).toBe('3#[d20 + 2d6 + 5]')
    expect(resolve({ kind: 'Check', source: { type: 'ability', ability: 'Str' }, repetitions: 3 }).expression).toBe('3#[d20 + (FOR) 3]')
  })
  it('scenario 5: 3 repetitions with advantage', () => {
    expect(resolve({ ...base, repetitions: 3, advantage: 'Advantage' }).expression).toBe('3#[2#(d20 + 2d6 + 5) maior]')
    expect(resolve({ kind: 'Attack', source: { type: 'weapon', itemId: 'sword' }, repetitions: 3, advantage: 'Disadvantage' }).expression)
      .toBe('3#[2#(d20 + (FOR) 3 + (prof) 3 + (arma) 1) menor]')
  })
  it('scenario 6: limits are reported before sending', () => {
    // 3 dice x 2 executions x 20 repetitions = 120 > 100.
    expect(resolve({ ...base, repetitions: 20, advantage: 'Advantage' }).error).toContain('100 dados')
    expect(resolve({ ...base, repetitions: 16 }).error).toBeNull()
    expect(resolve({ ...base, dice: { d6: 21 } }).error).toBe('Use até 20 dados de cada tipo.')
    expect(resolve({ ...base, extraModifiers: [{ label: null, value: 101 }] }).error).toBe('Cada adicional deve ficar entre -100 e +100.')
    expect(resolve({ ...base, extraModifiers: [{ label: 'Rótulo comprido demais', value: 1 }] }).error).toContain('rótulo')
  })
})

describe('sources, kinds and the armor rule', () => {
  it('round-trips source keys', () => {
    for (const source of [null, { type: 'ability', ability: 'Str' }, { type: 'skill', key: 'furtividade' }, { type: 'save', ability: 'Dex' }, { type: 'weapon', itemId: 'sword' }, { type: 'spell', spellId: 'fireball' }] as const) {
      expect(sourceFromKey(sourceKey(source))).toEqual(source)
    }
    expect(sourceFromKey('none')).toBeNull()
    expect(sourceFromKey('weapon:')).toBeNull()
    expect(sourceFromKey('luck:7')).toBeNull()
  })
  it('allowed kinds per source', () => {
    expect(kindsFor({ type: 'skill', key: 'furtividade' })).toEqual(['Check'])
    expect(kindsFor({ type: 'weapon', itemId: 'sword' })).toEqual(['Attack', 'Damage'])
    expect(kindsFor(null)).toEqual(['Check', 'Attack', 'Damage', 'Free'])
    expect(defaultKindFor({ type: 'weapon', itemId: 'sword' }, 'Check', stats)).toBe('Attack')
    expect(defaultKindFor({ type: 'spell', spellId: 'fireball' }, 'Check', stats)).toBe('Damage')
    expect(defaultKindFor({ type: 'skill', key: 'furtividade' }, 'Free', stats)).toBe('Check')
    expect(defaultKindFor({ type: 'ability', ability: 'Str' }, 'Free', stats)).toBe('Check')
    expect(defaultKindFor({ type: 'ability', ability: 'Str' }, 'Damage', stats)).toBe('Damage')
    expect(defaultKindFor(null, 'Attack', stats)).toBe('Attack')
  })
  it('FOR and DES d20 rolls are the ones the armor rule touches', () => {
    expect(usesStrDex(config({ kind: 'Check', source: { type: 'skill', key: 'furtividade' } }), stats)).toBe(true)
    expect(usesStrDex(config({ kind: 'Check', source: { type: 'skill', key: 'arcanismo' } }), stats)).toBe(false)
    expect(usesStrDex(config({ kind: 'Attack', source: { type: 'weapon', itemId: 'sword' } }), stats)).toBe(true)
    expect(usesStrDex(config({ kind: 'Damage', source: { type: 'weapon', itemId: 'sword' } }), stats)).toBe(false)
    expect(usesStrDex(config({ kind: 'Check', source: { type: 'save', ability: 'Dex' } }), stats)).toBe(true)
    expect(usesStrDex(config({ kind: 'Attack', source: { type: 'spell', spellId: 'firebolt' } }), stats)).toBe(false)
  })
  it('damage after a hit: the same weapon, no pad dice, no extras', () => {
    const attack = config({ kind: 'Attack', source: { type: 'weapon', itemId: 'sword' }, dice: { d4: 1 }, extraModifiers: [{ label: null, value: 2 }], advantage: 'Advantage' })
    expect(weaponDamageExpression(attack, stats)).toBe('d8 + (FOR) 3 + (arma) 1')
    expect(weaponDamageExpression(config({ kind: 'Attack', source: { type: 'weapon', itemId: 'gone' } }), stats)).toBeNull()
    expect(weaponDamageExpression(config({ kind: 'Check' }), stats)).toBeNull()
  })
  it('saved attack damage appends independent dice and retains a typed breakdown plan', () => {
    const attack = config({ kind: 'Attack', source: { type: 'weapon', itemId: 'sword' }, baseDamageType: 'cortante', damageComponents: [
      { label: 'Encantamento', dice: { d4: 1 }, damageType: 'gélido' }
    ] })
    expect(weaponDamageExpression(attack, stats)).toBe('d8 + (FOR) 3 + (arma) 1+1d4')
    expect(weaponDamageBreakdownPlan(attack, stats)).toEqual({ baseType: 'cortante', baseTermCount: 1, components: [
      { label: 'Encantamento', damageType: 'gélido', termCount: 1 }
    ] })
  })
})

describe('saved rolls keep references (FR-034)', () => {
  it('maps SavedRollData to the panel and back with the same fields', () => {
    const data = savedRollData({ dice: { d4: 1 }, extraModifiers: [{ label: 'Guia', value: 1 }], advantage: 'Advantage', repetitions: 2 })
    const panel = configFromSavedRoll(data)
    expect(panel).toEqual({ kind: 'Attack', source: { type: 'weapon', itemId: 'sword' }, dice: { d4: 1 }, extraModifiers: [{ label: 'Guia', value: 1 }], advantage: 'Advantage', repetitions: 2, baseDamageType: null, damageComponents: [] })
    expect(savedRollFromConfig(panel, '  Ataque com espada ', 'lucide:sword')).toEqual(data)
  })
  it('persists additional damage components and the selected base type', () => {
    const data = savedRollData({ baseDamageType: 'cortante', damageComponents: [
      { label: 'Encantamento', dice: { d4: 1 }, damageType: 'gélido' }
    ] })
    const panel = configFromSavedRoll(data)
    expect(savedRollFromConfig(panel, data.name, data.icon)).toEqual(data)
  })
  it('T074: a two-handed damage roll survives the trip through the saved roll (d10, not d8)', () => {
    const panel = config({ kind: 'Damage', source: { type: 'weapon', itemId: 'sword', versatile: true } })
    expect(resolveRoll(panel, stats, fighter).expression).toBe('d10 + (FOR) 3 + (arma) 1')
    const saved = savedRollFromConfig(panel, 'Espada com duas mãos', 'lucide:sword')
    expect(saved.versatile).toBe(true)
    const back = configFromSavedRoll(saved)
    expect(back.source).toEqual({ type: 'weapon', itemId: 'sword', versatile: true })
    expect(resolveRoll(back, stats, fighter).expression).toBe('d10 + (FOR) 3 + (arma) 1')
    expect(resolveRoll(back, stats, fighter).label).toBe('Dano: Espada Longa (duas mãos)')
  })
  it('T074: versatile goes as true only for weapon damage (the server rejects it elsewhere)', () => {
    const attack = config({ kind: 'Attack', source: { type: 'weapon', itemId: 'sword', versatile: true } })
    expect(savedRollFromConfig(attack, 'X', 'lucide:sword').versatile).toBe(false)
    expect(savedRollFromConfig(config({ kind: 'Damage', source: { type: 'weapon', itemId: 'sword' } }), 'X', 'lucide:sword').versatile).toBe(false)
    expect(savedRollFromConfig(config({ kind: 'Damage', source: { type: 'spell', spellId: 'fireball' } }), 'X', 'lucide:flame').versatile).toBe(false)
    // An old saved roll without the field, or a flag left on an attack, loads as one hand.
    expect(configFromSavedRoll(savedRollData({ kind: 'Damage', versatile: undefined })).source).toEqual({ type: 'weapon', itemId: 'sword' })
    expect(configFromSavedRoll(savedRollData({ kind: 'Attack', versatile: true })).source).toEqual({ type: 'weapon', itemId: 'sword' })
  })
  it('stores exactly one reference per source type', () => {
    expect(savedRollFromConfig(config({ kind: 'Check', source: { type: 'skill', key: 'furtividade' } }), 'Furtivo', 'lucide:footprints')).toMatchObject({
      skill: 'furtividade', ability: null, savingThrow: null, inventoryItemId: null, characterSpellId: null
    })
    expect(savedRollFromConfig(config({ kind: 'Check', source: { type: 'save', ability: 'Dex' } }), 'Reflexo', 'lucide:zap')).toMatchObject({ savingThrow: 'Dex', skill: null })
    expect(savedRollFromConfig(config({ kind: 'Damage', source: { type: 'spell', spellId: 'fireball' } }), 'Bola', 'lucide:flame')).toMatchObject({ characterSpellId: 'fireball', inventoryItemId: null })
    expect(savedRollFromConfig(config({ kind: 'Free', source: { type: 'ability', ability: 'Str' }, dice: { d6: 0, d8: 1 } }), 'X', 'lucide:dices')).toMatchObject({ ability: 'Str', dice: { d8: 1 } })
    expect(savedRollFromConfig(config({ extraModifiers: [{ label: '  ', value: 2 }] }), 'X', 'lucide:dices').extraModifiers).toEqual([{ label: null, value: 2 }])
  })
  it('a saved roll uses the current sheet: raising FOR changes the expression', () => {
    const panel = configFromSavedRoll(savedRollData())
    const stronger = makeFighter()
    stronger.stats.weaponRolls[0]!.attack[0] = { ...stronger.stats.weaponRolls[0]!.attack[0]!, value: 4 }
    expect(resolveRoll(panel, stats, fighter).expression).toBe('d20 + (FOR) 3 + (prof) 3 + (arma) 1')
    expect(resolveRoll(panel, stronger.stats, stronger).expression).toBe('d20 + (FOR) 4 + (prof) 3 + (arma) 1')
  })
  it('cloneConfig copies deeply', () => {
    const original = config({ source: { type: 'weapon', itemId: 'sword' }, dice: { d6: 1 }, extraModifiers: [{ label: 'a', value: 1 }] })
    const copy = cloneConfig(original)
    copy.dice.d6 = 5; copy.extraModifiers[0]!.value = 9
    expect(original.dice.d6).toBe(1)
    expect(original.extraModifiers[0]!.value).toBe(1)
  })
})

describe('animation dice', () => {
  it('animates every execution of every repetition', () => {
    expect(plannedDice({ d20: 1 }, 'advantage', 3)).toHaveLength(6)
    expect(plannedDice({ d20: 1, d6: 2 }, 'none', 1).map(die => die.faces)).toEqual([20, 6, 6])
    expect(plannedDice({ d6: 20 }, 'advantage', 20)).toHaveLength(100)
  })
  it('reads both executions when the server sends them', () => {
    const term = (expression: string, values: number[]) => ({ expression, values, subtotal: values.reduce((a, b) => a + b, 0) })
    const results: DiceRollResult[] = [{
      sequence: 1, terms: [term('d20', [17])], modifier: 3, total: 20, selection: 'maior', selectedExecution: 1,
      executions: [{ terms: [term('d20', [4])], modifier: 3, total: 7 }, { terms: [term('d20', [17])], modifier: 3, total: 20 }]
    }]
    expect(plannedValues(results)).toEqual([{ faces: 20, value: 4 }, { faces: 20, value: 17 }])
    expect(plannedValues([{ sequence: 1, terms: [term('2d6', [3, 5])], modifier: 0, total: 8 }])).toEqual([{ faces: 6, value: 3 }, { faces: 6, value: 5 }])
  })
})
