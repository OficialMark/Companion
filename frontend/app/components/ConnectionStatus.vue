<script setup lang="ts">
defineProps<{ status: 'offline' | 'connecting' | 'online' }>()
</script>

<template>
  <div class="connection" :class="status" role="status" aria-live="polite">
    <span class="connection-dot" aria-hidden="true" />
    <span>{{ status === 'online' ? 'Conectado' : status === 'connecting' ? 'Reconectando…' : 'Sem conexão' }}</span>
  </div>
</template>
