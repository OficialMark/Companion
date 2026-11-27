<script setup lang="ts">
import type { CreatureSize, CreatureTokenOption } from '~/types/creatures'
import type { CatalogItemSummary, ItemCategory } from '~/types/character'
import CreaturePicker from '~/components/creatures/CreaturePicker.vue'

/**
 * Spec 016: drop a compendium creature or a catalog item on the board as a token. Every participant
 * gets it; the token lands in the reserve, owned by whoever added it. The master can also send a
 * creature into combat (initiative, HP) through the existing picker.
 */
const props = withDefaults(defineProps<{ isMaster?: boolean }>(), { isMaster: false })

type Tab = 'creatures' | 'items'
type CreatureMode = 'token' | 'combat'
const PAGE = 8
const SIZE_LABEL: Record<CreatureSize, string> = {
  Tiny: 'Miúda', Small: 'Pequena', Medium: 'Média', Large: 'Grande', Huge: 'Enorme', Gargantuan: 'Imensa'
}
const CATEGORY: Record<ItemCategory, { label: string; icon: string }> = {
  Weapon: { label: 'Arma', icon: 'lucide:sword' },
  Armor: { label: 'Armadura', icon: 'lucide:shirt' },
  Shield: { label: 'Escudo', icon: 'lucide:shield' },
  Gear: { label: 'Equipamento', icon: 'lucide:backpack' },
  Misc: { label: 'Diverso', icon: 'lucide:package' },
  Potion: { label: 'Poção', icon: 'lucide:flask-conical' }
}

const campaign = useCampaignStore()
const images = useImageCacheStore()
const api = useCompanionApi()
const characterApi = useCharacterApi()

const tab = ref<Tab>('creatures')
const creatureMode = ref<CreatureMode>('token')
const search = ref('')
const creatures = ref<CreatureTokenOption[]>([])
const items = ref<CatalogItemSummary[]>([])
const total = ref(0)
const loading = ref(false)
const error = ref('')
const added = ref('')
const adding = ref<string | null>(null)
const offline = computed(() => campaign.connection === 'offline')
const showsCombatPicker = computed(() => props.isMaster && tab.value === 'creatures' && creatureMode.value === 'combat')
const shown = computed(() => tab.value === 'creatures' ? creatures.value.length : items.value.length)

let sequence = 0
let debounce: ReturnType<typeof setTimeout> | undefined

async function load() {
  const session = campaign.session
  if (!session || showsCombatPicker.value) return
  const current = ++sequence
  loading.value = true
  error.value = ''
  try {
    const q = search.value.trim()
    if (tab.value === 'creatures') {
      const page = await api.listCreatureTokenOptions(session, q, PAGE)
      if (current !== sequence) return
      creatures.value = page.items
      total.value = page.total
      void Promise.all(page.items.map(item => images.ensure(item.imageUrl)))
    } else {
      const page = await characterApi.listCatalogItems(session, { q, page: 1, pageSize: PAGE })
      if (current !== sequence) return
      items.value = page.items
      total.value = page.total
    }
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
watch([tab, creatureMode], () => {
  added.value = ''
  void load()
})
onMounted(load)
onBeforeUnmount(() => clearTimeout(debounce))

function portrait(item: CreatureTokenOption) { return item.imageUrl ? images.urls[item.imageUrl] ?? null : null }
function initials(name: string) { return name.split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0]!.toUpperCase()).join('') }

async function add(kind: 'creature' | 'item', id: string, name: string) {
  if (adding.value || offline.value) return
  adding.value = id
  added.value = ''
  const ok = await campaign.createMarker(kind === 'creature' ? { creatureId: id } : { itemId: id })
  adding.value = null
  if (ok) added.value = `${name} foi para a reserva. Selecione-o e escolha uma célula.`
  else error.value = campaign.error || 'Não foi possível adicionar o token.'
}
</script>

