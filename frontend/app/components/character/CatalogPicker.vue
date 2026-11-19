<script setup lang="ts">
import type { CatalogItemQuery, CatalogItemSummary, CatalogSpellSummary, Character, ItemCategory } from '~/types/character'
import { CATEGORY_LABEL } from '~/types/character'
import { CATEGORY_ORDER } from './ItemEditorModal.vue'

/**
 * Pick items or spells from the catalogs (spec 011, US7). Search waits for a pause in typing,
 * filters go to the server, results are paginated. A sequence number makes sure a slow answer
 * never replaces the results of a newer search. The SRD credit appears only for items; the
 * spell catalog carries no credit by the owners' request.
 */
const props = defineProps<{ mode: 'items' | 'spells' }>()
const emit = defineEmits<{ close: [] }>()

/** The backend DTO has these fields; the shared TS type may not have them yet. */
type ItemSummary = CatalogItemSummary
type ItemQuery = CatalogItemQuery

const SEARCH_DELAY_MS = 300
const PAGE_SIZE = 20
const BASE_PAGE_SIZE = 50
const RARITIES = ['Comum', 'Incomum', 'Raro', 'Muito Raro', 'Lendário', 'Artefato', 'Varia']
const SPELL_CLASSES = ['bardo', 'bruxo', 'clérigo', 'druida', 'feiticeiro', 'mago', 'paladino', 'patrulheiro']

const character = useCharacterStore()
const searchInput = ref<HTMLInputElement | null>(null)
let opener: Element | null = null

const search = ref('')
const query = ref('')
const category = ref<ItemCategory | ''>('')
const rarity = ref('')
const level = ref<number | ''>('')
const spellClass = ref('')
const ritual = ref(false)
const concentration = ref(false)
const page = ref(1)

const items = ref<ItemSummary[]>([])
const spells = ref<CatalogSpellSummary[]>([])
const total = ref(0)
const attribution = ref<string | null>(null)
const loading = ref(false)
const loadError = ref('')
const addingId = ref<string | null>(null)
const status = ref('')
const actionError = ref('')

/** Base item step for generic magic items ("Arma +1"). */
const baseFor = ref<ItemSummary | null>(null)
const baseSearch = ref('')
const baseQuery = ref('')
const baseItems = ref<ItemSummary[]>([])
const baseTotal = ref(0)
const baseLoading = ref(false)
const baseError = ref('')

let sequence = 0
let baseSequence = 0
let searchTimer: ReturnType<typeof setTimeout> | undefined
let baseTimer: ReturnType<typeof setTimeout> | undefined

const pages = computed(() => Math.max(1, Math.ceil(total.value / PAGE_SIZE)))
const title = computed(() => props.mode === 'items' ? 'Itens do catálogo' : 'Magias do catálogo')
const results = computed(() => props.mode === 'items' ? items.value.length : spells.value.length)

function levelLabel(value: number) { return value === 0 ? 'Truque' : `${value}º círculo` }
function errorMessage(error: unknown) { return (error as Error)?.message || 'Não foi possível consultar o catálogo.' }

async function load() {
  const id = ++sequence
  loading.value = true
  loadError.value = ''
  try {
    const api = useCharacterApi()
    const session = character.session()
    if (props.mode === 'items') {
      const result = await api.listCatalogItems(session, {
        q: query.value || undefined, category: category.value || undefined, rarity: rarity.value || undefined, page: page.value, pageSize: PAGE_SIZE
      })
      if (id !== sequence) return
      items.value = result.items as ItemSummary[]
      total.value = result.total
      attribution.value = result.attribution
    } else {
      const result = await api.listCatalogSpells(session, {
        q: query.value || undefined,
        level: level.value === '' ? null : level.value,
        class: spellClass.value || undefined,
        ritual: ritual.value ? true : null,
        concentration: concentration.value ? true : null,
        page: page.value,
        pageSize: PAGE_SIZE
      })
      if (id !== sequence) return
      spells.value = result.items
      total.value = result.total
    }
  } catch (error) {
    if (id !== sequence) return
    loadError.value = errorMessage(error)
  } finally {
    if (id === sequence) loading.value = false
  }
}

