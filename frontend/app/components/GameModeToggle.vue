<script setup lang="ts">
import { useGameMode } from '~/composables/useGameMode'
const gameMode = useGameMode()
</script>
<template>
  <button
    class="button ghost game-mode-toggle"
    type="button"
    :aria-pressed="gameMode.active.value"
    @click="gameMode.toggle()"
  >
    {{ gameMode.active.value ? 'Voltar à visão completa' : 'Modo Jogo' }}
  </button>
</template>
