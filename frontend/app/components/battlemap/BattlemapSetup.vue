<script setup lang="ts">
import type { BattlemapSnapshot } from '~/types/campaign'

const props = defineProps<{ encounterId: string; battlemap: BattlemapSnapshot | null }>()
const emit = defineEmits<{ configured: [] }>()
const store = useCampaignStore()
const api = useCompanionApi()
const columns = ref(props.battlemap?.columns ?? 20)
const rows = ref(props.battlemap?.rows ?? 15)
const isGridVisible = ref(props.battlemap?.isGridVisible ?? true)
const file = ref<File | null>(null)
const previewUrl = ref('')
const busy = ref(false)
const error = ref('')

function selectFile(event: Event) {
  const selected = (event.target as HTMLInputElement).files?.[0] ?? null
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
  file.value = selected
  previewUrl.value = selected ? URL.createObjectURL(selected) : ''
}

async function submit() {
  error.value = ''
  if (columns.value < 1 || columns.value > 100 || rows.value < 1 || rows.value > 100) {
    error.value = 'Linhas e colunas devem estar entre 1 e 100.'
    return
  }
  if (!file.value) {
    error.value = 'Selecione uma imagem PNG, JPEG ou WebP.'
    return
  }
  if (!store.session || store.connection === 'offline') {
    error.value = 'Conexão necessária para publicar o battlemap.'
    return
  }
  busy.value = true
  try {
    await api.configureBattlemap(
      store.session, props.encounterId, file.value, columns.value, rows.value, isGridVisible.value,
      props.battlemap?.revision ?? null)
    emit('configured')
  } catch (exception) {
    error.value = (exception as Error).message
  } finally {
    busy.value = false
  }
}

onBeforeUnmount(() => {
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
})
</script>

<template>
  <form class="battlemap-setup" @submit.prevent="submit">
    <h3>Configurar battlemap</h3>
    <div class="battlemap-fields">
      <label>Imagem
        <input type="file" accept="image/png,image/jpeg,image/webp" required @change="selectFile">
      </label>
      <label>Colunas do grid
        <input v-model.number="columns" aria-label="Colunas do grid" type="number" min="1" max="100" required>
      </label>
      <label>Linhas do grid
        <input v-model.number="rows" aria-label="Linhas do grid" type="number" min="1" max="100" required>
      </label>
      <label class="battlemap-grid-visibility">
        <input v-model="isGridVisible" type="checkbox">
        Exibir grid sobre o mapa
      </label>
    </div>
    <div v-if="previewUrl" class="battlemap-preview" :style="{ aspectRatio: `${columns} / ${rows}` }">
      <img :src="previewUrl" alt="Prévia do mapa selecionado">
      <span v-if="isGridVisible" class="battlemap-preview-grid" :style="{ '--columns': columns, '--rows': rows }" />
    </div>
    <p class="field-hint">PNG, JPEG ou WebP de até 20 MB. A prévia mostra o recorte final.</p>
    <p v-if="error" class="form-error" role="alert">{{ error }}</p>
    <button class="button primary" type="submit" :disabled="busy">
      {{ battlemap ? 'Substituir battlemap' : 'Publicar battlemap' }}
    </button>
  </form>
</template>
