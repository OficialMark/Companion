<script setup lang="ts">
import { radioGroupKeydown, trapTab } from '~/utils/focus'
import type { SavedRoll } from '~/types/character'
import RollConfigFields from '~/components/character/RollConfigFields.vue'
import { MAX_ROLL_NAME, cloneConfig, configFromSavedRoll, emptyConfig, resolveRoll, savedRollFromConfig, type RollConfig } from '~/utils/playerRoll'
import { DEFAULT_SAVED_ROLL_ICON, SAVED_ROLL_ICONS, SAVED_ROLL_ICON_LABEL, isSavedRollIcon, type SavedRollIcon } from '~/utils/savedRollIcons'

/**
 * Create or edit a saved roll (spec 011, US6). The controls are the same `RollConfigFields` of the
 * Modo Jogo panel, so the same configuration shows the same expression in both places (FR-036).
 * The roll stores references (weapon, spell, ability), never numbers (FR-034).
 */
const props = defineProps<{
  /** The saved roll being edited; absent to create a new one. */
  roll?: SavedRoll | null
  /** Starting configuration of a new roll (the panel's current one, for "Salvar como rolagem"). */
  initial?: RollConfig | null
}>()
const emit = defineEmits<{ close: []; saved: [] }>()

const character = useCharacterStore()
const name = ref(props.roll?.data.name ?? '')
const icon = ref<SavedRollIcon>(props.roll && isSavedRollIcon(props.roll.data.icon) ? props.roll.data.icon : DEFAULT_SAVED_ROLL_ICON)
const config = ref<RollConfig>(props.roll ? configFromSavedRoll(props.roll.data) : props.initial ? cloneConfig(props.initial) : emptyConfig())
const busy = ref(false)
const error = ref('')
const nameInput = ref<HTMLInputElement | null>(null)
const dialog = ref<HTMLElement | null>(null)
const returnFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null

const stats = computed(() => character.character?.stats ?? null)
const resolved = computed(() => resolveRoll(config.value, stats.value, character.character))
const nameError = computed(() => (name.value.trim().length === 0 ? 'Dê um nome à rolagem.' : null))
const canSave = computed(() => !busy.value && !nameError.value && !resolved.value.error && Boolean(resolved.value.expression))
const title = computed(() => (props.roll ? 'Editar rolagem' : 'Nova rolagem'))

async function save() {
  if (!canSave.value) return
  busy.value = true; error.value = ''
  const data = savedRollFromConfig(config.value, name.value, icon.value)
  const existing = props.roll
  const ok = await character.run((api, session) => (existing ? api.saveRoll(session, existing.id, data) : api.addRoll(session, data)))
  busy.value = false
  if (!ok) { error.value = character.error || 'Não foi possível salvar a rolagem.'; return }
  emit('saved')
}

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape' && !busy.value) emit('close')
  else if (dialog.value) trapTab(event, dialog.value)
}
onMounted(() => { window.addEventListener('keydown', onKeydown); nameInput.value?.focus() })
onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKeydown)
  if (returnFocus?.isConnected) returnFocus.focus()
})
</script>

<template>
  <div ref="dialog" class="modal-overlay" role="dialog" aria-modal="true" aria-labelledby="saved-roll-editor-title" @click.self="!busy && emit('close')">
    <form class="modal-panel saved-roll-editor" data-saved-roll-editor @submit.prevent="save">
      <div class="saved-roll-editor-head">
        <h2 id="saved-roll-editor-title"><Icon :name="icon" />{{ title }}</h2>
        <button class="icon-button" type="button" aria-label="Fechar" title="Fechar" :disabled="busy" @click="emit('close')"><Icon name="lucide:x" /></button>
      </div>

      <label class="sheet-field">
        <span>Nome</span>
        <input ref="nameInput" v-model="name" type="text" :maxlength="MAX_ROLL_NAME" placeholder="Ataque com espada" data-roll-name required>
      </label>

      <fieldset class="saved-roll-icons">
        <legend>Ícone</legend>
        <div class="saved-roll-icon-grid" role="radiogroup" aria-label="Ícone da rolagem" @keydown="radioGroupKeydown">
          <button
            v-for="option in SAVED_ROLL_ICONS"
            :key="option"
            type="button"
            role="radio"
            class="saved-roll-icon"
            :class="{ selected: icon === option }"
            :data-icon="option"
            :aria-checked="icon === option"
            :tabindex="icon === option ? 0 : -1"
            :aria-label="SAVED_ROLL_ICON_LABEL[option]"
            :title="SAVED_ROLL_ICON_LABEL[option]"
            @click="icon = option"
          ><Icon :name="option" /></button>
        </div>
      </fieldset>

      <RollConfigFields v-model="config" :stats="stats" :character="character.character" :disabled="busy" />

      <p v-if="error" class="form-error" role="alert" data-editor-error><Icon name="lucide:circle-alert" />{{ error }}</p>
      <div class="button-row">
        <button class="button primary" type="submit" data-save-saved-roll :disabled="!canSave">
          <Icon :name="busy ? 'lucide:loader-circle' : 'lucide:save'" :class="{ spin: busy }" />{{ busy ? 'Salvando…' : 'Salvar rolagem' }}
        </button>
        <button class="button ghost" type="button" data-cancel-editor :disabled="busy" @click="emit('close')">Cancelar</button>
      </div>
    </form>
  </div>
</template>
