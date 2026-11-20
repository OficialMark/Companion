<script setup lang="ts">
import type { CharacterSpell } from '~/types/character'
import { ABILITY_LABEL } from '~/types/character'
import SpellEditorModal from './SpellEditorModal.vue'
import CatalogPicker from './CatalogPicker.vue'
import { focusAfterRender, rowOf } from '~/utils/focus'

/**
 * "Magias" tab (spec 011, US7). Spell save DC and spell attack come from the server's stats.
 * Descriptions are plain text shown with their line breaks; never rendered as HTML.
 */
const character = useCharacterStore()
const expanded = ref(new Set<string>())
const confirmingId = ref<string | null>(null)
const busyId = ref<string | null>(null)
const editor = ref<{ spell: CharacterSpell | null } | null>(null)
const pickerOpen = ref(false)

const spells = computed(() => character.character?.spells ?? [])
const groups = computed(() => {
  const byLevel = new Map<number, CharacterSpell[]>()
  for (const spell of spells.value) byLevel.set(spell.data.level, [...(byLevel.get(spell.data.level) ?? []), spell])
  return [...byLevel.entries()]
    .sort(([a], [b]) => a - b)
    .map(([level, list]) => ({ level, label: levelTitle(level), spells: list.sort((a, b) => a.sortOrder - b.sortOrder || a.data.name.localeCompare(b.data.name, 'pt-BR')) }))
})

const saveDc = computed(() => character.stats?.spellSaveDc ?? null)
const attack = computed(() => character.stats?.spellAttackBonus ?? null)

function levelTitle(level: number) { return level === 0 ? 'Truques' : `${level}º círculo` }
function signed(value: number) { return value >= 0 ? `+${value}` : `${value}` }
function partsTitle(parts: { source: string; value: number }[]) { return parts.map(part => `${part.source}: ${signed(part.value)}`).join('\n') }

function toggle(id: string) {
  const next = new Set(expanded.value)
  if (next.has(id)) next.delete(id)
  else next.add(id)
  expanded.value = next
}

function rollLine(spell: CharacterSpell) {
  const data = spell.data
  const parts: string[] = []
  if (data.attackKind === 'Attack') parts.push('ataque mágico')
  if (data.attackKind === 'Save' && data.saveAbility) parts.push(`salvaguarda de ${ABILITY_LABEL[data.saveAbility]}`)
  if (data.damageDice) parts.push([data.damageDice, data.damageType].filter(Boolean).join(' '))
  return parts.join(', ')
}

// Keyboard focus follows the inline confirmation (FR-039), as in the bag.
const root = ref<HTMLElement | null>(null)
const inRow = (id: string, selector: string) => () => rowOf(root.value, 'data-spell-id', id)?.querySelector<HTMLElement>(selector)

async function remove(spell: CharacterSpell) {
  if (confirmingId.value !== spell.id) {
    confirmingId.value = spell.id
    await focusAfterRender(inRow(spell.id, '[data-confirm-remove]'))
    return
  }
  busyId.value = spell.id
  const ok = await character.run((api, session) => api.removeSpell(session, spell.id))
  busyId.value = null
  if (!ok) return
  confirmingId.value = null
  await focusAfterRender(() => root.value)
}

async function cancelRemove(spell: CharacterSpell) {
  confirmingId.value = null
  await focusAfterRender(inRow(spell.id, '[data-remove-spell]'))
}

function openEditor(spell: CharacterSpell | null) {
  confirmingId.value = null
  editor.value = { spell }
}
</script>

