export const ANIMATED_DICE_FACES = [4, 6, 8, 10, 12, 20, 100] as const

export type AnimatedDieFace = typeof ANIMATED_DICE_FACES[number]

export interface AnimatedDie {
  id: string
  faces: number
  value?: number
}

/** Whether `faces` has a dedicated polyhedron shape, vs. the generic fallback visual. */
export function hasStandardShape(faces: number): faces is AnimatedDieFace {
  return standardFaces.has(faces)
}

interface RollTerm { expression: string; values: number[] }
interface RollResult { terms: RollTerm[] }

/** Associates server-authoritative values with the matching visual dice. */
export function diceWithResults(dice: AnimatedDie[], results: RollResult[]): AnimatedDie[] {
  const valuesByFaces = new Map<number, number[]>()
  for (const result of results) {
    for (const term of result.terms) {
      const match = term.expression.match(/d(\d+)$/i)
      const faces = Number.parseInt(match?.[1] ?? '', 10)
      if (!Number.isSafeInteger(faces)) continue
      valuesByFaces.set(faces, [...(valuesByFaces.get(faces) ?? []), ...term.values])
    }
  }

  return dice.map((die) => ({ ...die, value: valuesByFaces.get(die.faces)?.shift() }))
}

const standardFaces = new Set<number>(ANIMATED_DICE_FACES)

/** Expands dice terms, including `#` repetitions, into visual dice for any valid face count. */
export function diceForAnimation(expression: string): AnimatedDie[] {
  const compact = expression.replace(/\s+/g, '')
  const repeatMatch = compact.match(/^(?:(\d+)#)?(.+)$/i)
  if (!repeatMatch) return []

  const repetitions = Number.parseInt(repeatMatch[1] || '1', 10)
  if (!Number.isSafeInteger(repetitions) || repetitions < 1 || repetitions > 20) return []

  const dice: AnimatedDie[] = []
  const termPattern = /([+-]?)(\d*)d(\d+)/gi
  let match: RegExpExecArray | null

  while ((match = termPattern.exec(repeatMatch[2] ?? '')) !== null) {
    const count = Number.parseInt(match[2] || '1', 10)
    const faces = Number.parseInt(match[3] ?? '', 10)
    if (!Number.isSafeInteger(count) || count < 1 || !Number.isSafeInteger(faces) || faces < 2) continue

    const total = Math.min(count * repetitions, 100 - dice.length)
    for (let index = 0; index < total; index += 1) {
      dice.push({ id: `${match.index}-${faces}-${index}`, faces })
    }
  }

  return dice
}