watch(search, (value) => {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    query.value = value.trim()
    page.value = 1
    void load()
  }, SEARCH_DELAY_MS)
})
watch([category, rarity, level, spellClass, ritual, concentration], () => {
  page.value = 1
  void load()
})

function goTo(target: number) {
  if (target < 1 || target > pages.value || target === page.value) return
  page.value = target
  void load()
}

async function added(name: string, action: Parameters<typeof character.run>[0]) {
  status.value = ''
  actionError.value = ''
  const ok = await character.run(action)
  if (ok) status.value = props.mode === 'items' ? `${name} foi para a mochila.` : `${name} entrou na sua lista de magias.`
  else actionError.value = character.error || 'Não foi possível adicionar.'
  return ok
}

async function addItem(item: ItemSummary) {
  if (item.requiresBaseItem) {
    openBaseStep(item)
    return
  }
  addingId.value = item.id
  await added(item.name, (api, session) => api.addItemFromCatalog(session, item.id, 1))
  addingId.value = null
}

async function addSpell(spell: CatalogSpellSummary) {
  addingId.value = spell.id
  await added(spell.name, (api, session) => api.addSpellFromCatalog(session, spell.id))
  addingId.value = null
}

function openBaseStep(item: ItemSummary) {
  baseFor.value = item
  baseSearch.value = ''
  baseQuery.value = ''
  status.value = ''
  actionError.value = ''
  void loadBase()
}

async function loadBase() {
  const target = baseFor.value
  if (!target) return
  const id = ++baseSequence
  baseLoading.value = true
  baseError.value = ''
  try {
    const params: ItemQuery = { q: baseQuery.value || undefined, category: target.category, mundane: true, page: 1, pageSize: BASE_PAGE_SIZE }
    const result = await useCharacterApi().listCatalogItems(character.session(), params)
    if (id !== baseSequence) return
    baseItems.value = result.items as ItemSummary[]
    baseTotal.value = result.total
  } catch (error) {
    if (id !== baseSequence) return
    baseError.value = errorMessage(error)
  } finally {
    if (id === baseSequence) baseLoading.value = false
  }
}

watch(baseSearch, (value) => {
  clearTimeout(baseTimer)
  baseTimer = setTimeout(() => {
    baseQuery.value = value.trim()
    void loadBase()
  }, SEARCH_DELAY_MS)
})

/** useCharacterApi.addItemFromCatalog does not send the base yet: same route, with baseCatalogItemId. */
async function chooseBase(base: ItemSummary) {
  const target = baseFor.value
  if (!target) return
  addingId.value = base.id
  const { request } = useCompanionApi()
  const ok = await added(target.name, (_api, session) => request<Character>(
    `/campaigns/${session.campaignId}/participants/me/character/items/from-catalog`,
    { method: 'POST', body: JSON.stringify({ catalogItemId: target.id, quantity: 1, baseCatalogItemId: base.id }) },
    session.participantToken
  ))
  addingId.value = null
  if (ok) {
    status.value = `${target.name} (${base.name}) foi para a mochila.`
    baseFor.value = null
  }
}

function backFromBase() {
  baseFor.value = null
  actionError.value = ''
}

function close() { emit('close') }

onMounted(async () => {
  opener = document.activeElement
  void load()
  await nextTick()
  searchInput.value?.focus()
})
onBeforeUnmount(() => {
  clearTimeout(searchTimer)
  clearTimeout(baseTimer)
  sequence++
  baseSequence++
  if (opener instanceof HTMLElement) opener.focus()
})
</script>

