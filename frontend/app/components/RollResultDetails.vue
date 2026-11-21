<script setup lang="ts">
import type { DiceRoll } from '~/types/campaign'

defineProps<{ roll: DiceRoll }>()
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
        <strong>= {{ result.total }}</strong>
      </p>
      <ul v-if="result.damageBreakdown" class="damage-breakdown" aria-label="Detalhamento do dano por tipo">
        <li v-if="result.damageBreakdown.baseType">
          <span>Dano-base · {{ result.damageBreakdown.baseType }}</span><strong>{{ result.damageBreakdown.baseSubtotal }}</strong>
        </li>
        <li v-for="(part, index) in result.damageBreakdown.components" :key="index">
          <span>{{ part.label }} · {{ part.damageType }}</span><strong>{{ part.subtotal }}</strong>
        </li>
      </ul>
    </div>
  </div>
</template>

<style scoped>
.roll-result-details { display: grid; gap: 6px; color: var(--ink-soft); font-size: .84rem; }
.roll-result-sum { display: flex; flex-wrap: wrap; gap: 3px 8px; margin: 0 0 6px; overflow-wrap: anywhere; }
.roll-result-sum strong { color: var(--gold); }
</style>
