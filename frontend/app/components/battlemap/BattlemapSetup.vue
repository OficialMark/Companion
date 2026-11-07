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
const imageSize = ref<{ width: number; height: number } | null>(null)
const busy = ref(false)
const error = ref('')
const pendingConfirmation = ref(false)

const MAX_GRID = 100
const PREVIEW_MAX_WIDTH = 560
const PREVIEW_MAX_HEIGHT = 340
// Above this the stretch needed to fill the grid becomes noticeable.
const DISTORTION_TOLERANCE = .03

const impactedTokens = computed(() => {
  if (!props.battlemap) return []
  return props.battlemap.tokens.filter(token =>
    token.column !== null && token.row !== null &&
    (token.column >= columns.value || token.row >= rows.value))
})

watch([columns, rows, file], () => { pendingConfirmation.value = false })

const clampGrid = (value: number) => Math.max(1, Math.min(MAX_GRID, Math.round(value)))

/**
 * Picks a grid whose proportion matches the image as closely as possible, so the
 * whole image fills the grid with (almost) no stretch. The long side stays close
 * to 20 cells; each candidate is scored by aspect error with a small bias toward 20.
 */
function suggestGrid(naturalWidth: number, naturalHeight: number) {
  if (naturalWidth <= 0 || naturalHeight <= 0) return null
  const landscape = naturalWidth >= naturalHeight
  const ratio = landscape ? naturalHeight / naturalWidth : naturalWidth / naturalHeight
  let best: { long: number; short: number; score: number } | null = null
  for (let long = 16; long <= 24; long++) {
    const short = clampGrid(long * ratio)
    const error = Math.abs(short / long - ratio) / ratio
    const score = error + Math.abs(long - 20) * .002
    if (!best || score < best.score) best = { long, short, score }
  }
  return landscape ? { columns: best!.long, rows: best!.short } : { columns: best!.short, rows: best!.long }
}

const imageRatio = computed(() => imageSize.value ? imageSize.value.width / imageSize.value.height : null)
const validGrid = computed(() => columns.value >= 1 && rows.value >= 1 && columns.value <= MAX_GRID && rows.value <= MAX_GRID)
const gridRatio = computed(() => validGrid.value ? columns.value / rows.value : null)
// How much the image is stretched to fill the grid (0 = perfect match).
const distortion = computed(() => imageRatio.value && gridRatio.value ? Math.abs(gridRatio.value / imageRatio.value - 1) : 0)
const distortionPercent = computed(() => Math.round(distortion.value * 100))

const previewStyle = computed(() => {
  const ratio = gridRatio.value ?? imageRatio.value ?? 4 / 3
  const width = Math.min(PREVIEW_MAX_WIDTH, PREVIEW_MAX_HEIGHT * ratio)
  return { aspectRatio: String(ratio), width: `min(100%, ${Math.round(width)}px)` }
})