<template>
  <section ref="root" class="spell-list" aria-labelledby="spell-list-title" tabindex="-1" data-spell-list>
    <header class="inventory-header">
      <div class="inventory-heading">
        <h2 id="spell-list-title" class="sheet-section-title">Magias</h2>
        <p v-if="spells.length" class="inventory-meta">{{ spells.length === 1 ? '1 magia' : `${spells.length} magias` }}</p>
      </div>
      <div class="inventory-actions">
        <button type="button" class="button ghost small" data-open-catalog @click="pickerOpen = true">
          <Icon name="lucide:book-open" />Do catálogo
        </button>
        <button type="button" class="button primary small" data-new-spell @click="openEditor(null)">
          <Icon name="lucide:plus" />Nova magia
        </button>
      </div>
    </header>

    <div v-if="saveDc || attack" class="spell-casting" data-spell-casting>
      <div v-if="saveDc" class="spell-casting-stat" :title="partsTitle(saveDc.parts)" data-spell-dc>
        <span>CD de magia</span><strong>{{ saveDc.value }}</strong>
      </div>
      <div v-if="attack" class="spell-casting-stat" :title="partsTitle(attack.parts)" data-spell-attack>
        <span>Ataque mágico</span><strong>{{ signed(attack.value) }}</strong>
      </div>
    </div>
    <p v-else class="sheet-hint spell-casting-hint" data-spell-casting-hint>
      <Icon name="lucide:info" /> Escolha o atributo de conjuração na aba Ficha para ver a CD e o ataque mágico.
    </p>

    <div v-if="!groups.length" class="inventory-empty" data-spell-empty>
      <Icon name="lucide:wand-sparkles" size="2rem" />
      <p>Nenhuma magia ainda.</p>
      <p class="sheet-hint">Busque no catálogo ou crie a sua.</p>
    </div>

    <section v-for="group in groups" :key="group.level" class="inventory-group" :data-spell-level="group.level">
      <h3 class="inventory-group-title">{{ group.label }} <small>{{ group.spells.length }}</small></h3>
      <ul class="inventory-list">
        <li v-for="spell in group.spells" :key="spell.id" class="inventory-item spell-item" :data-spell-id="spell.id">
          <div class="inventory-item-main">
            <p class="inventory-item-name">
              <span class="inventory-item-title">{{ spell.data.name }}</span>
              <span v-if="spell.data.concentration" class="item-badge subtle" title="Concentração" data-badge="concentration">Concentração</span>
              <span v-if="spell.data.ritual" class="item-badge subtle" title="Ritual" data-badge="ritual">Ritual</span>
            </p>
            <p class="inventory-item-stats spell-meta">
              <span v-if="spell.data.school">{{ spell.data.school }}</span>
              <span v-if="spell.data.castingTime"><Icon name="lucide:clock" />{{ spell.data.castingTime }}</span>
              <span v-if="spell.data.range"><Icon name="lucide:move-horizontal" />{{ spell.data.range }}</span>
              <span v-if="spell.data.duration"><Icon name="lucide:hourglass" />{{ spell.data.duration }}</span>
              <span v-if="spell.data.components">{{ spell.data.components }}</span>
            </p>
            <p v-if="rollLine(spell)" class="spell-roll" data-spell-roll>{{ rollLine(spell) }}</p>
            <button
              v-if="spell.data.description"
              type="button"
              class="spell-expand"
              :aria-expanded="expanded.has(spell.id)"
              :aria-controls="`spell-description-${spell.id}`"
              data-spell-expand
              @click="toggle(spell.id)"
            >
              <Icon :name="expanded.has(spell.id) ? 'lucide:chevron-up' : 'lucide:chevron-down'" />
              {{ expanded.has(spell.id) ? 'Esconder descrição' : 'Ver descrição' }}
            </button>
            <p v-if="expanded.has(spell.id)" :id="`spell-description-${spell.id}`" class="spell-description" data-spell-description>{{ spell.data.description }}</p>
          </div>

          <div class="inventory-item-controls">
            <button type="button" class="icon-button" :aria-label="`Editar ${spell.data.name}`" data-edit-spell @click="openEditor(spell)">
              <Icon name="lucide:pencil" />
            </button>
            <template v-if="confirmingId === spell.id">
              <span class="inventory-confirm-label">Remover?</span>
              <button type="button" class="button ghost danger small" :disabled="busyId === spell.id" data-confirm-remove @click="remove(spell)">Confirmar</button>
              <button type="button" class="icon-button" aria-label="Cancelar remoção" data-cancel-remove @click="cancelRemove(spell)"><Icon name="lucide:x" /></button>
            </template>
            <button v-else type="button" class="icon-button danger" :aria-label="`Remover ${spell.data.name}`" data-remove-spell @click="remove(spell)">
              <Icon name="lucide:trash-2" />
            </button>
          </div>
        </li>
      </ul>
    </section>

    <SpellEditorModal v-if="editor" :spell="editor.spell" @close="editor = null" />
    <CatalogPicker v-if="pickerOpen" mode="spells" @close="pickerOpen = false" />
  </section>
</template>
