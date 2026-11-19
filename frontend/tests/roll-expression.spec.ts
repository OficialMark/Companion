import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { buildExpression, expressionLimits, type RollExpressionInput } from '~/utils/rollExpression'

interface DiceCase {
  input: string
  canonical: string | null
  error: string | null
  roundTrip: boolean
  build?: RollExpressionInput
  note?: string
}

// Mesmo arquivo que backend/tests/Companion.UnitTests/DiceExpressionGroupingTests.cs lê.
// O vitest roda em frontend/ (pnpm --dir frontend test); no jsdom, import.meta.url não é file://.
const casesPath = resolve(process.cwd(), '../backend/tests/Fixtures/dice-expressions.cases.json')
const { cases } = JSON.parse(readFileSync(casesPath, 'utf8')) as { cases: DiceCase[] }
const roundTrip = cases.filter(item => item.roundTrip)

const base: RollExpressionInput = { dice: { d20: 1 }, parts: [], selection: 'none', repetitions: 1 }

describe('buildExpression', () => {
  it('has round-trip cases in the shared file', () => {
    expect(roundTrip.length).toBeGreaterThanOrEqual(20)
  })

  it.each(roundTrip.map(item => [item.canonical, item] as const))('generates %s exactly', (_, item) => {
    expect(buildExpression(item.build!)).toBe(item.canonical)
    expect(expressionLimits(item.build!)).toBeNull()
  })

  it('returns an empty string without dice', () => {
    expect(buildExpression({ ...base, dice: {} })).toBe('')
    expect(buildExpression({ ...base, dice: { d20: 0 }, parts: [{ label: 'FOR', value: 3 }] })).toBe('')
  })

  it('treats a blank label as an unlabeled constant', () => {
    expect(buildExpression({ ...base, parts: [{ label: '   ', value: 2 }] })).toBe('1d20+2')
  })
})

describe('expressionLimits', () => {
  it('counts both executions of advantage and every repetition', () => {
    expect(expressionLimits({ ...base, dice: { d6: 50 }, selection: 'advantage' })).toBeNull()
    expect(expressionLimits({ ...base, dice: { d6: 51 }, selection: 'advantage' })).toContain('102 dados')
    expect(expressionLimits({ ...base, dice: { d6: 10 }, selection: 'disadvantage', repetitions: 5 })).toBeNull()
    expect(expressionLimits({ ...base, dice: { d6: 10, d4: 1 }, selection: 'disadvantage', repetitions: 5 })).toContain('110 dados')
  })

  it('accepts 1 to 20 repetitions', () => {
    expect(expressionLimits({ ...base, repetitions: 20 })).toBeNull()
    expect(expressionLimits({ ...base, repetitions: 21 })).toContain('1 a 20 repetições')
    expect(expressionLimits({ ...base, repetitions: 0 })).toContain('1 a 20 repetições')
    expect(expressionLimits({ ...base, repetitions: 1.5 })).toContain('1 a 20 repetições')
  })

  it('requires at least one die', () => {
    expect(expressionLimits({ ...base, dice: {} })).toBe('Escolha pelo menos um dado.')
  })

  it('rejects negative or fractional dice counts', () => {
    expect(expressionLimits({ ...base, dice: { d20: -1 } })).toContain('número inteiro')
    expect(expressionLimits({ ...base, dice: { d20: 1.5 } })).toContain('número inteiro')
  })

  it('limits the total modifier to 10000', () => {
    expect(expressionLimits({ ...base, parts: [{ label: 'FOR', value: 10000 }, { label: null, value: -1 }] })).toBeNull()
    expect(expressionLimits({ ...base, parts: [{ label: 'FOR', value: 10000 }, { label: null, value: 1 }] })).toContain('-10000 e 10000')
  })

  it('rejects labels the server would reject', () => {
    expect(expressionLimits({ ...base, parts: [{ label: 'abcdefghijklmnopq', value: 1 }] })).toContain('até 16 caracteres')
    expect(expressionLimits({ ...base, parts: [{ label: 'FOR!', value: 1 }] })).toContain('até 16 caracteres')
    expect(expressionLimits({ ...base, parts: [{ label: "Mão leve D'Ar.+", value: 1 }] })).toBeNull()
  })

  it('rejects a canonical form longer than 256 characters', () => {
    const parts = Array.from({ length: 11 }, (_, index) => ({ label: `Modificador ${String(index + 1).padStart(4, '0')}`, value: 1 }))
    expect(buildExpression({ ...base, parts })).toHaveLength(256)
    expect(expressionLimits({ ...base, parts })).toBeNull()
    parts[10] = { ...parts[10]!, value: 10 }
    expect(expressionLimits({ ...base, parts })).toContain('256 caracteres')
  })
})
