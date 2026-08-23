<script setup lang="ts">
const store = useCampaignStore()
const api = useCompanionApi()
const expression = ref('d20')
const busy = ref(false)
const error = ref('')
const presets = ['d20', '2d20', 'd12', 'd10', 'd8', 'd6', 'd4']

async function roll(value = expression.value) {
  if (!store.session || store.connection === 'offline') return
  busy.value = true
  error.value = ''
  expression.value = value
  try {
    const result = await api.rollDice(store.session, value)
    if (store.snapshot && !store.snapshot.diceRolls.some(item => item.id === result.id)) {
      store.snapshot.diceRolls.unshift(result)
      store.snapshot.diceRolls = store.snapshot.diceRolls.slice(0, 50)
    }
  } catch (exception) {
    error.value = (exception as Error).message
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <section class="panel dice-panel" aria-labelledby="dice-title">
    <div class="panel-heading">
      <div>
        <p class="eyebrow">Tente a sorte</p>
        <h2 id="dice-title">Rolar dados</h2>
      </div>
    </div>
    <div class="dice-presets" aria-label="Dados rápidos">
      <button v-for="preset in presets" :key="preset" class="die-button" type="button" :disabled="busy || store.connection === 'offline'" @click="roll(preset)">
        {{ preset.toUpperCase() }}
      </button>
    </div>
    <form class="roll-form" @submit.prevent="roll()">
      <label class="sr-only" for="dice-expression">Expressão de dados</label>
      <input id="dice-expression" v-model="expression" maxlength="128" spellcheck="false" placeholder="2d6+3">
      <button class="button primary" type="submit" :disabled="busy || store.connection === 'offline'">
        {{ busy ? 'Rolando…' : 'Rolar' }}
      </button>
    </form>
    <p class="field-hint">Exemplos: d20, 3#3d5+4, 2d3+4+2d8+3</p>
    <p v-if="error" class="form-error" role="alert">{{ error }}</p>
  </section>
</template>
