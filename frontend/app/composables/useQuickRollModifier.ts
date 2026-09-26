export type QuickDieFaces = 20 | 8 | 6

const value = ref(0)

export function useQuickRollModifier() {
  function increment() { value.value += 1 }
  function decrement() { value.value -= 1 }
  function expressionFor(faces: QuickDieFaces) {
    if (value.value > 0) return `1d${faces}+${value.value}`
    if (value.value < 0) return `1d${faces}${value.value}`
    return `1d${faces}`
  }
  return { value, increment, decrement, expressionFor }
}
