<script lang="ts">
import { ref } from 'vue'

// One add at a time across every picker on the page (setup panel and board can both be open).
const adding = ref<string | null>(null)
</script>

<script setup lang="ts">
import type { CreatureSummary } from '~/types/creatures'

/** Catalog search inside combat preparation: find a creature and add 1-20 copies as tokens. */
const PAGE = 6
const MAX_COUNT = 20
const campaign = useCampaignStore()
const creatures = useCreatureStore()
const images = useImageCacheStore()
const api = useCompanionApi()

const search = ref('')
const items = ref<CreatureSummary[]>([])
const total = ref(0)
const loading = ref(false)
const error = ref('')
const added = ref('')
const blocked = computed(() => {
  const status = campaign.snapshot?.activeEncounter?.status
  if (!status || status === 'Ended') return 'Este combate já terminou.'
  return campaign.connection === 'offline' ? 'Conexão necessária para adicionar.' : ''
})
const running = computed(() => campaign.snapshot?.activeEncounter?.status === 'Active')
const counts = reactive<Record<string, number>>({})
let sequence = 0
let debounce: ReturnType<typeof setTimeout> | undefined

async function load() {
  const session = campaign.session
  if (!session) return
  const current = ++sequence
  loading.value = true
  error.value = ''
  try {
    const page = await api.listCreatures(session, { q: search.value.trim(), page: 1, pageSize: PAGE })
    if (current !== sequence) return
    items.value = page.items
    total.value = page.total
    void Promise.all(page.items.map(item => images.ensure(item.imageUrl)))
  } catch (exception) {
    if (current === sequence) error.value = (exception as Error).message
  } finally {
    if (current === sequence) loading.value = false
  }
}

watch(search, () => {
  clearTimeout(debounce)
  debounce = setTimeout(load, 250)
})
onMounted(load)
onBeforeUnmount(() => clearTimeout(debounce))

const countOf = (id: string) => counts[id] ?? 1
function step(id: string, delta: number) { counts[id] = Math.min(MAX_COUNT, Math.max(1, countOf(id) + delta)) }
function portrait(item: CreatureSummary) { return item.imageUrl ? images.urls[item.imageUrl] ?? null : null }
function initials(name: string) { return name.split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0]!.toUpperCase()).join('') }

async function add(item: CreatureSummary) {
  const session = campaign.session
  const encounter = campaign.snapshot?.activeEncounter
  if (!session || !encounter || adding.value || blocked.value) return
  const count = countOf(item.id)
  adding.value = item.id
  error.value = ''
  added.value = ''
  try {
    campaign.updateActiveEncounter(await api.addCreaturesToEncounter(session, encounter.id, item.id, count))
    await campaign.refreshBattlemap().catch(() => {})
    added.value = count === 1 ? `${item.name} adicionado ao combate.` : `${count} × ${item.name} adicionados ao combate.`
    counts[item.id] = 1
  } catch (exception) {
    error.value = (exception as Error).message
  } finally {
    adding.value = null
  }
}
</script>

<template>
  <div class="creature-picker" data-creature-picker>
    <label class="creature-search">
      <Icon name="lucide:search" />
      <input v-model="search" type="search" placeholder="Buscar no catálogo: goblin, dragão, ogre…" aria-label="Buscar criatura no catálogo">
    </label>
    <p v-if="blocked" class="field-hint" data-picker-blocked><Icon name="lucide:info" />{{ blocked }}</p>
    <p v-else-if="running" class="field-hint" data-picker-running><Icon name="lucide:swords" />Combate em andamento: cada criatura entra com iniciativa rolada, sem mudar o turno atual.</p>
    <ul class="creature-picker-list" :class="{ loading }">
      <li v-for="item in items" :key="item.id" class="creature-picker-row" :data-picker-creature="item.id">
        <button class="creature-picker-info" type="button" :aria-label="`Ver ficha de ${item.name}`" @click="creatures.open(item.id)">
          <span class="creature-portrait small" :class="{ 'has-image': portrait(item) }">
            <img v-if="portrait(item)" :src="portrait(item)!" alt="" loading="lazy">
            <template v-else>{{ initials(item.name) }}</template>
          </span>
          <span class="creature-card-text">
            <strong>{{ item.name }}</strong>
            <span class="creature-card-stats">
              <span title="Nível de desafio"><Icon name="lucide:crown" />{{ item.challengeRating }}</span>
              <span title="Classe de armadura"><Icon name="lucide:shield" />{{ item.armorClass }}</span>
              <span title="Pontos de vida"><Icon name="lucide:heart" />{{ item.hitPoints }}</span>
            </span>
          </span>
        </button>
        <div class="creature-picker-actions">
          <div class="roll-stepper" role="group" :aria-label="`Quantidade de ${item.name}`">
            <button class="icon-button small" type="button" :disabled="countOf(item.id) <= 1" aria-label="Menos uma" @click="step(item.id, -1)"><Icon name="lucide:minus" /></button>
            <output class="roll-stepper-value" :data-picker-count="item.id">{{ countOf(item.id) }}</output>
            <button class="icon-button small" type="button" :disabled="countOf(item.id) >= MAX_COUNT" aria-label="Mais uma" @click="step(item.id, 1)"><Icon name="lucide:plus" /></button>
          </div>
          <button class="button secondary compact" type="button" :disabled="adding !== null || Boolean(blocked)" :data-picker-add="item.id" @click="add(item)">
            <Icon :name="adding === item.id ? 'lucide:loader-circle' : 'lucide:user-plus'" />Adicionar
          </button>
        </div>
      </li>
    </ul>
    <p v-if="!loading && !items.length && !error" class="field-hint"><Icon name="lucide:search-x" />Nenhuma criatura encontrada. Crie uma na aba Criaturas ou use o modo Manual.</p>
    <p v-else-if="total > items.length" class="field-hint"><Icon name="lucide:info" />Mostrando {{ items.length }} de {{ total }}. Refine a busca para achar outras.</p>
    <p v-if="added" class="field-hint creature-picker-done" role="status"><Icon name="lucide:check" />{{ added }}</p>
    <p v-if="error" class="form-error" role="alert"><Icon name="lucide:circle-alert" />{{ error }}</p>
  </div>
</template>
