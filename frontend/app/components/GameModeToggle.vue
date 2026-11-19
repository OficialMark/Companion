<script setup lang="ts">
import { useGameMode } from '~/composables/useGameMode'
const gameMode = useGameMode()
</script>
<template>
  <button
    class="button ghost game-mode-toggle"
    :class="{ active: gameMode.active.value }"
    type="button"
    :aria-pressed="gameMode.active.value"
    :title="gameMode.active.value ? 'Voltar à visão completa' : 'Ativar Modo Jogo (interface enxuta para o combate)'"
    @click="gameMode.toggle()"
  >
    <Icon :name="gameMode.active.value ? 'lucide:layout-dashboard' : 'lucide:swords'" />
    <span class="toggle-label">{{ gameMode.active.value ? 'Voltar à visão completa' : 'Modo Jogo' }}</span>
  </button>
</template>
