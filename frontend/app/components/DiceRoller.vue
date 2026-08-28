<script setup lang="ts">
import { useId } from 'vue'
import type { AnimatedDie } from '~/utils/diceAnimation'
import type { DiceRoll } from '~/types/campaign'
import { diceForAnimation, diceWithResults } from '~/utils/diceAnimation'
const props = withDefaults(defineProps<{ mode?: 'public' | 'attack' }>(), { mode: 'public' })
const emit = defineEmits<{ rolled: [roll: DiceRoll] }>()
const componentId = useId(); const titleId = `dice-title-`; const expressionId = `dice-expression-`
const store = useCampaignStore(); const api = useCompanionApi(); const expression = ref('d20'); const busy = ref(false); const error = ref(''); const rollingDice = ref<AnimatedDie[]>([]); const settled = ref(false); const rollingTotals = ref<number[]>([])
const presets = ['d20', '2d20', 'd12', 'd10', 'd8', 'd6', 'd4']; const rollingDuration = 750; const settlingDuration = 250; const resultHoldDuration = 1000
function wait(duration: number) { return new Promise<void>(resolve => window.setTimeout(resolve, duration)) }
async function roll(value = expression.value) {
  if (!store.session || store.connection === 'offline') return
  busy.value = true; error.value = ''; expression.value = value
  try {
    rollingDice.value = diceForAnimation(value); settled.value = false; rollingTotals.value = []
    if (rollingDice.value.length === 0) {
      const result = await api.rollDice(store.session, value)
      if (store.snapshot && !store.snapshot.diceRolls.some(item => item.id === result.id)) { store.snapshot.diceRolls.unshift(result); store.snapshot.diceRolls = store.snapshot.diceRolls.slice(0, 50) }
      emit('rolled', result); return
    }
    const startedAt = performance.now(); const result = await api.rollDice(store.session, value, false)
    rollingDice.value = diceWithResults(rollingDice.value, result.results); rollingTotals.value = result.results.map(item => item.total)
    await wait(Math.max(0, rollingDuration - (performance.now() - startedAt))); settled.value = true; await wait(settlingDuration)
    const revealed = await api.revealDiceRoll(store.session, result.id); await wait(resultHoldDuration)
    if (store.snapshot && !store.snapshot.diceRolls.some(item => item.id === revealed.id)) { store.snapshot.diceRolls.unshift(revealed); store.snapshot.diceRolls = store.snapshot.diceRolls.slice(0, 50) }
    emit('rolled', revealed)
  } catch (exception) { error.value = (exception as Error).message }
  finally { rollingDice.value = []; settled.value = false; rollingTotals.value = []; busy.value = false }
}
</script>
<template>
  <section class="panel dice-panel" :aria-labelledby="titleId">
    <div class="panel-heading"><div><p class="eyebrow">{{ props.mode === 'attack' ? 'Dano' : 'Tente a sorte' }}</p><h2 :id="titleId">{{ props.mode === 'attack' ? 'Rolar ataque' : 'Rolar dados' }}</h2></div></div>
    <div class="dice-presets" aria-label="Dados rápidos"><button v-for="preset in presets" :key="preset" class="die-button" type="button" :disabled="busy || store.connection === 'offline'" @click="roll(preset)">{{ preset.toUpperCase() }}</button></div>
    <DiceRollAnimation v-if="rollingDice.length" :dice="rollingDice" :settled="settled" :totals="rollingTotals" />
    <form class="roll-form" @submit.prevent="roll()"><label class="sr-only" :for="expressionId">Expressão de dados</label><input :id="expressionId" v-model="expression" maxlength="128" spellcheck="false" placeholder="2d6+3"><button class="button primary" type="submit" :disabled="busy || store.connection === 'offline'">{{ busy ? 'Rolando…' : 'Rolar' }}</button></form>
    <p class="field-hint">{{ props.mode === 'attack' ? 'Use uma expressão com um único resultado, como 2d6+3.' : 'Exemplos: d20, 3#3d5+4, 2d3+4+2d8+3' }}</p><p v-if="error" class="form-error" role="alert">{{ error }}</p>
  </section>
</template>