<template>
  <section class="token-source-picker" data-token-source-picker aria-label="Adicionar token do compêndio">
    <div class="segmented token-source-tabs" role="tablist" aria-label="Origem do token">
      <button type="button" role="tab" :aria-selected="tab === 'creatures'" :class="{ active: tab === 'creatures' }" data-token-source-tab="creatures" @click="tab = 'creatures'">
        <Icon name="lucide:paw-print" />Criaturas
      </button>
      <button type="button" role="tab" :aria-selected="tab === 'items'" :class="{ active: tab === 'items' }" data-token-source-tab="items" @click="tab = 'items'">
        <Icon name="lucide:package" />Itens
      </button>
    </div>

    <div v-if="isMaster && tab === 'creatures'" class="token-source-mode" role="radiogroup" aria-label="Como a criatura entra no mapa">
      <label :class="{ active: creatureMode === 'token' }">
        <input v-model="creatureMode" type="radio" value="token" name="token-source-mode" data-token-source-mode="token">
        <Icon name="lucide:map-pin" /><span>Só token</span>
      </label>
      <label :class="{ active: creatureMode === 'combat' }">
        <input v-model="creatureMode" type="radio" value="combat" name="token-source-mode" data-token-source-mode="combat">
        <Icon name="lucide:swords" /><span>No combate</span>
      </label>
    </div>

    <CreaturePicker v-if="showsCombatPicker" />
    <template v-else>
      <label class="creature-search">
        <Icon name="lucide:search" />
        <input
          v-model="search"
          type="search"
          data-token-source-search
          :placeholder="tab === 'creatures' ? 'Buscar criatura: lobo, goblin, dragão…' : 'Buscar item: corda, baú, poção…'"
          :aria-label="tab === 'creatures' ? 'Buscar criatura no compêndio' : 'Buscar item no catálogo'"
        >
      </label>

      <ul class="token-source-list" :class="{ loading }">
        <template v-if="tab === 'creatures'">
          <li v-for="item in creatures" :key="item.id" class="token-source-row" :data-token-source-creature="item.id">
            <span class="creature-portrait small" :class="{ 'has-image': portrait(item) }">
              <img v-if="portrait(item)" :src="portrait(item)!" alt="" loading="lazy">
              <template v-else>{{ initials(item.name) }}</template>
            </span>
            <span class="token-source-text">
              <strong>{{ item.name }}</strong>
              <small>{{ SIZE_LABEL[item.size] }} · {{ item.type }}</small>
            </span>
            <button class="button secondary compact" type="button" :disabled="adding !== null || offline" :aria-label="`Adicionar ${item.name} ao mapa`" @click="add('creature', item.id, item.name)">
              <Icon :name="adding === item.id ? 'lucide:loader-circle' : 'lucide:map-pin-plus'" /><span>Adicionar</span>
            </button>
          </li>
        </template>
        <template v-else>
          <li v-for="item in items" :key="item.id" class="token-source-row" :data-token-source-item="item.id">
            <span class="token-source-item-icon" aria-hidden="true"><Icon :name="CATEGORY[item.category]?.icon ?? 'lucide:package'" /></span>
            <span class="token-source-text">
              <strong>{{ item.name }}</strong>
              <small>{{ CATEGORY[item.category]?.label ?? 'Item' }}<template v-if="item.rarity"> · {{ item.rarity }}</template></small>
            </span>
            <button class="button secondary compact" type="button" :disabled="adding !== null || offline" :aria-label="`Adicionar ${item.name} ao mapa`" @click="add('item', item.id, item.name)">
              <Icon :name="adding === item.id ? 'lucide:loader-circle' : 'lucide:map-pin-plus'" /><span>Adicionar</span>
            </button>
          </li>
        </template>
      </ul>

      <p v-if="offline" class="field-hint"><Icon name="lucide:wifi-off" />Conexão necessária para adicionar.</p>
      <p v-else-if="!loading && !shown && !error" class="field-hint"><Icon name="lucide:search-x" />Nada encontrado. Tente outro nome.</p>
      <p v-else-if="total > shown" class="field-hint"><Icon name="lucide:info" />Mostrando {{ shown }} de {{ total }}. Refine a busca para achar outros.</p>
      <p v-if="added" class="field-hint token-source-done" role="status" data-token-source-done><Icon name="lucide:check" />{{ added }}</p>
      <p v-if="error" class="form-error" role="alert"><Icon name="lucide:circle-alert" />{{ error }}</p>
    </template>
  </section>
</template>
