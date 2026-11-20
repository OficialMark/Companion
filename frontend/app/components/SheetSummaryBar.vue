<script setup lang="ts">
import SheetOverviewModal from '~/components/SheetOverviewModal.vue'
import { useSheetAutosaveGuards } from '~/stores/character'

/**
 * The player's sheet summary at the foot of the Mesa (spec 012, US3). It replaces the old "Perfil de
 * combate" form: the sheet is the only source of initiative and hit points (spec 011), so this shows
 * the key numbers and opens the overview, where hit points go up and down.
 */
const store = useCampaignStore()
const character = useCharacterStore()
useSheetAutosaveGuards()
const open = ref(false)
const trigger = ref<HTMLButtonElement | null>(null)

// Every player has a sheet (spec 011, global per name): opening the Mesa loads it, creating it once.
watch(() => [store.isMaster, store.session?.participantToken] as const, ([master, token]) => {
  if (!master && token && !character.character && !character.loading) void character.load()
}, { immediate: true })

const sheet = computed(() => character.draft)
const stats = computed(() => character.stats)
const initiative = computed(() => {
  const value = stats.value?.initiative.value ?? 0
  return value >= 0 ? `+${value}` : String(value)
})
function close() {
  open.value = false
  void nextTick(() => trigger.value?.focus())
}
</script>

<template>
  <section v-if="!store.isMaster" class="sheet-summary" aria-label="Resumo da sua ficha" data-sheet-summary>
    <button ref="trigger" class="sheet-summary-button" type="button" :disabled="!sheet || !stats" aria-haspopup="dialog" :aria-expanded="open" data-open-overview @click="open = true">
      <span class="sheet-summary-name"><Icon name="lucide:scroll-text" />{{ sheet?.characterName || store.session?.displayName }}</span>
      <template v-if="sheet && stats">
        <span class="sheet-summary-stat" data-summary-hp><Icon name="lucide:heart" />{{ sheet.currentHitPoints }}/{{ stats.maxHitPoints.value }}<small v-if="sheet.temporaryHitPoints"> +{{ sheet.temporaryHitPoints }}</small></span>
        <span class="sheet-summary-stat" data-summary-ac><Icon name="lucide:shield" />CA {{ stats.armorClass.value }}</span>
        <span class="sheet-summary-stat" data-summary-initiative><Icon name="lucide:zap" />{{ initiative }}</span>
      </template>
      <span v-else class="sheet-summary-stat"><Icon name="lucide:loader-circle" class="spin" />Abrindo a ficha…</span>
      <Icon name="lucide:chevron-up" class="sheet-summary-chevron" />
    </button>
    <SheetOverviewModal v-if="open" @close="close" />
  </section>
</template>
