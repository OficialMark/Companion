<script setup lang="ts">
import type { CampaignKnowledgeDetail, CampaignKnowledgeKind, CampaignKnowledgeSummary, CampaignMapPoint, SaveCampaignKnowledge } from '~/types/campaignKnowledge'
import { CAMPAIGN_KNOWLEDGE_KINDS } from '~/types/campaignKnowledge'

const store = useCampaignStore()
const api = useCampaignKnowledgeApi()
const kind = ref<CampaignKnowledgeKind>('Npc')
const query = ref('')
const items = ref<CampaignKnowledgeSummary[]>([])
const page = ref(1)
const total = ref(0)
const selected = ref<CampaignKnowledgeDetail | null>(null)
const busy = ref(false)
const error = ref('')
const formOpen = ref(false)
const titleInput = ref<HTMLInputElement | null>(null)
const form = reactive({ title: '', summary: '', description: '', visible: true })
const contribution = ref('')
const note = ref('')
const mapImage = ref('')
const imageInput = ref<HTMLInputElement | null>(null)
const pdfInput = ref<HTMLInputElement | null>(null)
const pdfBusy = ref(false)
const pdfError = ref<{ entryId: string; message: string } | null>(null)
const pointDraft = ref<{ x: number; y: number } | null>(null)
const editingPointId = ref<string | null>(null)
const pointTitle = ref('')
const pointDescription = ref('')
const isMaster = computed(() => store.isMaster)
const supportsPdf = computed(() => selected.value && ['Clue', 'Journal', 'MasterRecord'].includes(selected.value.kind))
const canChangePdf = computed(() => isMaster.value || selected.value?.authorId === store.session?.participantId)
const offline = computed(() => store.connection !== 'online')
const pendingEntryId = ref<string | null>(null)
let entryRequest = 0
const category = computed(() => CAMPAIGN_KNOWLEDGE_KINDS.find(item => item.key === kind.value)!)
const visibleTabs = computed(() => CAMPAIGN_KNOWLEDGE_KINDS.filter(tab => tab.key !== 'MasterRecord' || isMaster.value))
const categorySingular = computed(() => ({ Npc: 'NPC', Map: 'mapa', Clue: 'pista', Journal: 'diário', MasterRecord: 'registro' })[kind.value])

