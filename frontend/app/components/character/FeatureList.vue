<script setup lang="ts">
import type { CharacterFeature, RestKind } from '~/types/character'
import { RECOVERY_LABEL } from '~/types/character'
import FeatureEditorModal from './FeatureEditorModal.vue'
import { describeEffect } from '~/utils/effects'
import { focusAfterRender, rowOf } from '~/utils/focus'

/**
 * "Características" tab (spec 013, US2): class, species and feat features, with an optional use counter.
 * Every change goes to the server (deltas, never values), queued with the other writes of this device,
 * so two quick taps on -1 are two uses spent.
 */
const character = useCharacterStore()
const expanded = ref(new Set<string>())
const confirmingId = ref<string | null>(null)
const busy = ref(false)
const editor = ref<{ feature: CharacterFeature | null } | null>(null)

const features = computed(() => [...(character.character?.features ?? [])]
  .sort((a, b) => a.sortOrder - b.sortOrder || a.id.localeCompare(b.id)))
const hasCounters = computed(() => features.value.some(feature => feature.data.maxUses !== null))

function toggle(id: string) {
  const next = new Set(expanded.value)
  if (next.has(id)) next.delete(id)
  else next.add(id)
  expanded.value = next
}

/** Runs one write; the buttons stay usable (the queue keeps the order), only the rest buttons wait. */
function act(action: Parameters<typeof character.run>[0]) {
  return character.run(action)
}

const spend = (feature: CharacterFeature, delta: number) => act((api, session) => api.spendFeature(session, feature.id, delta))
const restore = (feature: CharacterFeature) => act((api, session) => api.restoreFeature(session, feature.id))
const move = (feature: CharacterFeature, direction: -1 | 1) => act((api, session) => api.moveFeature(session, feature.id, direction))

async function rest(kind: RestKind) {
  busy.value = true
  await act((api, session) => api.rest(session, kind))
  busy.value = false
}

const root = ref<HTMLElement | null>(null)
const inRow = (id: string, selector: string) => () => rowOf(root.value, 'data-feature-id', id)?.querySelector<HTMLElement>(selector)

async function remove(feature: CharacterFeature) {
  if (confirmingId.value !== feature.id) {
    confirmingId.value = feature.id
    await focusAfterRender(inRow(feature.id, '[data-confirm-remove]'))
    return
  }
  const ok = await act((api, session) => api.removeFeature(session, feature.id))
  if (!ok) return
  confirmingId.value = null
  await focusAfterRender(() => root.value)
}

async function cancelRemove(feature: CharacterFeature) {
  confirmingId.value = null
  await focusAfterRender(inRow(feature.id, '[data-remove-feature]'))
}

function openEditor(feature: CharacterFeature | null) {
  confirmingId.value = null
  editor.value = { feature }
}

function usesLabel(feature: CharacterFeature) {
  return `${feature.data.currentUses} de ${feature.data.maxUses} usos`
}
</script>

