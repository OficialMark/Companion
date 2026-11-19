<script setup lang="ts">
/**
 * The player's "Ficha" screen (spec 011, US1). Tabs for the sheet, the bag, spells and saved rolls.
 * Loads the caller's own character; the server decides whose it is from the session (FR-003).
 */
const character = useCharacterStore()
const store = useCampaignStore()

type Tab = 'ficha' | 'mochila' | 'magias' | 'rolagens'
const TABS: { key: Tab; label: string; icon: string }[] = [
  { key: 'ficha', label: 'Ficha', icon: 'lucide:scroll-text' },
  { key: 'mochila', label: 'Mochila', icon: 'lucide:backpack' },
  { key: 'magias', label: 'Magias', icon: 'lucide:wand-sparkles' },
  { key: 'rolagens', label: 'Rolagens', icon: 'lucide:dices' }
]
const tab = ref<Tab>('ficha')

const SAVE_LABEL: Record<string, string> = {
  idle: '', saving: 'Salvando…', saved: 'Salvo', error: 'Não foi possível salvar', conflict: 'A ficha mudou em outro aparelho'
}

onMounted(() => { if (!character.character) void character.load() })
onBeforeUnmount(() => { void character.flush() })

const title = computed(() => character.draft?.characterName || store.session?.displayName || 'Seu personagem')
const subtitle = computed(() => {
  const draft = character.draft
  if (!draft) return ''
  return [draft.species, draft.className && `${draft.className} ${draft.level}`, draft.background].filter(Boolean).join(' · ')
})
</script>

<template>
  <section class="character-view" aria-labelledby="character-title" data-character-view>
    <header class="character-header">
      <div class="character-heading">
        <p class="eyebrow"><Icon name="lucide:user-round" />Ficha pessoal</p>
        <h1 id="character-title">{{ title }}</h1>
        <p v-if="subtitle" class="character-subtitle">{{ subtitle }}</p>
      </div>
      <p
        class="save-state"
        :data-save-state="character.saveState"
        role="status"
        aria-live="polite"
      >
        <template v-if="character.saveState === 'saving'"><Icon name="lucide:loader-circle" class="spin" />{{ SAVE_LABEL.saving }}</template>
        <template v-else-if="character.saveState === 'saved'"><Icon name="lucide:check" />{{ SAVE_LABEL.saved }}</template>
        <template v-else-if="character.saveState === 'error'">
          <Icon name="lucide:circle-alert" />{{ SAVE_LABEL.error }}
          <button type="button" class="button ghost small" data-retry-save @click="character.flush()">Tentar de novo</button>
        </template>
        <template v-else-if="character.saveState === 'conflict'">
          <Icon name="lucide:refresh-ccw" />{{ SAVE_LABEL.conflict }}
          <button type="button" class="button ghost small" data-reload-sheet @click="character.load()">Recarregar ficha</button>
        </template>
      </p>
    </header>

    <div v-if="character.loading && !character.character" class="loading-state" role="status">
      <Icon name="lucide:loader-circle" class="spin" size="2rem" /><span>Abrindo a ficha…</span>
    </div>
    <div v-else-if="!character.character" class="fatal-state">
      <h2>Não foi possível abrir a ficha</h2>
      <p>{{ character.error }}</p>
      <button class="button primary" type="button" @click="character.load()"><Icon name="lucide:refresh-cw" />Tentar novamente</button>
    </div>
    <template v-else>
      <nav class="character-tabs" role="tablist" aria-label="Partes da ficha">
        <button
          v-for="item in TABS"
          :key="item.key"
          type="button"
          role="tab"
          class="character-tab"
          :data-tab="item.key"
          :aria-selected="tab === item.key"
          @click="tab = item.key"
        >
          <Icon :name="item.icon" /><span>{{ item.label }}</span>
        </button>
      </nav>
      <p v-if="character.error && character.saveState !== 'conflict'" class="form-error" role="alert">{{ character.error }}</p>
      <ul v-if="character.notices.length" class="character-notices" role="status">
        <li v-for="notice in character.notices" :key="notice"><Icon name="lucide:info" />{{ notice }}</li>
      </ul>
      <div class="character-tab-panel" role="tabpanel">
        <SheetCoreTab v-if="tab === 'ficha'" />
        <InventoryPanel v-else-if="tab === 'mochila'" />
        <SpellList v-else-if="tab === 'magias'" />
        <SavedRollList v-else />
      </div>
    </template>
  </section>
</template>
