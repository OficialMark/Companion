<script setup lang="ts">
const props = withDefaults(defineProps<{
  name: string
  src?: string | null
  master?: boolean
  size?: number
}>(), { src: null, master: false, size: 38 })
const initial = computed(() => props.name.trim().slice(0, 1).toUpperCase() || '?')
</script>

<template>
  <span class="avatar" :class="{ master, 'has-photo': Boolean(src) }" :style="{ '--avatar-size': `${size}px` }" aria-hidden="true">
    <img v-if="src" :src="src" alt="" draggable="false">
    <template v-else>{{ initial }}</template>
  </span>
</template>