async function load() {
  if (!store.session) return
  busy.value = true; error.value = ''
  try {
    const result = await api.list(store.session, kind.value, query.value.trim(), page.value)
    items.value = result.items; total.value = result.total
    if (selected.value && !result.items.some(item => item.id === selected.value!.id)) closeDetail()
  } catch (e) { error.value = (e as Error).message }
  finally { busy.value = false }
}
async function openEntry(entry: CampaignKnowledgeSummary) {
  if (!store.session) return
  const requestId = ++entryRequest
  const session = store.session
  pendingEntryId.value = entry.id
  error.value = ''
  let nextImage = ''
  try {
    const nextEntry = await api.get(session, entry.id)
    if (nextEntry.hasImage) nextImage = await prepareImage(session, nextEntry.id)
    if (requestId !== entryRequest || store.session !== session) {
      if (nextImage.startsWith('blob:')) URL.revokeObjectURL(nextImage)
      return
    }

    // Keep the current detail visible while the next entry and its image load, then swap both
    // together so the image area does not collapse and expand around the user's scroll position.
    const scrollY = window.scrollY
    const oldImage = mapImage.value
    selected.value = nextEntry
    note.value = nextEntry.masterNote ?? ''
    pointDraft.value = null
    mapImage.value = nextImage
    await nextTick()
    window.scrollTo(window.scrollX, scrollY)
    if (oldImage.startsWith('blob:')) URL.revokeObjectURL(oldImage)
  } catch (e) {
    if (nextImage.startsWith('blob:')) URL.revokeObjectURL(nextImage)
    if (requestId === entryRequest) error.value = (e as Error).message
  } finally {
    if (requestId === entryRequest) pendingEntryId.value = null
  }
}
function closeDetail() {
  entryRequest++
  pendingEntryId.value = null
  selected.value = null; pointDraft.value = null
  if (mapImage.value.startsWith('blob:')) URL.revokeObjectURL(mapImage.value)
  mapImage.value = ''
}
async function loadImage() {
  const session = store.session
  const entry = selected.value
  if (!session || !entry?.hasImage) {
    if (mapImage.value.startsWith('blob:')) URL.revokeObjectURL(mapImage.value)
    mapImage.value = ''
    return
  }
  let nextImage = ''
  try {
    nextImage = await prepareImage(session, entry.id)
    if (store.session !== session || selected.value?.id !== entry.id) {
      if (nextImage.startsWith('blob:')) URL.revokeObjectURL(nextImage)
      return
    }
    const oldImage = mapImage.value
    const scrollY = window.scrollY
    mapImage.value = nextImage
    await nextTick()
    window.scrollTo(window.scrollX, scrollY)
    if (oldImage.startsWith('blob:')) URL.revokeObjectURL(oldImage)
  }
  catch (e) { error.value = (e as Error).message }
}
async function prepareImage(session: NonNullable<typeof store.session>, entryId: string) {
  const url = await api.image(session, entryId)
  const image = new Image()
  image.src = url
  try { await image.decode() } catch { /* Keep the URL; the rendered image can still report its own load error. */ }
  return url
}
async function startCreate() {
  closeDetail(); form.title = ''; form.summary = ''; form.description = ''; form.visible = kind.value !== 'MasterRecord'; formOpen.value = true
  await nextTick()
  titleInput.value?.focus()
}
function startEdit() {
  if (!selected.value) return
  form.title = selected.value.title; form.summary = selected.value.summary ?? ''
  form.description = selected.value.sharedDescription ?? ''; form.visible = selected.value.isPlayerVisible; formOpen.value = true
}
async function saveEntry() {
  if (!store.session || !form.title.trim()) return
  busy.value = true; error.value = ''
  const body: SaveCampaignKnowledge = { kind: kind.value, title: form.title, summary: form.summary || null, sharedDescription: form.description || null }
  if (isMaster.value) body.isPlayerVisible = kind.value === 'Journal' || (kind.value !== 'MasterRecord' && form.visible)
  try {
    selected.value = selected.value
      ? await api.save(store.session, selected.value.id, { ...body, expectedVersion: selected.value.version })
      : await api.create(store.session, body)
    formOpen.value = false; await load(); if (selected.value) await loadImage()
  } catch (e) { error.value = (e as Error).message }
  finally { busy.value = false }
}
async function addContribution() {
  if (!store.session || !selected.value || !contribution.value.trim()) return
  busy.value = true
  try { await api.contribute(store.session, selected.value.id, contribution.value); contribution.value = ''; selected.value = await api.get(store.session, selected.value.id) }
  catch (e) { error.value = (e as Error).message }
  finally { busy.value = false }
}
async function removeContribution(entry: CampaignKnowledgeDetail['contributions'][number]) {
  if (!store.session || !selected.value || (entry.authorId !== store.session.participantId && !isMaster.value)) return
  busy.value = true
  try {
    await api.deleteContribution(store.session, selected.value.id, entry.id)
    selected.value = await api.get(store.session, selected.value.id)
  } catch (e) { error.value = (e as Error).message }
  finally { busy.value = false }
}
async function saveMasterNote() {
  if (!store.session || !selected.value) return
  busy.value = true
  try { selected.value = await api.saveNote(store.session, selected.value.id, note.value) }
  catch (e) { error.value = (e as Error).message }
  finally { busy.value = false }
}
async function chooseImage(event: Event) {
  if (!store.session || !selected.value) return
  const file = (event.target as HTMLInputElement).files?.[0]
  if (!file) return
  busy.value = true
  try { selected.value = await api.uploadImage(store.session, selected.value.id, file); await loadImage() }
  catch (e) { error.value = (e as Error).message }
  finally { busy.value = false; (event.target as HTMLInputElement).value = '' }
}
async function changePdf(action: 'remove' | 'download' | File) {
  const session = store.session
  const entry = selected.value
  if (!session || !entry || offline.value || pdfBusy.value) return
  pdfError.value = null
  if (action instanceof File && action.size > 20 * 1024 * 1024) {
    pdfError.value = { entryId: entry.id, message: 'O PDF deve ter até 20 MB.' }
    return
  }

  pdfBusy.value = true
  try {
    if (action === 'download') {
      if (!entry.pdfFileName) return
      const blob = await api.downloadPdf(session, entry.id)
      if (store.session !== session) return
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = entry.pdfFileName
      link.click()
      setTimeout(() => URL.revokeObjectURL(url), 1000)
      return
    }
    const updated = action === 'remove'
      ? await api.removePdf(session, entry.id)
      : await api.uploadPdf(session, entry.id, action)
    if (store.session === session && selected.value?.id === entry.id) selected.value = updated
  } catch (e) {
    pdfError.value = { entryId: entry.id, message: (e as Error).message }
  } finally {
    pdfBusy.value = false
  }
}

