<script setup lang="ts">
/**
 * Top-bar navigation (spec 009 US3, spec 011 FR-001): the master switches between the table and the
 * creature compendium; a player switches between the table and their own character sheet.
 */
const store = useCampaignStore()
const { view, go } = useAppView()
const isPlayer = computed(() => store.session?.role === 'Player')
</script>

<template>
  <nav v-if="store.isMaster" class="view-nav" aria-label="Áreas do mestre" data-master-nav>
    <button type="button" class="view-nav-item" data-view="mesa" :aria-current="view === 'mesa' ? 'page' : undefined" @click="go('mesa')">
      <Icon name="lucide:swords" /><span>Mesa</span>
    </button>
    <button type="button" class="view-nav-item" data-view="compendio" :aria-current="view === 'compendio' ? 'page' : undefined" @click="go('compendio')">
      <Icon name="lucide:book-open" /><span>Compêndio</span>
    </button>
    <button type="button" class="view-nav-item" data-view="campanha" :aria-current="view === 'campanha' ? 'page' : undefined" @click="go('campanha')">
      <Icon name="lucide:book-marked" /><span>Campanha</span>
    </button>
  </nav>
  <nav v-else-if="isPlayer" class="view-nav" aria-label="Áreas do jogador" data-player-nav>
    <button type="button" class="view-nav-item" data-view="mesa" :aria-current="view === 'mesa' ? 'page' : undefined" @click="go('mesa')">
      <Icon name="lucide:swords" /><span>Mesa</span>
    </button>
    <button type="button" class="view-nav-item" data-view="ficha" :aria-current="view === 'ficha' ? 'page' : undefined" @click="go('ficha')">
      <Icon name="lucide:scroll-text" /><span>Ficha</span>
    </button>
    <button type="button" class="view-nav-item" data-view="campanha" :aria-current="view === 'campanha' ? 'page' : undefined" @click="go('campanha')">
      <Icon name="lucide:book-marked" /><span>Campanha</span>
    </button>
  </nav>
</template>
