<script setup lang="ts">
import { toRaw } from 'vue'
import type { InventoryItem, InventoryItemData, SuppressedEffect } from '~/types/character'
import { CATEGORY_LABEL } from '~/types/character'
import ItemEditorModal, { ARMOR_TYPE_LABEL, CATEGORY_ORDER } from './ItemEditorModal.vue'
import CatalogPicker from './CatalogPicker.vue'
import { focusAfterRender, rowOf } from '~/utils/focus'

/**
 * The player's bag (spec 011, US3). Same component in the "Ficha" screen and in a game-mode modal,
 * so it makes no layout assumption. Every write goes through the character store and comes back
 * recalculated by the server; nothing here applies a rule.
 */
const props = withDefaults(defineProps<{
  /** Denser rows, for the game-mode modal. */
  compact?: boolean
  /**
   * Show the store's notices and errors inside the panel. Left undefined, the panel shows them
   * unless it sits inside the "Ficha" screen, which already lists them above the tabs.
   */
  showFeedback?: boolean
  /** Hide the "Mochila" heading when the container already shows it (the game-mode modal). */
  hideTitle?: boolean
}>(), { compact: false, showFeedback: undefined, hideTitle: false })

const character = useCharacterStore()
const root = ref<HTMLElement | null>(null)
const insideSheet = ref(false)
const busyId = ref<string | null>(null)
const confirmingId = ref<string | null>(null)
const editor = ref<{ item: InventoryItem | null } | null>(null)
const pickerOpen = ref(false)

const EQUIPPABLE = new Set(['Weapon', 'Armor', 'Shield', 'Gear'])
const TAKES_PROFICIENCY = new Set(['Weapon', 'Armor', 'Shield'])

const items = computed(() => character.character?.items ?? [])
const groups = computed(() => CATEGORY_ORDER
  .map(category => ({
    category,
    label: CATEGORY_LABEL[category],
    items: items.value.filter(item => item.data.category === category).sort((a, b) => a.sortOrder - b.sortOrder)
  }))
  .filter(group => group.items.length > 0))

const suppressedByItem = computed(() => {
  const map = new Map<string, SuppressedEffect[]>()
  for (const entry of character.stats?.suppressedEffects ?? []) {
    map.set(entry.itemId, [...(map.get(entry.itemId) ?? []), entry])
  }
  return map
})

const totalWeight = computed(() => {
  const sum = items.value.reduce((total, item) => total + (item.data.weight ?? 0) * (item.data.quantity ?? 0), 0)
  return Math.round(sum * 100) / 100
})

const feedback = computed(() => props.showFeedback ?? !insideSheet.value)

function signed(value: number) { return value >= 0 ? `+${value}` : `${value}` }
function formatNumber(value: number) { return value.toLocaleString('pt-BR', { maximumFractionDigits: 2 }) }

/** A short line of the numbers that matter at the table. Display only, straight from the item data. */
function summary(data: InventoryItemData): string {
  if (data.category === 'Weapon' && data.weapon) {
    const parts = [data.weapon.damageDice, data.weapon.damageType].filter(Boolean).join(' ')
    const versatile = data.weapon.versatileDice ? ` (${data.weapon.versatileDice} com duas mãos)` : ''
    return `${parts}${versatile}`
  }
  if (data.category === 'Armor' && data.armor) return `CA ${data.armor.baseAc}, armadura ${ARMOR_TYPE_LABEL[data.armor.type]}`
  if (data.category === 'Shield' && data.shield) return `+${data.shield.acBonus} CA`
  return ''
}

function effectsLabel(count: number) { return count === 1 ? '1 efeito' : `${count} efeitos` }

async function toggleEquip(item: InventoryItem) {
  busyId.value = item.id
  await character.run((api, session) => api.equipItem(session, item.id, !item.data.equipped))
  busyId.value = null
}

async function toggleProficient(item: InventoryItem) {
  busyId.value = item.id
  const data: InventoryItemData = { ...structuredClone(toRaw(item.data)), proficient: !item.data.proficient }
  await character.run((api, session) => api.saveItem(session, item.id, data, item.version))
  busyId.value = null
}

// Keyboard focus follows the inline confirmation (FR-039): onto "Confirmar", back to the trash can on
// cancel, and onto the list after the item is gone.
const inRow = (id: string, selector: string) => () => rowOf(root.value, 'data-item-id', id)?.querySelector<HTMLElement>(selector)

async function remove(item: InventoryItem) {
  if (confirmingId.value !== item.id) {
    confirmingId.value = item.id
    await focusAfterRender(inRow(item.id, '[data-confirm-remove]'))
    return
  }
  busyId.value = item.id
  const ok = await character.run((api, session) => api.removeItem(session, item.id))
  busyId.value = null
  if (!ok) return
  confirmingId.value = null
  await focusAfterRender(() => root.value)
}

async function cancelRemove(item: InventoryItem) {
  confirmingId.value = null
  await focusAfterRender(inRow(item.id, '[data-remove-item]'))
}

function openEditor(item: InventoryItem | null) {
  confirmingId.value = null
  editor.value = { item }
}

onMounted(() => { insideSheet.value = Boolean(root.value?.closest('[data-character-view]')) })
</script>