function choosePdf(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (file) void changePdf(file)
}
function mapClicked(event: MouseEvent) {
  const rect = (event.currentTarget as HTMLElement).getBoundingClientRect()
  pointDraft.value = { x: Math.min(1, Math.max(0, (event.clientX - rect.left) / rect.width)), y: Math.min(1, Math.max(0, (event.clientY - rect.top) / rect.height)) }
  pointTitle.value = ''; pointDescription.value = ''
  editingPointId.value = null
}
function beginCenterPoint() {
  pointDraft.value = { x: 0.5, y: 0.5 }
  pointTitle.value = ''; pointDescription.value = ''; editingPointId.value = null
}
function setPointAxis(axis: 'x' | 'y', raw: string) {
  if (!pointDraft.value) return
  const value = Number(raw)
  if (!Number.isFinite(value)) return
  pointDraft.value = { ...pointDraft.value, [axis]: Math.min(1, Math.max(0, value / 100)) }
}
async function savePoint() {
  if (!store.session || !selected.value || !pointDraft.value || !pointTitle.value.trim()) return
  busy.value = true
  try {
    const payload = { title: pointTitle.value, description: pointDescription.value || null, ...pointDraft.value }
    if (editingPointId.value) await api.savePoint(store.session, selected.value.id, editingPointId.value, payload)
    else await api.addPoint(store.session, selected.value.id, payload)
    selected.value = await api.get(store.session, selected.value.id); pointDraft.value = null
    editingPointId.value = null
  } catch (e) { error.value = (e as Error).message }
  finally { busy.value = false }
}
function editPoint(point: CampaignMapPoint) {
  editingPointId.value = point.id
  pointDraft.value = { x: point.x, y: point.y }
  pointTitle.value = point.title; pointDescription.value = point.description ?? ''
}
async function removePoint(point: CampaignMapPoint) {
  if (!store.session || !selected.value || (point.authorId !== store.session.participantId && !isMaster.value)) return
  busy.value = true
  try { await api.deletePoint(store.session, selected.value.id, point.id); selected.value = await api.get(store.session, selected.value.id) }
  catch (e) { error.value = (e as Error).message }
  finally { busy.value = false }
}
async function removeEntry() {
  if (!store.session || !selected.value || (!isMaster.value && selected.value.authorId !== store.session.participantId)) return
  busy.value = true
  try { await api.remove(store.session, selected.value.id); closeDetail(); await load() }
  catch (e) { error.value = (e as Error).message }
  finally { busy.value = false }
}
function pointStyle(point: CampaignMapPoint) { return { left: `${point.x * 100}%`, top: `${point.y * 100}%` } }
function nextPage(delta: number) { page.value = Math.max(1, page.value + delta); void load() }
function dateLabel(value: string) { return new Intl.DateTimeFormat('pt-BR', { dateStyle: 'medium' }).format(new Date(value)) }
watch([kind], () => { page.value = 1; closeDetail(); void load() })
watch(isMaster, master => { if (!master && kind.value === 'MasterRecord') kind.value = 'Npc' })
watch(() => store.campaignKnowledgeRevision, () => { if (store.session) void load(); if (selected.value && store.session) void openEntry(selected.value) })
watch(() => store.session?.campaignId, () => { closeDetail(); void load() })
onMounted(() => void load())
onBeforeUnmount(closeDetail)
</script>

