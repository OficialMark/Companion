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
const pendingConfirmation = ref(false)

const impactedTokens = computed(() => {
  if (!props.battlemap) return []
  return props.battlemap.tokens.filter(token =>
    token.column !== null && token.row !== null &&
    (token.column >= columns.value || token.row >= rows.value))
})

watch([columns, rows, file], () => { pendingConfirmation.value = false })

function suggestGrid(naturalWidth: number, naturalHeight: number) {
  const base = 20
  if (naturalWidth <= 0 || naturalHeight <= 0) return null
  if (naturalWidth >= naturalHeight) {
    return { columns: base, rows: Math.max(1, Math.min(100, Math.round(base * naturalHeight / naturalWidth))) }
  }
  return { columns: Math.max(1, Math.min(100, Math.round(base * naturalWidth / naturalHeight))), rows: base }
}

function selectFile(event: Event) {
  const selected = (event.target as HTMLInputElement).files?.[0] ?? null
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
  file.value = selected
  previewUrl.value = selected ? URL.createObjectURL(selected) : ''
  if (selected && previewUrl.value) {
    const probe = new Image()
    const objectUrl = previewUrl.value
    probe.onload = () => {
      if (previewUrl.value !== objectUrl) return
      const suggestion = suggestGrid(probe.naturalWidth, probe.naturalHeight)
      if (suggestion) { columns.value = suggestion.columns; rows.value = suggestion.rows }
    }
    probe.src = objectUrl
  }
}

function cancelConfirmation() {
  pendingConfirmation.value = false
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
  if (impactedTokens.value.length > 0 && !pendingConfirmation.value) {
    pendingConfirmation.value = true
    return
  }
  busy.value = true
  try {
    await api.configureBattlemap(
      store.session, props.encounterId, file.value, columns.value, rows.value, isGridVisible.value,
      props.battlemap?.revision ?? null)
    pendingConfirmation.value = false
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
    <p class="field-hint">PNG, JPEG ou WebP de até 20 MB. As colunas e linhas se ajustam à proporção da imagem ao selecioná-la (edite se quiser um recorte diferente). A prévia mostra o recorte final.</p>
    <div v-if="pendingConfirmation" class="battlemap-shrink-warning" role="alert">
      <p><strong>{{ impactedTokens.length }} pino{{ impactedTokens.length > 1 ? 's' : '' }}</strong> {{ impactedTokens.length > 1 ? 'ficam' : 'fica' }} fora do novo grid e {{ impactedTokens.length > 1 ? 'voltarão' : 'voltará' }} para a reserva:</p>
      <ul>
        <li v-for="token in impactedTokens" :key="token.participantId">{{ token.displayName }}</li>
      </ul>
    </div>
    <p v-if="error" class="form-error" role="alert">{{ error }}</p>
    <div class="battlemap-setup-actions">
      <button v-if="pendingConfirmation" class="button ghost" type="button" :disabled="busy" @click="cancelConfirmation">Cancelar</button>
      <button class="button primary" type="submit" :disabled="busy">
        {{ pendingConfirmation ? 'Confirmar substituição' : (battlemap ? 'Substituir battlemap' : 'Publicar battlemap') }}
      </button>
    </div>
  </form>
</template>
