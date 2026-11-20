<script setup lang="ts">
/**
 * The master's reserved code in the top bar (spec 012, FR-009). Hidden by default; the eye shows and
 * hides it. Only the master has the code in the session, so players never see this control.
 */
const store = useCampaignStore()
const visible = ref(false)
const code = computed(() => (store.isMaster ? store.session?.masterCode ?? null : null))
const masked = computed(() => '•'.repeat(Math.max(6, code.value?.length ?? 6)))
</script>

<template>
  <div v-if="code" class="master-code-toggle" data-master-code>
    <Icon name="lucide:key-round" aria-hidden="true" />
    <span class="master-code-label">Código do mestre</span>
    <code class="master-code-value" :aria-label="visible ? `Código do mestre ${code}` : 'Código do mestre oculto'" data-master-code-value>{{ visible ? code : masked }}</code>
    <button
      class="icon-button master-code-eye"
      type="button"
      :aria-pressed="visible"
      :aria-label="visible ? 'Ocultar código do mestre' : 'Mostrar código do mestre'"
      :title="visible ? 'Ocultar código do mestre' : 'Mostrar código do mestre'"
      data-master-code-eye
      @click="visible = !visible"
    >
      <Icon :name="visible ? 'lucide:eye-off' : 'lucide:eye'" />
    </button>
  </div>
</template>
