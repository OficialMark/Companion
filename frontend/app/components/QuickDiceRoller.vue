<script setup lang="ts">
import type { AnimatedDie } from '~/utils/diceAnimation'
import type { DiceRoll } from '~/types/campaign'
import { diceForAnimation, diceWithResults } from '~/utils/diceAnimation'
import { useQuickRollModifier, type QuickDieFaces } from '~/composables/useQuickRollModifier'

const emit = defineEmits<{ rolled: [roll: DiceRoll]; 'rolled-d20': [roll: DiceRoll] }>()
const store = useCampaignStore(); const api = useCompanionApi()
const modifier = useQuickRollModifier()
const busy = ref(false); const error = ref(''); const rollingDice = ref<AnimatedDie[]>([]); const settled = ref(false); const rollingTotals = ref<number[]>([])
const dice: QuickDieFaces[] = [20, 8, 6]
const rollingDuration = 750; const settlingDuration = 250; const resultHoldDuration = 1000
function wait(duration: number) { return new Promise<void>(resolve => window.setTimeout(resolve, duration)) }

async function roll(faces: QuickDieFaces) {
  if (!store.session || store.connection === 'offline' || busy.value) return
  const expression = modifier.expressionFor(faces)
  busy.value = true; error.value = ''
  try {
    rollingDice.value = diceForAnimation(expression); settled.value = false; rollingTotals.value = []
    const startedAt = performance.now(); const result = await api.rollDice(store.session, expression, false)
    rollingDice.value = diceWithResults(rollingDice.value, result.results); rollingTotals.value = result.results.map(item => item.total)
    await wait(Math.max(0, rollingDuration - (performance.now() - startedAt))); settled.value = true; await wait(settlingDuration)
    const revealed = await api.revealDiceRoll(store.session, result.id); await wait(resultHoldDuration)
    if (store.snapshot && !store.snapshot.diceRolls.some(item => item.id === revealed.id)) { store.snapshot.diceRolls.unshift(revealed); store.snapshot.diceRolls = store.snapshot.diceRolls.slice(0, 50) }
    emit('rolled', revealed)
    if (faces === 20) emit('rolled-d20', revealed)
  } catch (exception) { error.value = (exception as Error).message }
  finally { rollingDice.value = []; settled.value = false; rollingTotals.value = []; busy.value = false }
}
</script>
<template>
  <section class="panel dice-panel" aria-labelledby="quick-dice-title">
    <div class="panel-heading"><div><p class="eyebrow">Modo Jogo</p><h2 id="quick-dice-title">Rolagem rápida</h2></div></div>
    <div class="quick-dice-buttons" aria-label="Dados rápidos">
      <button v-for="faces in dice" :key="faces" class="die-button" type="button" :disabled="busy || store.connection === 'offline'" @click="roll(faces)">D{{ faces }}</button>
    </div>
    <div class="quick-dice-modifier" aria-label="Modificador">
      <button class="button ghost" type="button" :disabled="busy" aria-label="Diminuir modificador" @click="modifier.decrement()">-</button>
      <span class="quick-dice-modifier-value">{{ modifier.value.value > 0 ? `+${modifier.value.value}` : modifier.value.value }}</span>
      <button class="button ghost" type="button" :disabled="busy" aria-label="Aumentar modificador" @click="modifier.increment()">+</button>
    </div>
    <DiceRollAnimation v-if="rollingDice.length" :dice="rollingDice" :settled="settled" :totals="rollingTotals" />
    <p v-if="error" class="form-error" role="alert">{{ error }}</p>
  </section>
</template>
