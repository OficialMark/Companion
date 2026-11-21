<script setup lang="ts">
import type { DiceModifier, DiceRoll, DiceRollResult } from '~/types/campaign'
defineProps<{ rolls: DiceRoll[] }>()

/** "(FOR) 3", "- (FOR) 1", "+2" (spec 011). */
function modifierText(modifier: DiceModifier) {
  const sign = modifier.value < 0 ? '-' : '+'
  return modifier.label ? `${sign} (${modifier.label}) ${Math.abs(modifier.value)}` : `${sign}${Math.abs(modifier.value)}`
}

function selectionTitle(result: DiceRollResult) {
  return result.selection === 'menor' ? 'Desvantagem: rolou tudo duas vezes e ficou o menor' : 'Vantagem: rolou tudo duas vezes e ficou o maior'
}

function time(value: string) {
  return new Intl.DateTimeFormat('pt-BR', { hour: '2-digit', minute: '2-digit' }).format(new Date(value))
}
</script>

<template>
  <section class="panel history-panel" aria-labelledby="history-title">
    <div class="panel-heading">
      <div>
        <p class="eyebrow"><Icon name="lucide:history" />Resultados públicos</p>
        <h2 id="history-title">Histórico</h2>
      </div>
      <span class="count-pill" :title="`${rolls.length} rolagens`">{{ rolls.length }}</span>
    </div>
    <div v-if="rolls.length === 0" class="empty-state">
      <span class="state-icon" aria-hidden="true"><Icon name="lucide:dices" size="1.6rem" /></span>
      <p>A primeira rolagem ainda está esperando.</p>
    </div>
    <ol v-else class="roll-list" aria-live="polite">
      <li v-for="roll in rolls" :key="roll.id">
        <div class="roll-meta">
          <strong>{{ roll.displayName }}<span v-if="roll.label" class="roll-label" :title="`Rolagem para ${roll.label}`"><Icon name="lucide:skull" />{{ roll.label }}</span></strong>
          <span><Icon name="lucide:clock" />{{ time(roll.createdAt) }}</span>
        </div>
        <div class="roll-command">{{ roll.expression }}</div>
        <div class="roll-attempts">
          <div v-for="result in roll.results" :key="result.sequence" class="roll-attempt">
            <span class="attempt-label">{{ roll.results.length > 1 ? `#${result.sequence}` : 'Resultado' }}</span>
            <span class="values">
              <template v-if="result.executions?.length">
                <span class="selection-label" :title="selectionTitle(result)">{{ result.selection }}</span>
                <span
                  v-for="(execution, executionIndex) in result.executions"
                  :key="`${result.sequence}-execution-${executionIndex}`"
                  class="execution-result"
                  :class="{ selected: executionIndex === (result.selectedExecution ?? 0) }"
                  :data-selected-execution="executionIndex === (result.selectedExecution ?? 0) ? 'true' : undefined"
                >
                  <span v-for="(term, termIndex) in execution.terms" :key="`${result.sequence}-${executionIndex}-${termIndex}`" class="term-result">
                    {{ term.expression }} [{{ term.values.join(', ') }}]
                  </span>
                  <strong class="execution-total">= {{ execution.total }}</strong>
                </span>
              </template>
              <template v-else>
                <span v-for="(term, termIndex) in result.terms" :key="`${result.sequence}-${termIndex}`" class="term-result">
                  {{ term.expression }} [{{ term.values.join(', ') }}]
                </span>
              </template>
              <template v-if="result.modifiers?.length">
                <span v-for="(modifier, modifierIndex) in result.modifiers" :key="`${result.sequence}-modifier-${modifierIndex}`" class="modifier-result">{{ modifierText(modifier) }}</span>
              </template>
              <span v-else-if="result.modifier" class="modifier-result">{{ result.modifier > 0 ? '+' : '' }}{{ result.modifier }}</span>
            </span>
            <strong class="total">{{ result.total }}</strong>
            <ul v-if="result.damageBreakdown" class="history-damage-breakdown" aria-label="Dano por tipo">
              <li v-if="result.damageBreakdown.baseType"><span>Dano-base · {{ result.damageBreakdown.baseType }}</span><strong>{{ result.damageBreakdown.baseSubtotal }}</strong></li>
              <li v-for="component in result.damageBreakdown.components" :key="`${component.label}-${component.damageType}`"><span>{{ component.label }} · {{ component.damageType }}</span><strong>{{ component.subtotal }}</strong></li>
            </ul>
          </div>
        </div>
      </li>
    </ol>
  </section>
</template>

<style scoped>
.selection-label { color: var(--ink-soft); font-size: .68rem; font-weight: 700; text-transform: uppercase; letter-spacing: .06em; }
.execution-result { display: inline-flex; flex-wrap: wrap; align-items: baseline; gap: 4px 6px; border-radius: 7px; padding: 1px 6px; opacity: .55; }
.execution-result.selected { opacity: 1; outline: 1px solid var(--ink-soft); }
.execution-total { font-weight: 700; }
.history-damage-breakdown { grid-column: 1 / -1; display: grid; gap: 4px; margin: 4px 0 0; padding: 0; list-style: none; }
.history-damage-breakdown li { display: flex; justify-content: space-between; gap: 10px; color: var(--muted); font-size: .75rem; }
.history-damage-breakdown strong { color: var(--gold-soft); font-variant-numeric: tabular-nums; }
</style>
