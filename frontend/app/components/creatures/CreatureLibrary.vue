<script setup lang="ts">
import { CREATURE_SIZE_LABELS, ORIGIN_LABELS, type CreatureOrigin, type CreatureSize, type CreatureSummary } from '~/types/creatures'

/** Creature library tab (spec 008, US3): search, filters, cards, pagination, credit. */
const creatures = useCreatureStore()
const images = useImageCacheStore()
const search = ref(creatures.query.q)
let debounce: ReturnType<typeof setTimeout> | undefined

const sizes = Object.entries(CREATURE_SIZE_LABELS) as Array<[CreatureSize, string]>
const origins = Object.entries(ORIGIN_LABELS) as Array<[CreatureOrigin, string]>
const challengeSteps = ['0', '1/8', '1/4', '1/2', ...Array.from({ length: 30 }, (_, index) => String(index + 1))]
const challengeValue = (value: string) => value.includes('/') ? Number(value.split('/')[0]) / Number(value.split('/')[1]) : Number(value)
const crLabel = (value: number | null) => value == null ? '' : challengeSteps.find(step => Math.abs(challengeValue(step) - value) < 1e-6) ?? ''
// "Minhas criaturas" (spec 009) is the existing Custom origin filter, one click away.
const mine = computed(() => creatures.query.origin === 'Custom')
function toggleMine() { creatures.setQuery({ origin: mine.value ? '' : 'Custom' }) }
const filtersActive = computed(() => Boolean(creatures.query.size || creatures.query.type || creatures.query.origin || creatures.query.crMin != null || creatures.query.crMax != null))

watch(search, value => {
  clearTimeout(debounce)
  debounce = setTimeout(() => creatures.setQuery({ q: value.trim() }), 250)
})

function initials(name: string) {
  return name.split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0]!.toUpperCase()).join('')
}
function portrait(item: CreatureSummary) { return item.imageUrl ? images.urls[item.imageUrl] ?? null : null }
function onCr(field: 'crMin' | 'crMax', event: Event) {
  const value = (event.target as HTMLSelectElement).value
  creatures.setQuery({ [field]: value === '' ? null : challengeValue(value) })
}
function clearFilters() {
  search.value = ''
  creatures.setQuery({ q: '', size: '', type: '', origin: '', crMin: null, crMax: null })
}

onMounted(() => {
  void creatures.loadTypes()
  if (!creatures.items.length) void creatures.load()
})
onBeforeUnmount(() => clearTimeout(debounce))
</script>

