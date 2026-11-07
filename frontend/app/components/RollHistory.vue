<script setup lang="ts">
import type { DiceRoll } from '~/types/campaign'
defineProps<{ rolls: DiceRoll[] }>()

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
          <strong>{{ roll.displayName }}</strong>
          <span><Icon name="lucide:clock" />{{ time(roll.createdAt) }}</span>
        </div>
        <div class="roll-command">{{ roll.expression }}</div>
        <div class="roll-attempts">
          <div v-for="result in roll.results" :key="result.sequence" class="roll-attempt">
            <span class="attempt-label">{{ roll.results.length > 1 ? `#${result.sequence}` : 'Resultado' }}</span>
            <span class="values">
              <span v-for="(term, termIndex) in result.terms" :key="`${result.sequence}-${termIndex}`" class="term-result">
                {{ term.expression }} [{{ term.values.join(', ') }}]
              </span>
              <span v-if="result.modifier" class="modifier-result">{{ result.modifier > 0 ? '+' : '' }}{{ result.modifier }}</span>
            </span>
            <strong class="total">{{ result.total }}</strong>
          </div>
        </div>
      </li>
    </ol>
  </section>
</template>