function formatSize(bytes: number) {
  return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`
}

function applySuggestion() {
  if (!imageSize.value) return
  const suggestion = suggestGrid(imageSize.value.width, imageSize.value.height)
  if (suggestion) { columns.value = suggestion.columns; rows.value = suggestion.rows }
}

function matchRowsToImage() {
  if (!imageRatio.value) return
  rows.value = clampGrid(columns.value / imageRatio.value)
}

function selectFile(event: Event) {
  const selected = (event.target as HTMLInputElement).files?.[0] ?? null
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
  file.value = selected
  imageSize.value = null
  previewUrl.value = selected ? URL.createObjectURL(selected) : ''
  if (selected && previewUrl.value) {
    const probe = new Image()
    const objectUrl = previewUrl.value
    probe.onload = () => {
      if (previewUrl.value !== objectUrl) return
      if (probe.naturalWidth > 0 && probe.naturalHeight > 0) {
        imageSize.value = { width: probe.naturalWidth, height: probe.naturalHeight }
      }
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
  if (columns.value < 1 || columns.value > MAX_GRID || rows.value < 1 || rows.value > MAX_GRID) {
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

    <label class="file-drop" :class="{ 'has-file': file }">
      <input type="file" accept="image/png,image/jpeg,image/webp" required aria-label="Imagem do battlemap" @change="selectFile">
      <span class="file-drop-icon" aria-hidden="true"><Icon :name="file ? 'lucide:file-image' : 'lucide:upload'" size="1.4rem" /></span>
      <span class="file-drop-text">
        <template v-if="file">
          <strong>{{ file.name }}</strong>
          <small>{{ formatSize(file.size) }}<template v-if="imageSize"> · {{ imageSize.width }} × {{ imageSize.height }} px</template> · clique para trocar</small>
        </template>
        <template v-else>
          <strong>Escolha ou arraste a imagem do mapa</strong>
          <small>PNG, JPEG ou WebP de até 20 MB, em qualquer proporção</small>
        </template>
      </span>
    </label>

    <div class="battlemap-fields">
      <label><span class="label-with-icon"><Icon name="lucide:columns-3" />Colunas</span>
        <input v-model.number="columns" aria-label="Colunas do grid" type="number" min="1" max="100" required>
      </label>
      <label><span class="label-with-icon"><Icon name="lucide:rows-3" />Linhas</span>
        <input v-model.number="rows" aria-label="Linhas do grid" type="number" min="1" max="100" required>
      </label>
      <label class="switch-field">
        <input v-model="isGridVisible" type="checkbox" role="switch">
        <span class="switch-track" aria-hidden="true"><span class="switch-thumb" /></span>
        <span class="label-with-icon"><Icon name="lucide:grid-3x3" />Exibir grid</span>
      </label>
    </div>

    <div v-if="previewUrl" class="battlemap-preview" :style="previewStyle">
      <img :src="previewUrl" alt="Prévia do mapa selecionado">
      <span v-if="isGridVisible" class="battlemap-preview-grid" :style="{ '--columns': columns, '--rows': rows }" />
    </div>

    <div v-if="imageSize && distortion > DISTORTION_TOLERANCE" class="battlemap-ratio-warning" role="status">
      <Icon name="lucide:ratio" />
      <p>O grid {{ columns }} × {{ rows }} tem proporção diferente da imagem: ela será esticada cerca de {{ distortionPercent }}% para aparecer inteira, sem cortes.</p>
      <div class="ratio-actions">
        <button class="button ghost compact" type="button" @click="matchRowsToImage"><Icon name="lucide:rows-3" />Ajustar linhas</button>
        <button class="button secondary compact" type="button" @click="applySuggestion"><Icon name="lucide:wand-sparkles" />Sugerir grid</button>
      </div>
    </div>
    <p v-else class="field-hint">
      <Icon name="lucide:info" />
      A imagem aparece inteira, sem cortes. Colunas e linhas são sugeridas pela proporção da imagem; se o mapa já tem um grid desenhado, informe a contagem dele.
    </p>

    <div v-if="pendingConfirmation" class="battlemap-shrink-warning" role="alert">
      <p><Icon name="lucide:triangle-alert" /><span><strong>{{ impactedTokens.length }} pino{{ impactedTokens.length > 1 ? 's' : '' }}</strong> {{ impactedTokens.length > 1 ? 'ficam' : 'fica' }} fora do novo grid e {{ impactedTokens.length > 1 ? 'voltarão' : 'voltará' }} para a reserva:</span></p>
      <ul>
        <li v-for="token in impactedTokens" :key="token.participantId">{{ token.displayName }}</li>
      </ul>
    </div>
    <p v-if="error" class="form-error" role="alert"><Icon name="lucide:circle-alert" />{{ error }}</p>
    <div class="battlemap-setup-actions">
      <button v-if="pendingConfirmation" class="button ghost" type="button" :disabled="busy" @click="cancelConfirmation">Cancelar</button>
      <button class="button primary" type="submit" :disabled="busy">
        <Icon :name="busy ? 'lucide:loader-circle' : 'lucide:image-up'" :class="{ spin: busy }" />
        {{ pendingConfirmation ? 'Confirmar substituição' : (battlemap ? 'Substituir battlemap' : 'Publicar battlemap') }}
      </button>
    </div>
  </form>
</template>
