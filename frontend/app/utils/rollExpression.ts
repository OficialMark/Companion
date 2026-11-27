/**
 * Gerador de expressões de dados (spec 011, contracts/dice-expression.md).
 *
 * O frontend só gera; quem executa é o servidor. A string gerada é exatamente o `Normalized` que o servidor
 * devolve para ela, então a tela mostra o que vai rodar. Casos compartilhados:
 * backend/tests/Fixtures/dice-expressions.cases.json (lidos por tests/roll-expression.spec.ts).
 *
 * Regras:
 * - Dados na ordem fixa d20, d12, d10, d8, d6, d4, d100, antes das constantes. Dado com quantidade 0 some.
 * - Partes com valor 0 somem. Rótulo vazio (depois de tirar espaços) vira constante sem rótulo.
 * - Uma rolagem só, sem rótulo e sem vantagem/desvantagem: forma legada compacta, igual ao servidor
 *   (`1d20+2d6+5`; as constantes viram um único modificador). Com repetição, sempre `N#[...]`.
 * - Com rótulo ou seleção: forma espaçada (`d20 + (FOR) 3`, `2#(d20 + 5) maior`, `3#[d20 + (FOR) 3]`),
 *   uma constante por parte, na ordem das partes.
 */

export type DieType = 'd4' | 'd6' | 'd8' | 'd10' | 'd12' | 'd20' | 'd100'
export type RollSelection = 'none' | 'advantage' | 'disadvantage'

export interface RollPart {
  label: string | null
  value: number
}

export interface RollExpressionInput {
  dice: Partial<Record<DieType, number>>
  additionalDice?: Array<Partial<Record<DieType, number>>>
  parts: RollPart[]
  selection: RollSelection
  repetitions: number
}

/** Ordem em que o gerador escreve os dados. O servidor preserva a ordem de entrada. */
export const DIE_ORDER: readonly DieType[] = ['d20', 'd12', 'd10', 'd8', 'd6', 'd4', 'd100']

export const MAX_DICE = 100
export const MAX_REPETITIONS = 20
export const MAX_MODIFIER = 10000
export const MAX_EXPRESSION_LENGTH = 256
export const MAX_LABEL_LENGTH = 16

const LABEL_PATTERN = /^[\p{L}0-9 +.']+$/u

/** Mesma normalização do servidor: NFC, espaços nas pontas removidos e espaços repetidos reduzidos a um. */
export function normalizeLabel(label: string | null | undefined): string | null {
  if (label == null) return null
  const normalized = label.normalize('NFC').split(/\s+/u).filter(Boolean).join(' ')
  return normalized.length === 0 ? null : normalized
}

interface PreparedInput {
  dice: Array<{ faces: number; count: number }>
  parts: Array<{ label: string | null; value: number }>
}

function prepare(input: RollExpressionInput): PreparedInput {
  const dice = [input.dice, ...(input.additionalDice ?? [])].flatMap(group =>
    DIE_ORDER.map(type => ({ faces: Number(type.slice(1)), count: group[type] ?? 0 }))
      .filter(die => die.count !== 0))
  const parts = input.parts
    .filter(part => part.value !== 0)
    .map(part => ({ label: normalizeLabel(part.label), value: part.value }))
  return { dice, parts }
}

function die(count: number, faces: number, compact: boolean) {
  return compact || count !== 1 ? `${count}d${faces}` : `d${faces}`
}

/** Gera a forma canônica. Devolve '' quando não há dado (o servidor exige pelo menos um). */
export function buildExpression(input: RollExpressionInput): string {
  const { dice, parts } = prepare(input)
  if (dice.length === 0) return ''
  const repetitions = input.repetitions

  // Repetition is always written with brackets (request 7.1: make the repeated part explicit), so the
  // compact legacy form is only used for a single roll with no labels and no selection.
  if (input.selection === 'none' && repetitions === 1 && parts.every(part => part.label === null)) {
    const modifier = parts.reduce((sum, part) => sum + part.value, 0)
    const terms = dice.map(({ count, faces }) => die(count, faces, true)).join('+')
    const suffix = modifier > 0 ? `+${modifier}` : modifier < 0 ? `${modifier}` : ''
    return `${repetitions > 1 ? `${repetitions}#` : ''}${terms}${suffix}`
  }

  let sum = dice.map(({ count, faces }) => die(count, faces, false)).join(' + ')
  for (const part of parts) {
    sum += part.value < 0 ? ' - ' : ' + '
    if (part.label !== null) sum += `(${part.label}) `
    sum += Math.abs(part.value)
  }
  const body = input.selection === 'none'
    ? sum
    : `2#(${sum}) ${input.selection === 'advantage' ? 'maior' : 'menor'}`
  return repetitions > 1 ? `${repetitions}#[${body}]` : body
}

/** Not a real error while the player is still choosing: the panels hide it and explain what is missing instead. */
export const NO_DICE_ERROR = 'Escolha pelo menos um dado.'

/**
 * Valida os limites do contrato antes de enviar. Devolve a mensagem em PT-BR ou null.
 * Os dados contam as duas execuções de maior/menor e todas as repetições.
 */
export function expressionLimits(input: RollExpressionInput): string | null {
  const { repetitions } = input
  if (!Number.isInteger(repetitions) || repetitions < 1 || repetitions > MAX_REPETITIONS) {
    return `Use de 1 a ${MAX_REPETITIONS} repetições.`
  }
  const { dice, parts } = prepare(input)
  if (dice.some(({ count }) => !Number.isInteger(count) || count < 0)) {
    return 'A quantidade de cada dado deve ser um número inteiro, zero ou mais.'
  }
  if (dice.length === 0) return NO_DICE_ERROR
  const executions = input.selection === 'none' ? 1 : 2
  const total = dice.reduce((sum, item) => sum + item.count, 0) * executions * repetitions
  if (total > MAX_DICE) {
    return executions === 2
      ? `Use no máximo ${MAX_DICE} dados no total; maior e menor rolam tudo duas vezes (${total} dados).`
      : `Use no máximo ${MAX_DICE} dados no total (${total} dados).`
  }
  if (parts.some(part => !Number.isInteger(part.value))) return 'Os modificadores devem ser números inteiros.'
  const modifier = parts.reduce((sum, part) => sum + part.value, 0)
  if (modifier < -MAX_MODIFIER || modifier > MAX_MODIFIER) {
    return `O modificador total deve ficar entre -${MAX_MODIFIER} e ${MAX_MODIFIER}.`
  }
  const badLabel = parts.find(part => part.label !== null &&
    (part.label.length > MAX_LABEL_LENGTH || !LABEL_PATTERN.test(part.label)))
  if (badLabel) {
    return `O rótulo "${badLabel.label}" deve ter até ${MAX_LABEL_LENGTH} caracteres: letras, números, espaço, +, . ou '.`
  }
  if (buildExpression(input).length > MAX_EXPRESSION_LENGTH) {
    return `A expressão passa de ${MAX_EXPRESSION_LENGTH} caracteres; use menos rótulos ou rótulos mais curtos.`
  }
  return null
}
