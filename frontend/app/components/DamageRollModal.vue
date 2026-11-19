<script setup lang="ts">
import { useAttackResolution } from '~/composables/useAttackResolution'
import AttackTargetList from './AttackTargetList.vue'
const emit = defineEmits<{ resolved: []; cancelled: [] }>()
const { encounter, roll, selected, busy, error, rolled, cancel, confirm } = useAttackResolution()
function close() { cancel(); emit('cancelled') }
async function confirmDamage() { const result = await confirm(); if (result) emit('resolved') }
function handleKeydown(event: KeyboardEvent) { if (event.key === 'Escape') close() }
onMounted(() => { window.addEventListener('keydown', handleKeydown) })
onBeforeUnmount(() => { window.removeEventListener('keydown', handleKeydown) })
</script>
<template>
  <div class="modal-overlay" role="alertdialog" aria-modal="true" aria-label="Rolar dano">
    <div class="modal-panel damage-roll-modal">
      <DiceRoller v-if="!roll" mode="attack" @rolled="rolled" />
      <div v-else class="attack-targets">
        <h2 class="damage-heading"><Icon name="lucide:flame" />Dano: {{ roll.total }}</h2>
        <p>Selecione quem recebe o dano:</p>
        <AttackTargetList v-model="selected" :combatants="encounter?.combatants ?? []" />
        <div class="button-row">
          <button class="button primary attack-confirm" :disabled="busy || selected.length === 0" type="button" @click="confirmDamage"><Icon :name="busy ? 'lucide:loader-circle' : 'lucide:swords'" :class="{ spin: busy }" />{{ busy ? 'Aplicando…' : 'Confirmar dano' }}</button>
          <button class="button ghost attack-cancel" :disabled="busy" type="button" @click="close">Cancelar</button>
        </div>
      </div>
      <button v-if="!roll" class="button ghost attack-cancel" type="button" @click="close">Cancelar</button>
      <p v-if="error" class="form-error" role="alert"><Icon name="lucide:circle-alert" />{{ error }}</p>
    </div>
  </div>
</template>
