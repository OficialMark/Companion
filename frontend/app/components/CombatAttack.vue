<script setup lang="ts">
import { useAttackResolution } from '~/composables/useAttackResolution'
import AttackTargetList from './AttackTargetList.vue'
const store = useCampaignStore(); const open = ref(false)
const { encounter, canAttack, roll, selected, busy, error, message, rolled, cancel: cancelAttack, confirm } = useAttackResolution()
function cancel() { open.value = false; cancelAttack() }
async function confirmAttack() { const result = await confirm(); if (result) open.value = false }
</script>
<template>
  <section v-if="canAttack" class="panel attack-panel" aria-labelledby="attack-title">
    <div class="panel-heading">
      <div><p class="eyebrow"><Icon name="lucide:sword" />Ação livre</p><h2 id="attack-title">Ataque</h2></div>
      <button v-if="!open" class="button primary attack-open" :disabled="store.connection === 'offline'" type="button" @click="open = true"><Icon name="lucide:swords" />Atacar</button>
    </div>
    <template v-if="open">
      <DiceRoller v-if="!roll" mode="attack" @rolled="rolled" />
      <div v-else class="attack-targets">
        <p class="attack-result"><Icon name="lucide:flame" />Resultado: <strong>{{ roll.total }}</strong>. Selecione os alvos:</p>
        <AttackTargetList v-model="selected" :combatants="encounter?.combatants ?? []" />
        <div class="button-row">
          <button class="button primary attack-confirm" :disabled="busy || selected.length === 0" type="button" @click="confirmAttack"><Icon :name="busy ? 'lucide:loader-circle' : 'lucide:swords'" :class="{ spin: busy }" />{{ busy ? 'Aplicando…' : 'Confirmar dano' }}</button>
          <button class="button ghost attack-cancel" :disabled="busy" type="button" @click="cancel">Cancelar</button>
        </div>
      </div>
      <button v-if="!roll" class="button ghost attack-cancel" type="button" @click="cancel">Cancelar</button>
    </template>
    <p v-if="message" class="form-success" role="status"><Icon name="lucide:circle-check" />{{ message }}</p>
    <p v-if="error" class="form-error" role="alert"><Icon name="lucide:circle-alert" />{{ error }}</p>
  </section>
</template>
