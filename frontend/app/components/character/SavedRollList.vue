<script setup lang="ts">
import type { SavedRoll } from '~/types/character'
import SavedRollEditor from '~/components/character/SavedRollEditor.vue'
import { configFromSavedRoll, resolveRoll } from '~/utils/playerRoll'

/**
 * "Rolagens" tab of the sheet (spec 011, US6): the saved rolls with their live expression, built from
 * the current sheet, and actions to create, edit, duplicate and remove them.
 */
const character = useCharacterStore()

const rolls = computed(() => [...(character.character?.savedRolls ?? [])].sort((a, b) => a.sortOrder - b.sortOrder))
const editing = ref<SavedRoll | null>(null)
const creating = ref(false)
const confirmingId = ref<string | null>(null)
const busyId = ref<string | null>(null)

function expressionOf(roll: SavedRoll) {
  return resolveRoll(configFromSavedRoll(roll.data), character.character?.stats ?? null, character.character).expression
}

function closeEditor() { editing.value = null; creating.value = false }

async function duplicate(roll: SavedRoll) {
  busyId.value = roll.id
  await character.run((api, session) => api.duplicateRoll(session, roll.id))
  busyId.value = null
}
async function remove(roll: SavedRoll) {
  busyId.value = roll.id
  const ok = await character.run((api, session) => api.removeRoll(session, roll.id))
  busyId.value = null
  if (ok) confirmingId.value = null
}
</script>

<template>
  <section class="saved-roll-list" aria-labelledby="saved-rolls-title" data-saved-roll-list>
    <div class="saved-roll-list-head">
      <h2 id="saved-rolls-title" class="sheet-section-title">Rolagens salvas</h2>
      <button class="button secondary small" type="button" data-new-roll @click="creating = true"><Icon name="lucide:plus" />Nova rolagem</button>
    </div>
    <p class="sheet-hint">Cada rolagem guarda a arma, a magia ou o atributo, e usa os valores atuais da ficha na hora de rolar.</p>

    <div v-if="rolls.length === 0" class="empty-state">
      <span class="state-icon"><Icon name="lucide:dices" size="1.6rem" /></span>
      <p>Nenhuma rolagem salva ainda.</p>
    </div>
    <ul v-else class="saved-roll-items">
      <li
        v-for="roll in rolls"
        :key="roll.id"
        class="saved-roll-item"
        :data-saved-roll-item="roll.id"
        :data-incomplete="roll.incomplete ? '' : undefined"
      >
        <span class="saved-roll-item-icon" aria-hidden="true"><Icon :name="roll.data.icon" /></span>
        <div class="saved-roll-item-body">
          <strong>{{ roll.data.name }}</strong>
          <code data-expression>{{ expressionOf(roll) }}</code>
          <span v-if="roll.incomplete" class="saved-roll-incomplete"><Icon name="lucide:triangle-alert" />Arma ou magia ausente: edite a rolagem</span>
        </div>
        <div v-if="confirmingId === roll.id" class="saved-roll-item-actions" role="group" aria-label="Confirmar remoção">
          <button class="button ghost danger small" type="button" data-confirm-remove :disabled="busyId === roll.id" @click="remove(roll)">
            <Icon name="lucide:trash-2" />Remover
          </button>
          <button class="button ghost small" type="button" data-cancel-remove :disabled="busyId === roll.id" @click="confirmingId = null">Manter</button>
        </div>
        <div v-else class="saved-roll-item-actions">
          <button class="icon-button" type="button" data-edit-roll :aria-label="`Editar ${roll.data.name}`" title="Editar" @click="editing = roll"><Icon name="lucide:pencil" /></button>
          <button class="icon-button" type="button" data-duplicate-roll :aria-label="`Duplicar ${roll.data.name}`" title="Duplicar" :disabled="busyId === roll.id" @click="duplicate(roll)"><Icon name="lucide:copy" /></button>
          <button class="icon-button" type="button" data-remove-roll :aria-label="`Remover ${roll.data.name}`" title="Remover" @click="confirmingId = roll.id"><Icon name="lucide:trash-2" /></button>
        </div>
      </li>
    </ul>

    <SavedRollEditor v-if="editing || creating" :roll="editing" @close="closeEditor" @saved="closeEditor" />
  </section>
</template>
