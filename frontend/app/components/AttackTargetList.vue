<script setup lang="ts">
import type { Combatant } from '~/types/campaign'
defineProps<{ combatants: Combatant[] }>()
const selected = defineModel<string[]>({ required: true })
</script>
<template>
  <div class="attack-target-list">
    <label
      v-for="combatant in combatants"
      :key="combatant.id"
      class="attack-target"
      :class="{ checked: selected.includes(combatant.id), defeated: combatant.isDefeated }"
    >
      <input v-model="selected" type="checkbox" :value="combatant.id">
      <span class="attack-target-name">{{ combatant.name }}</span>
      <span v-if="combatant.isDefeated" class="status-chip danger" title="Derrubado (0 de vida)"><Icon name="lucide:skull" />Caído</span>
      <span v-else-if="combatant.currentHitPoints !== null" class="hp-chip" :title="`${combatant.currentHitPoints} de vida`"><Icon name="lucide:heart" />{{ combatant.currentHitPoints }}</span>
      <Icon name="lucide:crosshair" class="attack-target-mark" />
    </label>
  </div>
</template>
