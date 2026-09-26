<script setup lang="ts">
import type { AnimatedDie } from '~/utils/diceAnimation'
import { hasStandardShape } from '~/utils/diceAnimation'

const props = defineProps<{ dice: AnimatedDie[]; settled: boolean; totals: number[] }>()
// Purely cosmetic spin shown before the server-authoritative result settles
// (`die.value`) — randomized per tick so the animation doesn't cycle through
// a fixed, predictable sequence of faces.
const spinFaces = ref<Record<string, number>>({})
let timer: ReturnType<typeof setInterval> | undefined

function randomizeSpin() {
  const next: Record<string, number> = {}
  for (const die of props.dice) next[die.id] = Math.floor(Math.random() * die.faces) + 1
  spinFaces.value = next
}

function visibleValue(die: AnimatedDie, index: number) {
  if (props.settled && die.value !== undefined) return die.value
  return spinFaces.value[die.id] ?? ((index % die.faces) + 1)
}

watch(() => props.dice, randomizeSpin, { immediate: true })
onMounted(() => { timer = setInterval(randomizeSpin, 75) })
onBeforeUnmount(() => { if (timer) clearInterval(timer) })
</script>

<template>
  <div class="dice-animation" role="status" aria-live="polite" aria-label="Dados rolando">
    <div class="dice-stage" aria-hidden="true">
      <div
        v-for="(die, index) in dice"
        :key="die.id"
        class="die-scene"
        :class="{ 'is-settled': settled }"
        :style="{ '--die-index': index, '--die-x': `${((index * 37) % 7 - 3) * 12}px` }"
        :data-die="`d${die.faces}`"
      >
        <div class="die-flight">
          <div class="poly-die" :class="hasStandardShape(die.faces) ? `poly-die-d${die.faces}` : 'poly-die-generic'">
            <span class="die-face die-face-top">{{ visibleValue(die, index) }}</span>
            <span class="die-face die-face-left">{{ ((visibleValue(die, index) + index) % die.faces) + 1 }}</span>
            <span class="die-face die-face-right">{{ ((visibleValue(die, index) + index + 1) % die.faces) + 1 }}</span>
          </div>
        </div>
      </div>
    </div>
    <div v-if="settled && totals.length" class="dice-total" aria-live="polite">
      <template v-if="totals.length === 1">
        <span>Total</span><strong>{{ totals[0] }}</strong>
      </template>
      <template v-else>
        <span>Totais</span>
        <strong v-for="(total, index) in totals" :key="index">#{{ index + 1 }} {{ total }}</strong>
      </template>
    </div>
    <p>{{ settled ? 'Resultado definido' : `Rolando ${dice.length} ${dice.length === 1 ? 'dado' : 'dados'}…` }}</p>
  </div>
</template>
