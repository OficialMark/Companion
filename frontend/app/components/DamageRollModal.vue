<script setup lang="ts">
import { useAttackResolution } from '~/composables/useAttackResolution'
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
        <h2>Dano: {{ roll.total }}</h2>
        <p>Selecione quem recebe o dano:</p>
        <label v-for="combatant in encounter?.combatants" :key="combatant.id" class="attack-target">
          <input v-model="selected" type="checkbox" :value="combatant.id">{{ combatant.name }}
        </label>
        <div class="button-row">
          <button class="button primary attack-confirm" :disabled="busy || selected.length === 0" type="button" @click="confirmDamage">{{ busy ? 'Aplicando…' : 'Confirmar dano' }}</button>
          <button class="button ghost attack-cancel" :disabled="busy" type="button" @click="close">Cancelar</button>
        </div>
      </div>
      <button v-if="!roll" class="button ghost attack-cancel" type="button" @click="close">Cancelar</button>
      <p v-if="error" class="form-error" role="alert">{{ error }}</p>
    </div>
  </div>
</template>
