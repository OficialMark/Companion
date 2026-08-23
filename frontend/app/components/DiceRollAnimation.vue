<script setup lang="ts">
import type { AnimatedDie } from '~/utils/diceAnimation'

const props = defineProps<{ dice: AnimatedDie[]; settled: boolean; totals: number[] }>()
const tick = ref(0)
let timer: ReturnType<typeof setInterval> | undefined

function visibleValue(die: AnimatedDie, index: number) {
  if (props.settled && die.value !== undefined) return die.value
  return ((tick.value * 7 + index * 11) % die.faces) + 1
}

onMounted(() => { timer = setInterval(() => { tick.value += 1 }, 75) })
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
          <div class="poly-die" :class="`poly-die-d${die.faces}`">
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
