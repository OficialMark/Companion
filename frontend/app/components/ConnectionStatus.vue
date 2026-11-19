<script setup lang="ts">
const props = defineProps<{ status: 'offline' | 'connecting' | 'online' }>()
const label = computed(() => props.status === 'online' ? 'Conectado' : props.status === 'connecting' ? 'Reconectando…' : 'Sem conexão')
const icon = computed(() => props.status === 'online' ? 'lucide:wifi' : props.status === 'connecting' ? 'lucide:loader-circle' : 'lucide:wifi-off')
</script>

<template>
  <div class="connection" :class="status" role="status" aria-live="polite" :title="label">
    <span class="connection-icon" aria-hidden="true"><Icon :name="icon" :class="{ spin: status === 'connecting' }" /></span>
    <span class="connection-label">{{ label }}</span>
  </div>
</template>
