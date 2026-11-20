<script setup lang="ts">
import CreatureLibrary from '~/components/creatures/CreatureLibrary.vue'
import CatalogPicker from '~/components/character/CatalogPicker.vue'

/**
 * The master's Compêndio (spec 012, US2): Criaturas, Magias and Itens as tabs. The active tab lives in
 * the URL (`?aba=magias`) so a reload keeps it; arrows, Home and End move between tabs (WAI-ARIA tabs).
 */
type Tab = 'criaturas' | 'magias' | 'itens'
const TABS: Array<{ key: Tab; label: string; icon: string }> = [
  { key: 'criaturas', label: 'Criaturas', icon: 'lucide:skull' },
  { key: 'magias', label: 'Magias', icon: 'lucide:wand-sparkles' },
  { key: 'itens', label: 'Itens', icon: 'lucide:backpack' }
]

const route = useRoute()
const router = useRouter()
const active = computed<Tab>(() => {
  const asked = route.query.aba
  return TABS.some(tab => tab.key === asked) ? asked as Tab : 'criaturas'
})
const buttons = ref<HTMLButtonElement[]>([])
/** Follows the URL, but moves at once on a key press: two quick arrows must move two tabs. */
const shown = ref<Tab>(active.value)
watch(active, value => { shown.value = value })

function select(tab: Tab, focus = false) {
  shown.value = tab
  if (tab !== active.value) {
    const query = { ...route.query }
    if (tab === 'criaturas') delete query.aba
    else query.aba = tab
    void router.replace({ query })
  }
  if (focus) void nextTick(() => buttons.value[TABS.findIndex(entry => entry.key === tab)]?.focus())
}

function onKeydown(event: KeyboardEvent) {
  const index = TABS.findIndex(tab => tab.key === shown.value)
  const next = { ArrowRight: index + 1, ArrowLeft: index - 1, Home: 0, End: TABS.length - 1 }[event.key]
  if (next === undefined) return
  event.preventDefault()
  select(TABS[(next + TABS.length) % TABS.length]!.key, true)
}
</script>

<template>
  <div class="compendium" data-compendium>
    <header class="compendium-header">
      <h1 id="compendium-title">Compêndio</h1>
      <div class="compendium-tabs" role="tablist" aria-labelledby="compendium-title" @keydown="onKeydown">
        <button
          v-for="tab in TABS"
          :id="`compendium-tab-${tab.key}`"
          :key="tab.key"
          ref="buttons"
          type="button"
          role="tab"
          class="compendium-tab"
          :aria-selected="shown === tab.key"
          :aria-controls="`compendium-panel-${tab.key}`"
          :tabindex="shown === tab.key ? 0 : -1"
          :data-compendium-tab="tab.key"
          @click="select(tab.key)"
        ><Icon :name="tab.icon" />{{ tab.label }}</button>
      </div>
    </header>
    <!-- One panel per tab (aria-controls always points somewhere); only the shown one is rendered. -->
    <div
      v-for="tab in TABS"
      :id="`compendium-panel-${tab.key}`"
      :key="tab.key"
      role="tabpanel"
      :aria-labelledby="`compendium-tab-${tab.key}`"
      :hidden="shown !== tab.key"
      :data-compendium-panel="shown === tab.key ? tab.key : undefined"
    >
      <template v-if="shown === tab.key">
        <CreatureLibrary v-if="tab.key === 'criaturas'" embedded />
        <CatalogPicker v-else-if="tab.key === 'magias'" mode="spells" browse />
        <CatalogPicker v-else mode="items" browse />
      </template>
    </div>
  </div>
</template>
