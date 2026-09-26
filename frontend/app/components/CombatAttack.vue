<script setup lang="ts">
import { useAttackResolution } from '~/composables/useAttackResolution'
const store = useCampaignStore(); const open = ref(false)
const { encounter, canAttack, roll, selected, busy, error, message, rolled, cancel: cancelAttack, confirm } = useAttackResolution()
function cancel() { open.value = false; cancelAttack() }
async function confirmAttack() { const result = await confirm(); if (result) open.value = false }
</script>
<template>
  <section v-if="canAttack" class="panel attack-panel" aria-labelledby="attack-title">
    <div class="panel-heading"><div><p class="eyebrow">Ação livre</p><h2 id="attack-title">Ataque</h2></div><button v-if="!open" class="button primary attack-open" :disabled="store.connection === 'offline'" type="button" @click="open = true">Atacar</button></div>
    <template v-if="open">
      <DiceRoller v-if="!roll" mode="attack" @rolled="rolled" />
      <div v-else class="attack-targets"><p>Resultado: <strong>{{ roll.total }}</strong>. Selecione os alvos:</p><label v-for="combatant in encounter?.combatants" :key="combatant.id" class="attack-target"><input v-model="selected" type="checkbox" :value="combatant.id">{{ combatant.name }}</label><div class="button-row"><button class="button primary attack-confirm" :disabled="busy || selected.length === 0" type="button" @click="confirmAttack">{{ busy ? 'Aplicando…' : 'Confirmar dano' }}</button><button class="button ghost attack-cancel" :disabled="busy" type="button" @click="cancel">Cancelar</button></div></div>
      <button v-if="!roll" class="button ghost attack-cancel" type="button" @click="cancel">Cancelar</button>
    </template>
    <p v-if="message" class="form-success" role="status">{{ message }}</p><p v-if="error" class="form-error" role="alert">{{ error }}</p>
  </section>
</template>