<template>
  <section ref="root" class="feature-list" aria-labelledby="feature-list-title" tabindex="-1" data-feature-list>
    <header class="inventory-header">
      <div class="inventory-heading">
        <h2 id="feature-list-title" class="sheet-section-title">Características</h2>
        <p v-if="features.length" class="inventory-meta">{{ features.length === 1 ? '1 característica' : `${features.length} características` }}</p>
      </div>
      <div class="inventory-actions">
        <button type="button" class="button primary small" data-new-feature @click="openEditor(null)">
          <Icon name="lucide:plus" />Nova característica
        </button>
      </div>
    </header>

    <div v-if="hasCounters" class="feature-rests" role="group" aria-label="Descansos">
      <button type="button" class="button ghost small" :disabled="busy" data-rest="Short" @click="rest('Short')">
        <Icon name="lucide:coffee" />Descanso curto
      </button>
      <button type="button" class="button ghost small" :disabled="busy" data-rest="Long" @click="rest('Long')">
        <Icon name="lucide:moon" />Descanso longo
      </button>
    </div>

    <div v-if="!features.length" class="inventory-empty" data-feature-empty>
      <Icon name="lucide:sparkles" size="2rem" />
      <p>Nenhuma característica ainda.</p>
      <p class="sheet-hint">Cadastre as habilidades de classe, raça e talentos, com contador de usos quando tiverem.</p>
    </div>

    <ul v-else class="inventory-list">
      <li
        v-for="(feature, index) in features"
        :key="feature.id"
        class="inventory-item feature-item"
        :data-feature-id="feature.id"
      >
        <div class="inventory-item-main">
          <p class="inventory-item-name">
            <span class="inventory-item-title">{{ feature.data.name }}</span>
            <span v-if="feature.data.source" class="item-badge subtle" data-feature-source>{{ feature.data.source }}</span>
          </p>
          <ul v-if="feature.data.effects?.length" class="feature-effects" aria-label="Efeitos" data-feature-effects>
            <li v-for="(effect, index) in feature.data.effects" :key="index" class="item-badge magic">{{ describeEffect(effect) }}</li>
          </ul>

          <div v-if="feature.data.maxUses !== null" class="feature-counter" data-feature-counter>
            <button
              type="button"
              class="icon-button"
              :aria-label="`Gastar um uso de ${feature.data.name}`"
              :disabled="feature.data.currentUses <= 0"
              data-spend
              @click="spend(feature, -1)"
            ><Icon name="lucide:minus" /></button>
            <output
              class="feature-uses"
              :class="{ empty: feature.data.currentUses === 0 }"
              :aria-label="usesLabel(feature)"
              aria-live="polite"
              data-feature-uses
            ><strong>{{ feature.data.currentUses }}</strong>/{{ feature.data.maxUses }}</output>
            <button
              type="button"
              class="icon-button"
              :aria-label="`Recuperar um uso de ${feature.data.name}`"
              :disabled="feature.data.currentUses >= (feature.data.maxUses ?? 0)"
              data-regain
              @click="spend(feature, 1)"
            ><Icon name="lucide:plus" /></button>
            <button
              type="button"
              class="button ghost small"
              :aria-label="`Restaurar todos os usos de ${feature.data.name}`"
              :disabled="feature.data.currentUses >= (feature.data.maxUses ?? 0)"
              data-restore
              @click="restore(feature)"
            ><Icon name="lucide:rotate-ccw" />Restaurar</button>
            <span class="sheet-hint feature-recovery" data-feature-recovery>{{ RECOVERY_LABEL[feature.data.recovery] }}</span>
          </div>

          <button
            v-if="feature.data.description"
            type="button"
            class="spell-expand"
            :aria-expanded="expanded.has(feature.id)"
            :aria-controls="`feature-description-${feature.id}`"
            data-feature-expand
            @click="toggle(feature.id)"
          >
            <Icon :name="expanded.has(feature.id) ? 'lucide:chevron-up' : 'lucide:chevron-down'" />
            {{ expanded.has(feature.id) ? 'Esconder descrição' : 'Ver descrição' }}
          </button>
          <p v-if="expanded.has(feature.id)" :id="`feature-description-${feature.id}`" class="spell-description" data-feature-description>{{ feature.data.description }}</p>
        </div>

        <div class="inventory-item-controls">
          <button type="button" class="icon-button" :aria-label="`Mover ${feature.data.name} para cima`" :disabled="index === 0" data-move-up @click="move(feature, -1)">
            <Icon name="lucide:arrow-up" />
          </button>
          <button type="button" class="icon-button" :aria-label="`Mover ${feature.data.name} para baixo`" :disabled="index === features.length - 1" data-move-down @click="move(feature, 1)">
            <Icon name="lucide:arrow-down" />
          </button>
          <button type="button" class="icon-button" :aria-label="`Editar ${feature.data.name}`" data-edit-feature @click="openEditor(feature)">
            <Icon name="lucide:pencil" />
          </button>
          <template v-if="confirmingId === feature.id">
            <span class="inventory-confirm-label">Remover?</span>
            <button type="button" class="button ghost danger small" data-confirm-remove @click="remove(feature)">Confirmar</button>
            <button type="button" class="icon-button" aria-label="Cancelar remoção" data-cancel-remove @click="cancelRemove(feature)"><Icon name="lucide:x" /></button>
          </template>
          <button v-else type="button" class="icon-button danger" :aria-label="`Remover ${feature.data.name}`" data-remove-feature @click="remove(feature)">
            <Icon name="lucide:trash-2" />
          </button>
        </div>
      </li>
    </ul>

    <FeatureEditorModal v-if="editor" :feature="editor.feature" @close="editor = null" />
  </section>
</template>
