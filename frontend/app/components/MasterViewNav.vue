<script setup lang="ts">
/** Master's top-bar navigation between the table and the creature compendium (spec 009, US3). */
const store = useCampaignStore()
const { view, go } = useMasterView()
</script>

<template>
  <nav v-if="store.isMaster" class="view-nav" aria-label="Áreas do mestre" data-master-nav>
    <button type="button" class="view-nav-item" data-view="mesa" :aria-current="view === 'mesa' ? 'page' : undefined" @click="go('mesa')">
      <Icon name="lucide:swords" /><span>Mesa</span>
    </button>
    <button type="button" class="view-nav-item" data-view="compendio" :aria-current="view === 'compendio' ? 'page' : undefined" @click="go('compendio')">
      <Icon name="lucide:book-open" /><span>Compêndio</span>
    </button>
  </nav>
</template>