<template>
  <section
    ref="root"
    class="inventory"
    :class="{ 'inventory--compact': compact }"
    :aria-labelledby="hideTitle ? undefined : 'inventory-title'"
    :aria-label="hideTitle ? 'Itens da mochila' : undefined"
    tabindex="-1"
    data-inventory
  >
    <header class="inventory-header">
      <div class="inventory-heading">
        <h2 v-if="!hideTitle" id="inventory-title" class="sheet-section-title">Mochila</h2>
        <p v-if="items.length" class="inventory-meta">
          {{ items.length === 1 ? '1 item' : `${items.length} itens` }}<template v-if="totalWeight">, {{ formatNumber(totalWeight) }} kg</template>
        </p>
      </div>
      <div class="inventory-actions">
        <button type="button" class="button ghost small" data-open-catalog @click="pickerOpen = true">
          <Icon name="lucide:book-open" />Do catálogo
        </button>
        <button type="button" class="button primary small" data-new-item @click="openEditor(null)">
          <Icon name="lucide:plus" />Novo item
        </button>
      </div>
    </header>

    <template v-if="feedback">
      <p v-if="character.error && !editor && !pickerOpen" class="form-error" role="alert" data-inventory-error>{{ character.error }}</p>
      <ul v-if="character.notices.length" class="character-notices" role="status" data-inventory-notices>
        <li v-for="notice in character.notices" :key="notice"><Icon name="lucide:info" />{{ notice }}</li>
      </ul>
    </template>

    <div v-if="!groups.length" class="inventory-empty" data-inventory-empty>
      <Icon name="lucide:backpack" size="2rem" />
      <p>A mochila está vazia.</p>
      <p class="sheet-hint">Crie um item do zero ou escolha um do catálogo.</p>
    </div>

    <section v-for="group in groups" :key="group.category" class="inventory-group" :data-category="group.category">
      <h3 class="inventory-group-title">{{ group.label }} <small>{{ group.items.length }}</small></h3>
      <ul class="inventory-list">
        <li
          v-for="item in group.items"
          :key="item.id"
          class="inventory-item"
          :class="{ 'is-equipped': item.data.equipped, 'is-busy': busyId === item.id }"
          :data-item-id="item.id"
        >
          <div class="inventory-item-main">
            <p class="inventory-item-name">
              <span class="inventory-item-title">{{ item.data.name }}</span>
              <span v-if="item.data.quantity !== 1" class="inventory-qty" :aria-label="`quantidade ${item.data.quantity}`">×{{ item.data.quantity }}</span>
            </p>
            <p v-if="summary(item.data)" class="inventory-item-stats">{{ summary(item.data) }}</p>
            <p class="inventory-badges">
              <span v-if="item.data.magicBonus > 0" class="item-badge magic" data-magic-bonus>{{ signed(item.data.magicBonus) }}</span>
              <span v-if="item.data.effects.length" class="item-badge">{{ effectsLabel(item.data.effects.length) }}</span>
              <span v-if="item.catalogItemId" class="item-badge subtle">catálogo</span>
              <span
                v-for="entry in suppressedByItem.get(item.id) ?? []"
                :key="entry.effectKey"
                class="item-badge warning"
                :title="`Vale o efeito de ${entry.keptItemName}.`"
                data-suppressed
              >
                <Icon name="lucide:copy-x" />Efeito repetido: não acumula
              </span>
            </p>
          </div>

          <div class="inventory-item-controls">
            <button
              v-if="TAKES_PROFICIENCY.has(item.data.category)"
              type="button"
              class="item-toggle"
              :aria-pressed="item.data.proficient"
              :disabled="busyId === item.id"
              :title="item.data.proficient ? 'Proficiente' : 'Sem proficiência'"
              data-proficient
              @click="toggleProficient(item)"
            >
              <Icon :name="item.data.proficient ? 'lucide:graduation-cap' : 'lucide:circle-dashed'" />
              <span>{{ compact ? 'Prof.' : item.data.proficient ? 'Proficiente' : 'Sem prof.' }}</span>
            </button>
            <button
              v-if="EQUIPPABLE.has(item.data.category)"
              type="button"
              class="item-toggle equip"
              :aria-pressed="item.data.equipped"
              :disabled="busyId === item.id"
              data-equip
              @click="toggleEquip(item)"
            >
              <Icon :name="item.data.equipped ? 'lucide:shield-check' : 'lucide:shield-off'" />
              <span>{{ item.data.equipped ? 'Equipado' : 'Equipar' }}</span>
            </button>
            <button type="button" class="icon-button" :aria-label="`Editar ${item.data.name}`" data-edit-item @click="openEditor(item)">
              <Icon name="lucide:pencil" />
            </button>
            <template v-if="confirmingId === item.id">
              <span class="inventory-confirm-label">Remover?</span>
              <button type="button" class="button ghost danger small" :disabled="busyId === item.id" data-confirm-remove @click="remove(item)">Confirmar</button>
              <button type="button" class="icon-button" aria-label="Cancelar remoção" data-cancel-remove @click="cancelRemove(item)"><Icon name="lucide:x" /></button>
            </template>
            <button v-else type="button" class="icon-button danger" :aria-label="`Remover ${item.data.name}`" data-remove-item @click="remove(item)">
              <Icon name="lucide:trash-2" />
            </button>
          </div>
        </li>
      </ul>
    </section>

    <ItemEditorModal v-if="editor" :item="editor.item" @close="editor = null" />
    <CatalogPicker v-if="pickerOpen" mode="items" @close="pickerOpen = false" />
  </section>
</template>
