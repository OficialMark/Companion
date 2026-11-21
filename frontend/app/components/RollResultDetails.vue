<script setup lang="ts">
import type { DiceRoll } from '~/types/campaign'
import DamageSubtotals from './DamageSubtotals.vue'

defineProps<{ roll: DiceRoll; totalInHeading?: boolean }>()
</script>

<template>
  <div class="roll-result-details" aria-label="Composição da rolagem">
    <div v-for="result in roll.results" :key="result.sequence">
      <p class="roll-result-sum">
        <strong v-if="roll.results.length > 1">#{{ result.sequence }}</strong>
        <span v-for="(term, index) in result.terms" :key="index">
          {{ term.expression.startsWith('-') ? '− ' : index ? '+ ' : '' }}{{ term.expression.replace(/^-/, '') }} [{{ term.values.join(', ') }}]
        </span>
        <template v-if="result.modifiers?.length">
          <span v-for="(modifier, index) in result.modifiers" :key="index">
            {{ modifier.value < 0 ? '−' : '+' }} {{ Math.abs(modifier.value) }}<template v-if="modifier.label"> ({{ modifier.label }})</template>
          </span>
        </template>
        <span v-else-if="result.modifier">{{ result.modifier < 0 ? '−' : '+' }} {{ Math.abs(result.modifier) }}</span>
        <strong v-if="!totalInHeading || roll.results.length > 1">= {{ result.total }}</strong>
      </p>
      <DamageSubtotals v-if="result.damageBreakdown" :damage="result.damageBreakdown" />
    </div>
  </div>
</template>

<style scoped>
.roll-result-details { display: grid; gap: 6px; color: var(--ink-soft); font-size: .8rem; line-height: 1.4; }
.roll-result-sum { display: flex; flex-wrap: wrap; gap: 2px 7px; margin: 0; overflow-wrap: anywhere; }
.roll-result-sum + .damage-subtotals { margin-top: 3px; }
.roll-result-sum strong { color: var(--gold); }
</style>