<template>
  <section class="panel creature-library" aria-labelledby="creature-library-title" data-creature-library>
    <div class="panel-heading">
      <div>
        <p class="eyebrow"><Icon name="lucide:book-open" />Bestiário da mesa</p>
        <h1 id="creature-library-title">Compêndio</h1>
        <p class="creature-library-lead">Consulte, filtre e edite as criaturas do compêndio, ou crie as suas.</p>
      </div>
      <div class="creature-library-actions">
        <button class="button ghost compact creature-mine" type="button" data-mine :aria-pressed="mine" @click="toggleMine"><Icon name="lucide:user-round-pen" /><span>Minhas criaturas</span></button>
        <button class="button primary compact" type="button" data-new-creature @click="creatures.newCreature()"><Icon name="lucide:plus" /><span>Nova criatura</span></button>
      </div>
    </div>

    <div class="creature-filters">
      <label class="creature-search">
        <Icon name="lucide:search" />
        <input v-model="search" type="search" inputmode="search" placeholder="Buscar por nome (PT ou EN)" aria-label="Buscar criatura" data-creature-search>
      </label>
      <select :value="creatures.query.size" aria-label="Tamanho" @change="creatures.setQuery({ size: ($event.target as HTMLSelectElement).value as CreatureSize })">
        <option value="">Tamanho</option>
        <option v-for="[value, label] in sizes" :key="value" :value="value">{{ label }}</option>
      </select>
      <select :value="creatures.query.type" aria-label="Tipo" @change="creatures.setQuery({ type: ($event.target as HTMLSelectElement).value })">
        <option value="">Tipo</option>
        <option v-for="type in creatures.types" :key="type" :value="type">{{ type }}</option>
      </select>
      <div class="creature-cr" role="group" aria-label="Nível de desafio">
        <select aria-label="ND mínimo" :value="crLabel(creatures.query.crMin)" @change="onCr('crMin', $event)"><option value="">ND mín.</option><option v-for="cr in challengeSteps" :key="cr" :value="cr">{{ cr }}</option></select>
        <select aria-label="ND máximo" :value="crLabel(creatures.query.crMax)" @change="onCr('crMax', $event)"><option value="">ND máx.</option><option v-for="cr in challengeSteps" :key="cr" :value="cr">{{ cr }}</option></select>
      </div>
      <select :value="creatures.query.origin" aria-label="Origem" @change="creatures.setQuery({ origin: ($event.target as HTMLSelectElement).value as CreatureOrigin })">
        <option value="">Origem</option>
        <option v-for="[value, label] in origins" :key="value" :value="value">{{ label }}</option>
      </select>
      <button v-if="filtersActive || search" class="button ghost compact creature-clear" type="button" @click="clearFilters"><Icon name="lucide:x" />Limpar</button>
    </div>

    <p class="creature-count" aria-live="polite">
      <template v-if="creatures.loading"><Icon name="lucide:loader-circle" class="spin" />Buscando…</template>
      <template v-else>{{ creatures.total.toLocaleString('pt-BR') }} criatura{{ creatures.total === 1 ? '' : 's' }}</template>
    </p>
    <p v-if="creatures.error" class="form-error" role="alert"><Icon name="lucide:circle-alert" />{{ creatures.error }}</p>

    <ul class="creature-grid-list" :class="{ loading: creatures.loading }">
      <li v-for="item in creatures.items" :key="item.id">
        <button class="creature-card" type="button" :data-creature-id="item.id" @click="creatures.open(item.id)">
          <span class="creature-portrait" :class="{ 'has-image': portrait(item) }">
            <img v-if="portrait(item)" :src="portrait(item)!" alt="" loading="lazy">
            <template v-else>{{ initials(item.name) }}</template>
          </span>
          <span class="creature-card-text">
            <strong>{{ item.name }}</strong>
            <small>{{ CREATURE_SIZE_LABELS[item.size] }} · {{ item.type }}</small>
            <span class="creature-card-stats">
              <span title="Nível de desafio"><Icon name="lucide:crown" />{{ item.challengeRating }}</span>
              <span title="Classe de armadura"><Icon name="lucide:shield" />{{ item.armorClass }}</span>
              <span title="Pontos de vida"><Icon name="lucide:heart" />{{ item.hitPoints }}</span>
            </span>
          </span>
          <span v-if="item.origin !== 'Compendium'" class="creature-badge" :data-origin="item.origin">{{ ORIGIN_LABELS[item.origin] }}</span>
        </button>
      </li>
    </ul>
    <div v-if="!creatures.loading && !creatures.items.length" class="empty-state">
      <span class="state-icon"><Icon name="lucide:search-x" size="1.6rem" /></span>
      <template v-if="mine && !search">
        <p data-mine-empty>Você ainda não criou nenhuma criatura nesta mesa.</p>
        <button class="button primary compact" type="button" @click="creatures.newCreature()"><Icon name="lucide:plus" />Criar a primeira</button>
      </template>
      <p v-else>Nenhuma criatura encontrada com esses filtros.</p>
    </div>

    <nav v-if="creatures.pageCount > 1" class="creature-pagination" aria-label="Páginas">
      <button class="button ghost compact" type="button" :disabled="creatures.query.page <= 1 || creatures.loading" @click="creatures.setQuery({ page: creatures.query.page - 1 })"><Icon name="lucide:chevron-left" />Anterior</button>
      <span>Página {{ creatures.query.page }} de {{ creatures.pageCount }}</span>
      <button class="button ghost compact" type="button" :disabled="creatures.query.page >= creatures.pageCount || creatures.loading" @click="creatures.setQuery({ page: creatures.query.page + 1 })">Próxima<Icon name="lucide:chevron-right" /></button>
    </nav>

    <p class="creature-credit">
      <Icon name="lucide:book-marked" />
      <!-- Spec 011, FR-040: the Pocket DM team authorized the use and asked for no credit. The SRD license notice stays. -->
      Conteúdo SRD sob <a href="https://creativecommons.org/licenses/by/4.0/" target="_blank" rel="noopener">CC BY 4.0</a> (Wizards of the Coast).
    </p>
  </section>
</template>