<template>
  <section class="campaign-notebook" aria-labelledby="campaign-notebook-title" data-campaign-notebook>
    <header class="notebook-heading">
      <div><p class="eyebrow"><Icon name="lucide:book-open-check" />Conhecimento compartilhado</p><h1 id="campaign-notebook-title">Campanha</h1><p class="notebook-lede">NPCs, mapas, pistas e lembranças que acompanham a mesa.</p></div>
      <button class="button primary" type="button" :disabled="offline" data-new-knowledge @click="startCreate"><Icon name="lucide:plus" />Novo {{ categorySingular }}</button>
    </header>
    <p v-if="offline" class="offline-warning" role="status"><Icon name="lucide:wifi-off" />Você está offline. O conteúdo está em modo de consulta.</p>
    <nav class="notebook-tabs" aria-label="Conteúdo da campanha">
      <button v-for="tab in visibleTabs" :key="tab.key" type="button" :aria-current="kind === tab.key ? 'page' : undefined" :data-kind="tab.key" @click="kind = tab.key"><Icon :name="tab.icon" />{{ tab.label }}</button>
    </nav>
    <div class="notebook-layout">
      <aside class="notebook-list panel" aria-label="Registros da campanha">
        <form class="notebook-search" @submit.prevent="page = 1; load()">
          <label class="sr-only" for="knowledge-search">Buscar {{ category.label.toLowerCase() }}</label>
          <Icon name="lucide:search" /><input id="knowledge-search" v-model="query" type="search" placeholder="Buscar por nome" maxlength="120">
          <button class="button secondary small" type="submit">Buscar</button>
        </form>
        <p v-if="error" class="form-error" role="alert">{{ error }}</p>
        <div v-if="busy && !items.length" class="empty-state" role="status">Carregando…</div>
        <div v-else-if="!items.length" class="empty-state"><span class="state-icon"><Icon :name="category.icon" size="1.6rem" /></span><p>Nenhum registro nesta seção ainda.</p></div>
        <ul v-else class="knowledge-items">
          <li v-for="item in items" :key="item.id">
            <button class="knowledge-item" :class="{ selected: selected?.id === item.id, pending: pendingEntryId === item.id }" :aria-busy="pendingEntryId === item.id" type="button" @click="openEntry(item)">
              <Icon :name="category.icon" /><span><strong>{{ item.title }}</strong><small>{{ item.summary || item.author }}<template v-if="item.summary"> · {{ item.author }}</template></small></span>
              <Icon v-if="isMaster && !item.isPlayerVisible" name="lucide:eye-off" title="Visível apenas ao mestre" />
            </button>
          </li>
        </ul>
        <div v-if="total > 24" class="notebook-pagination"><button class="button ghost small" type="button" :disabled="page <= 1" @click="nextPage(-1)">Anterior</button><span>{{ page }} · {{ total }}</span><button class="button ghost small" type="button" :disabled="page * 24 >= total" @click="nextPage(1)">Próxima</button></div>
      </aside>

      <article v-if="formOpen" class="panel knowledge-detail" aria-label="Editar registro da campanha">
        <form class="knowledge-form" @submit.prevent="saveEntry">
          <div class="panel-heading"><div><p class="eyebrow">{{ selected ? 'Editar informação' : 'Novo registro' }}</p><h2>{{ selected ? 'Atualizar' : 'Cadastrar' }} {{ categorySingular.toLowerCase() }}</h2></div><button class="icon-button" type="button" aria-label="Cancelar" @click="formOpen = false">×</button></div>
          <label class="sheet-field"><span>Nome</span><input ref="titleInput" v-model="form.title" type="text" maxlength="120" required autofocus></label>
          <label class="sheet-field"><span>Resumo</span><input v-model="form.summary" type="text" maxlength="500"></label>
          <label class="sheet-field"><span>{{ kind === 'MasterRecord' ? 'Anotações' : 'Descrição compartilhada' }}</span><textarea v-model="form.description" rows="7" maxlength="12000" /></label>
          <label v-if="isMaster && kind !== 'Journal' && kind !== 'MasterRecord'" class="notebook-visibility"><input v-model="form.visible" type="checkbox"><span><strong>Visível aos jogadores</strong><small>Desative para manter este registro reservado ao mestre.</small></span></label>
          <div class="button-row"><button class="button primary" type="submit" :disabled="busy || offline || !form.title.trim()"><Icon name="lucide:save" />Salvar</button><button class="button ghost" type="button" @click="formOpen = false">Cancelar</button></div>
        </form>
      </article>

      <article v-else-if="selected" class="panel knowledge-detail">
        <div class="panel-heading"><div><p class="eyebrow"><Icon :name="category.icon" />{{ categorySingular }} · {{ selected.author }} · {{ dateLabel(selected.updatedAt) }}</p><h2>{{ selected.title }}</h2></div><button class="icon-button" type="button" aria-label="Fechar detalhes" @click="closeDetail"><Icon name="lucide:x" /></button></div>
        <p v-if="selected.summary" class="knowledge-summary">{{ selected.summary }}</p>
        <p v-if="selected.sharedDescription" class="knowledge-description">{{ selected.sharedDescription }}</p>
        <section v-if="supportsPdf && (canChangePdf || selected.pdfFileName)" class="knowledge-pdf" aria-label="Anexo PDF" :aria-busy="pdfBusy">
          <input v-if="canChangePdf" ref="pdfInput" type="file" accept=".pdf,application/pdf" hidden @change="choosePdf">
          <span v-if="selected.pdfFileName" class="knowledge-pdf-name"><Icon name="lucide:file-text" />{{ selected.pdfFileName }}</span>
          <div class="button-row">
            <button v-if="selected.pdfFileName" class="button secondary small" type="button" :disabled="pdfBusy || offline" @click="changePdf('download')">Baixar PDF</button>
            <button v-if="canChangePdf" class="button secondary small" type="button" :disabled="pdfBusy || offline" @click="pdfInput?.click()">{{ selected.pdfFileName ? 'Trocar PDF' : 'Anexar PDF' }}</button>
            <button v-if="canChangePdf && selected.pdfFileName" class="button ghost small" type="button" :disabled="pdfBusy || offline" @click="changePdf('remove')">Remover PDF</button>
          </div>
          <p v-if="pdfError && pdfError.entryId === selected.id" class="form-error" role="alert">{{ pdfError.message }}</p>
        </section>
        <input ref="imageInput" class="sr-only" type="file" accept="image/png,image/jpeg,image/webp" @change="chooseImage">
        <section v-if="selected.kind !== 'Map'" class="knowledge-image-section">
          <button v-if="isMaster || !selected.hasImage" class="button secondary small" type="button" :disabled="busy || offline" @click="imageInput?.click()"><Icon name="lucide:image-plus" />{{ selected.hasImage ? 'Trocar imagem' : 'Adicionar imagem' }}</button>
          <img v-if="mapImage" class="knowledge-image" :src="mapImage" :alt="`Imagem de ${selected.title}`">
          <p v-else-if="!selected.hasImage" class="field-hint">Este registro ainda não tem imagem.</p>
          <p v-else class="field-hint">Carregando imagem…</p>
        </section>
        <div v-if="selected.kind === 'Map'" class="campaign-map-wrap">
          <button v-if="isMaster || !selected.hasImage" class="button secondary small map-upload" type="button" :disabled="busy || offline" @click="imageInput?.click()"><Icon name="lucide:image-plus" />{{ selected.hasImage ? 'Trocar imagem' : 'Adicionar imagem' }}</button>
          <button v-if="mapImage" class="button ghost small" type="button" :disabled="offline" @click="beginCenterPoint"><Icon name="lucide:map-pin-plus" />Adicionar ponto pelo teclado</button>
          <div v-if="mapImage" class="campaign-map" @click="mapClicked">
            <img :src="mapImage" alt="Mapa da campanha; toque ou clique para adicionar um ponto de interesse" draggable="false">
            <button v-for="point in selected.points" :key="point.id" class="campaign-map-point" :style="pointStyle(point)" type="button" :aria-label="`${point.title}${point.description ? `: ${point.description}` : ''}`" :title="point.title" @click.stop="void 0"><span>{{ point.title }}</span></button>
            <button v-if="pointDraft" class="campaign-map-draft" :style="{ left: `${pointDraft.x * 100}%`, top: `${pointDraft.y * 100}%` }" type="button" aria-label="Novo ponto" @click.stop="void 0"><Icon name="lucide:map-pin" /></button>
          </div>
          <p v-else class="empty-state">{{ selected.hasImage ? 'Carregando imagem…' : 'Este mapa ainda não tem imagem.' }}</p>
          <form v-if="pointDraft" class="point-editor" @submit.prevent="savePoint"><label class="sheet-field"><span>Nome do ponto</span><input v-model="pointTitle" type="text" maxlength="100" required autofocus></label><label class="sheet-field"><span>Informação</span><input v-model="pointDescription" type="text" maxlength="2000"></label><label class="sheet-field point-position"><span>Horizontal (%)</span><input type="number" min="0" max="100" step="0.1" :value="(pointDraft.x * 100).toFixed(1)" @input="setPointAxis('x', ($event.target as HTMLInputElement).value)"></label><label class="sheet-field point-position"><span>Vertical (%)</span><input type="number" min="0" max="100" step="0.1" :value="(pointDraft.y * 100).toFixed(1)" @input="setPointAxis('y', ($event.target as HTMLInputElement).value)"></label><button class="button primary small" type="submit" :disabled="busy || offline"><Icon name="lucide:map-pin" />{{ editingPointId ? 'Salvar ponto' : 'Marcar ponto' }}</button><button class="button ghost small" type="button" @click="pointDraft = null; editingPointId = null">Cancelar</button></form>
          <ul v-if="selected.points.length" class="point-list"><li v-for="point in selected.points" :key="point.id"><Icon name="lucide:map-pin" /><span><strong>{{ point.title }}</strong><small>{{ point.description || point.author }}</small></span><div v-if="point.authorId === store.session?.participantId || isMaster" class="point-actions"><button class="icon-button" type="button" :aria-label="`Editar ${point.title}`" :disabled="offline" @click="editPoint(point)"><Icon name="lucide:pencil" /></button><button class="icon-button" type="button" :aria-label="`Remover ${point.title}`" :disabled="offline || busy" @click="removePoint(point)"><Icon name="lucide:trash-2" /></button></div></li></ul>
        </div>
        <div class="detail-actions"><button class="button secondary small" type="button" :disabled="offline" @click="startEdit"><Icon name="lucide:pencil" />Complementar</button><button v-if="isMaster || selected.authorId === store.session?.participantId" class="button ghost small" type="button" :disabled="offline || busy" @click="removeEntry"><Icon name="lucide:trash-2" />Remover</button></div>
        <section v-if="selected.kind !== 'MasterRecord'" class="contribution-section"><h3>Informações do grupo</h3><ul v-if="selected.contributions.length" class="contribution-list"><li v-for="entry in selected.contributions" :key="entry.id"><div class="contribution-copy"><p>{{ entry.body }}</p><small>{{ entry.author }} · {{ dateLabel(entry.createdAt) }}</small></div><button v-if="isMaster || entry.authorId === store.session?.participantId" class="icon-button contribution-delete" type="button" :aria-label="`Remover contribuição de ${entry.author}`" :disabled="busy || offline" @click="removeContribution(entry)"><Icon name="lucide:trash-2" /></button></li></ul><p v-else class="field-hint">Ainda não há contribuições. Adicione um detalhe que o grupo descobriu.</p><form class="inline-contribution" @submit.prevent="addContribution"><label class="sr-only" for="new-contribution">Nova informação</label><textarea id="new-contribution" v-model="contribution" rows="3" maxlength="8000" placeholder="Adicionar informação compartilhada…" :disabled="offline" /><button class="button secondary small" type="submit" :disabled="busy || offline || !contribution.trim()"><Icon name="lucide:send" />Publicar</button></form></section>
        <section v-if="isMaster" class="master-notes"><h3><Icon name="lucide:lock-keyhole" />Anotações pessoais do mestre</h3><textarea v-model="note" rows="4" maxlength="12000" placeholder="Somente mestres desta campanha podem ver estas notas." :disabled="offline" /><button class="button ghost small" type="button" :disabled="busy || offline" @click="saveMasterNote"><Icon name="lucide:save" />Salvar anotação privada</button></section>
      </article>
      <section v-else class="panel notebook-prompt"><span class="state-icon"><Icon :name="category.icon" size="1.8rem" /></span><h2>{{ category.label }}</h2><p>{{ kind === 'MasterRecord' ? 'Anotações privadas, visíveis somente aos mestres desta campanha.' : 'Selecione um registro ou crie uma nova informação para o grupo.' }}</p></section>
    </div>
  </section>
</template>
