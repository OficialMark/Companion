<script setup lang="ts">
import type { DiceDamageBreakdown } from '~/types/campaign'

defineProps<{ damage: DiceDamageBreakdown }>()
</script>

<template>
  <ul class="damage-subtotals" aria-label="Dano por tipo">
    <li v-if="damage.baseType"><strong>{{ damage.baseSubtotal }}</strong> ({{ damage.baseType }})</li>
    <li v-for="(part, index) in damage.components" :key="index">
      <strong>{{ part.subtotal }}</strong> ({{ part.label }} · {{ part.damageType }})
    </li>
  </ul>
</template>

<style scoped>
.damage-subtotals {
  display: flex;
  flex-wrap: wrap;
  gap: 2px 12px;
  margin: 0;
  padding: 0;
  list-style: none;
  color: var(--muted);
  font-size: .75rem;
  overflow-wrap: anywhere;
}
.damage-subtotals strong { color: var(--gold-soft); font-variant-numeric: tabular-nums; }
</style>