<template>
  <div class="modal-overlay sheet-modal-overlay" @keydown.esc.stop="close" @click.self="close">
    <div
      class="modal-panel sheet-modal catalog-picker"
      role="dialog"
      aria-modal="true"
      aria-labelledby="catalog-picker-title"
      :data-catalog-picker="mode"
    >
      <header class="sheet-modal-header">
        <div>
          <p class="eyebrow"><Icon :name="mode === 'items' ? 'lucide:backpack' : 'lucide:wand-sparkles'" />Catálogo</p>
          <h2 id="catalog-picker-title">{{ baseFor ? 'Escolha a base' : title }}</h2>
        </div>
        <button type="button" class="icon-button" aria-label="Fechar" @click="close"><Icon name="lucide:x" /></button>
      </header>

      <p v-if="status" class="form-success" role="status" data-catalog-status><Icon name="lucide:check" />{{ status }}</p>
      <p v-if="actionError" class="form-error" role="alert" data-catalog-error>{{ actionError }}</p>

      <!-- Step 2 for generic magic items: choose the mundane weapon or armor that carries the bonus. -->
      <section v-if="baseFor" class="catalog-base-step" data-base-step>
        <p class="sheet-hint">
          {{ baseFor.category === 'Armor' ? 'Escolha a armadura base' : 'Escolha a arma base' }} para <strong>{{ baseFor.name }}</strong>.
        </p>
        <label class="catalog-search">
          <Icon name="lucide:search" />
          <input v-model="baseSearch" type="search" inputmode="search" placeholder="Filtrar pelo nome" aria-label="Filtrar itens base" data-base-search>
        </label>
        <p v-if="baseLoading" class="catalog-state" role="status"><Icon name="lucide:loader-circle" class="spin" />Carregando…</p>
        <p v-else-if="baseError" class="form-error" role="alert">{{ baseError }}</p>
        <p v-else-if="!baseItems.length" class="catalog-state">Nenhum item base encontrado.</p>
        <ul v-else class="catalog-list">
          <li v-for="base in baseItems" :key="base.id" class="catalog-row" :data-base-id="base.id">
            <div class="catalog-row-main">
              <p class="catalog-row-name">{{ base.name }} <small v-if="base.nameEn">{{ base.nameEn }}</small></p>
              <p v-if="base.subcategory" class="catalog-row-meta">{{ base.subcategory }}</p>
            </div>
            <button type="button" class="button secondary small" :disabled="addingId !== null" data-choose-base @click="chooseBase(base)">
              <Icon name="lucide:check" />Escolher
            </button>
          </li>
        </ul>
        <p v-if="baseTotal > baseItems.length" class="sheet-hint">Mostrando {{ baseItems.length }} de {{ baseTotal }}. Filtre pelo nome para achar outro.</p>
        <div class="sheet-modal-actions">
          <button type="button" class="button ghost" data-base-back @click="backFromBase"><Icon name="lucide:arrow-left" />Voltar</button>
        </div>
      </section>

      <template v-else>
        <div class="catalog-filters">
          <label class="catalog-search">
            <Icon name="lucide:search" />
            <input
              ref="searchInput"
              v-model="search"
              type="search"
              inputmode="search"
              :placeholder="mode === 'items' ? 'Buscar item (PT ou EN)' : 'Buscar magia'"
              :aria-label="mode === 'items' ? 'Buscar item' : 'Buscar magia'"
              data-catalog-search
            >
          </label>
          <template v-if="mode === 'items'">
            <select v-model="category" aria-label="Categoria" data-filter="category">
              <option value="">Todas as categorias</option>
              <option v-for="value in CATEGORY_ORDER" :key="value" :value="value">{{ CATEGORY_LABEL[value] }}</option>
            </select>
            <select v-model="rarity" aria-label="Raridade" data-filter="rarity">
              <option value="">Qualquer raridade</option>
              <option v-for="value in RARITIES" :key="value" :value="value">{{ value }}</option>
            </select>
          </template>
          <template v-else>
            <select v-model="level" aria-label="Círculo" data-filter="level">
              <option value="">Todos os círculos</option>
              <option v-for="value in 10" :key="value - 1" :value="value - 1">{{ levelLabel(value - 1) }}</option>
            </select>
            <select v-model="spellClass" aria-label="Classe" data-filter="class">
              <option value="">Todas as classes</option>
              <option v-for="value in SPELL_CLASSES" :key="value" :value="value">{{ value }}</option>
            </select>
            <div class="chip-row" role="group" aria-label="Filtros de magia">
              <button type="button" class="chip" :aria-pressed="ritual" data-filter="ritual" @click="ritual = !ritual">Ritual</button>
              <button type="button" class="chip" :aria-pressed="concentration" data-filter="concentration" @click="concentration = !concentration">Concentração</button>
            </div>
          </template>
        </div>

        <div class="catalog-results" :aria-busy="loading">
          <p v-if="loading && !results" class="catalog-state" role="status" data-catalog-loading><Icon name="lucide:loader-circle" class="spin" />Carregando…</p>
          <div v-else-if="loadError" class="catalog-state" role="alert" data-catalog-load-error>
            <p class="form-error">{{ loadError }}</p>
            <button type="button" class="button ghost small" @click="load"><Icon name="lucide:refresh-cw" />Tentar de novo</button>
          </div>
          <p v-else-if="!results" class="catalog-state" data-catalog-empty>Nada encontrado com esses filtros.</p>

          <ul v-else-if="mode === 'items'" class="catalog-list" :class="{ 'is-loading': loading }">
            <li v-for="item in items" :key="item.id" class="catalog-row" :data-catalog-id="item.id">
              <div class="catalog-row-main">
                <p class="catalog-row-name">{{ item.name }} <small v-if="item.nameEn">{{ item.nameEn }}</small></p>
                <p class="catalog-row-meta">
                  <span>{{ item.subcategory || CATEGORY_LABEL[item.category] }}</span>
                  <span v-if="item.rarity" class="item-badge subtle">{{ item.rarity }}</span>
                  <span v-if="item.magicBonus > 0" class="item-badge magic">+{{ item.magicBonus }}</span>
                  <span v-if="item.requiresAttunement" class="item-badge subtle">sintonia</span>
                  <span v-if="item.requiresBaseItem" class="item-badge subtle">escolha a base</span>
                </p>
              </div>
              <button type="button" class="button secondary small" :disabled="addingId !== null" data-add-catalog @click="addItem(item)">
                <Icon :name="addingId === item.id ? 'lucide:loader-circle' : 'lucide:plus'" :class="{ spin: addingId === item.id }" />Adicionar
              </button>
            </li>
          </ul>

          <ul v-else class="catalog-list" :class="{ 'is-loading': loading }">
            <li v-for="spell in spells" :key="spell.id" class="catalog-row" :data-catalog-id="spell.id">
              <div class="catalog-row-main">
                <p class="catalog-row-name">{{ spell.name }}</p>
                <p class="catalog-row-meta">
                  <span>{{ levelLabel(spell.level) }}<template v-if="spell.school">, {{ spell.school }}</template></span>
                  <span v-if="spell.concentration" class="item-badge subtle" title="Concentração">C</span>
                  <span v-if="spell.ritual" class="item-badge subtle" title="Ritual">R</span>
                </p>
              </div>
              <button type="button" class="button secondary small" :disabled="addingId !== null" data-add-catalog @click="addSpell(spell)">
                <Icon :name="addingId === spell.id ? 'lucide:loader-circle' : 'lucide:plus'" :class="{ spin: addingId === spell.id }" />Adicionar
              </button>
            </li>
          </ul>
        </div>

        <nav v-if="total > PAGE_SIZE" class="catalog-pages" aria-label="Páginas do catálogo">
          <button type="button" class="button ghost small" :disabled="page <= 1 || loading" data-page-prev @click="goTo(page - 1)">
            <Icon name="lucide:chevron-left" />Anterior
          </button>
          <span data-page-label>Página {{ page }} de {{ pages }}</span>
          <button type="button" class="button ghost small" :disabled="page >= pages || loading" data-page-next @click="goTo(page + 1)">
            Próxima<Icon name="lucide:chevron-right" />
          </button>
        </nav>

        <p v-if="mode === 'items' && attribution" class="catalog-attribution" data-attribution>{{ attribution }}</p>
      </template>
    </div>
  </div>
</template>
