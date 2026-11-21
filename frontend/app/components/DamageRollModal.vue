<script setup lang="ts">
import { useAttackResolution } from '~/composables/useAttackResolution'
import AttackTargetList from './AttackTargetList.vue'
import type { DamageBreakdownPlan } from '~/utils/playerRoll'
import type { DiceRoll } from '~/types/campaign'
/** Optional damage expression to start with, e.g. the weapon of the player's attack (spec 011, US4). */
const props = defineProps<{ expression?: string; damageSummary?: string | null; damagePlan?: DamageBreakdownPlan | null }>()
const emit = defineEmits<{ resolved: []; cancelled: [] }>()
const { encounter, roll, selected, busy, error, rolled, cancel, confirm } = useAttackResolution()
const breakdown = ref<Array<{ label: string; damageType: string; subtotal: number }> | null>(null)
function handleRolled(result: DiceRoll) {
  const actual = result.results[0]?.damageBreakdown
  breakdown.value = actual ? [
    ...(actual.baseType ? [{ label: 'Dano-base', damageType: actual.baseType, subtotal: actual.baseSubtotal ?? 0 }] : []),
    ...actual.components
  ] : null
  rolled(result)
}
function close() { cancel(); emit('cancelled') }
async function confirmDamage() { const result = await confirm(); if (result) emit('resolved') }
function handleKeydown(event: KeyboardEvent) { if (event.key === 'Escape') close() }
onMounted(() => { window.addEventListener('keydown', handleKeydown) })
onBeforeUnmount(() => { window.removeEventListener('keydown', handleKeydown) })
</script>
<template>
  <div class="modal-overlay" role="alertdialog" aria-modal="true" aria-label="Rolar dano">
    <div class="modal-panel damage-roll-modal">
      <p v-if="!roll && props.damageSummary" class="damage-type-summary">{{ props.damageSummary }}</p>
      <DiceRoller v-if="!roll" mode="attack" :initial-expression="props.expression" :locked="Boolean(props.expression)" :damage-breakdown="props.damagePlan" @rolled="handleRolled" />
      <div v-else class="attack-targets">
        <h2 class="damage-heading"><Icon name="lucide:flame" />Dano: {{ roll.total }}</h2>
        <ul v-if="breakdown?.length" class="damage-breakdown" aria-label="Detalhamento do dano por tipo">
          <li v-for="part in breakdown" :key="`${part.label}-${part.damageType}`"><span>{{ part.label }} · {{ part.damageType }}</span><strong>{{ part.subtotal }}</strong></li>
        </ul>
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
