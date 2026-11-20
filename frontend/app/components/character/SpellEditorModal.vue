<script setup lang="ts">
import { toRaw } from 'vue'
import type { CatalogSpell, CharacterSpell, CharacterSpellData } from '~/types/character'
import { ABILITY_LABEL, ABILITY_ORDER } from '~/types/character'
import { DAMAGE_TYPES, SUM_DICE, compactDice } from './ItemEditorModal.vue'

/**
 * Create or edit one of the character's spells (spec 011, US7). The server validates and recalculates.
 * With `catalog` (spec 013) it edits the master's compendium: English name, classes and "em círculos
 * superiores" join the form, and saving goes to the catalog API (a compendium spell gets the table's version).
 */
const props = defineProps<{ spell?: CharacterSpell | null; catalog?: { entry: CatalogSpell | null } | null }>()
const emit = defineEmits<{ close: []; saved: [entry: CatalogSpell] }>()

const SPELL_CLASSES = ['bardo', 'bruxo', 'clérigo', 'druida', 'feiticeiro', 'mago', 'paladino', 'patrulheiro']

const character = useCharacterStore()
const catalogMode = computed(() => Boolean(props.catalog))
const entry = props.catalog?.entry ?? null
const isNew = catalogMode.value ? !entry : !props.spell
const initial = entry ? entry.template : props.spell?.data
const extra = reactive({
  nameEn: entry?.nameEn ?? '',
  classes: [...(entry?.classes ?? [])],
  higherLevels: entry?.higherLevels ?? ''
})
function toggleClass(value: string) {
  extra.classes = extra.classes.includes(value) ? extra.classes.filter(x => x !== value) : [...extra.classes, value]
}
const form = reactive<CharacterSpellData>(initial ? structuredClone(toRaw(initial)) : {
  name: '', level: 1, school: null, castingTime: '1 ação', range: null, components: null, duration: 'instantânea',
  ritual: false, concentration: false, description: null, attackKind: 'None', saveAbility: null,
  damageDice: null, damageType: null, addAbilityToDamage: false
})
const localError = ref('')
const saving = ref(false)
const nameInput = ref<HTMLInputElement | null>(null)
let opener: Element | null = null

const SCHOOLS = ['abjuração', 'adivinhação', 'conjuração', 'encantamento', 'evocação', 'ilusão', 'necromancia', 'transmutação']

function validate(): string {
  if (!form.name.trim()) return 'Dê um nome à magia.'
  if (form.attackKind === 'Save' && !form.saveAbility) return 'Escolha o atributo da salvaguarda.'
  const dice = compactDice(form.damageDice)
  if (dice && !SUM_DICE.test(dice)) return 'Escreva o dano como 8d6 ou 2d6+1d4.'
  return ''
}

const text = (value: string | null) => value?.trim() || null

function payload(): CharacterSpellData {
  const data = structuredClone(toRaw(form)) as CharacterSpellData
  return {
    ...data,
    name: data.name.trim(),
    level: Math.min(9, Math.max(0, Number(data.level) || 0)),
    school: text(data.school),
    castingTime: text(data.castingTime),
    range: text(data.range),
    components: text(data.components),
    duration: text(data.duration),
    description: text(data.description),
    saveAbility: data.attackKind === 'Save' ? data.saveAbility : null,
    damageDice: compactDice(data.damageDice) || null,
    damageType: data.damageType || null
  }
}

async function saveCatalog(data: CharacterSpellData) {
  const body = { template: data, classes: extra.classes, higherLevels: text(extra.higherLevels), nameEn: text(extra.nameEn) }
  try {
    const api = useCharacterApi()
    const session = character.session()
    const saved = entry ? await api.saveCatalogSpell(session, entry.id, body) : await api.createCatalogSpell(session, body)
    emit('saved', saved)
    return true
  } catch (error) {
    localError.value = (error as Error).message || 'Não foi possível salvar no compêndio.'
    return false
  }
}

async function save() {
  localError.value = validate()
  if (localError.value) return
  const data = payload()
  const spell = props.spell
  saving.value = true
  const ok = catalogMode.value
    ? await saveCatalog(data)
    : await character.run((api, session) => spell ? api.saveSpell(session, spell.id, data) : api.addSpell(session, data))
  saving.value = false
  if (ok) emit('close')
}

const title = computed(() => catalogMode.value
  ? (isNew ? 'Nova magia do compêndio' : 'Editar magia do compêndio')
  : (isNew ? 'Nova magia' : 'Editar magia'))

const shownError = computed(() => localError.value || (saving.value || catalogMode.value ? '' : character.error))

onMounted(async () => {
  opener = document.activeElement
  character.error = ''
  await nextTick()
  nameInput.value?.focus()
})
onBeforeUnmount(() => { if (opener instanceof HTMLElement) opener.focus() })
</script>

<template>
  <div class="modal-overlay sheet-modal-overlay" @keydown.esc.stop="emit('close')" @click.self="emit('close')">
    <form
      class="modal-panel sheet-modal"
      role="dialog"
      aria-modal="true"
      aria-labelledby="spell-editor-title"
      data-spell-editor
      novalidate
      @submit.prevent="save"
    >
      <header class="sheet-modal-header">
        <h2 id="spell-editor-title">{{ title }}</h2>
        <button type="button" class="icon-button" aria-label="Fechar" @click="emit('close')"><Icon name="lucide:x" /></button>
      </header>
      <p v-if="entry?.origin === 'Compendium'" class="sheet-hint" data-catalog-copy-hint>
        <Icon name="lucide:info" /> Salvar cria a versão da mesa desta magia. O original do compêndio não muda e volta se você excluir a versão da mesa.
      </p>

      <div class="sheet-grid">
        <label class="sheet-field wide">
          <span>Nome</span>
          <input ref="nameInput" v-model="form.name" type="text" maxlength="80" required data-spell-field="name">
        </label>
        <label class="sheet-field">
          <span>Círculo</span>
          <select v-model.number="form.level" data-spell-field="level">
            <option v-for="value in 10" :key="value - 1" :value="value - 1">{{ value === 1 ? 'Truque' : `${value - 1}º círculo` }}</option>
          </select>
        </label>
        <label class="sheet-field">
          <span>Escola</span>
          <input v-model="form.school" type="text" list="spell-schools" maxlength="80" data-spell-field="school">
          <datalist id="spell-schools"><option v-for="school in SCHOOLS" :key="school" :value="school" /></datalist>
        </label>
        <label class="sheet-field">
          <span>Tempo de conjuração</span>
          <input v-model="form.castingTime" type="text" maxlength="80" data-spell-field="castingTime">
        </label>
        <label class="sheet-field">
          <span>Alcance</span>
          <input v-model="form.range" type="text" maxlength="80" placeholder="ex.: 18 metros" data-spell-field="range">
        </label>
        <label class="sheet-field">
          <span>Componentes</span>
          <input v-model="form.components" type="text" maxlength="80" placeholder="V, S, M" data-spell-field="components">
        </label>
        <label class="sheet-field">
          <span>Duração</span>
          <input v-model="form.duration" type="text" maxlength="80" data-spell-field="duration">
        </label>
      </div>

      <div class="chip-row" role="group" aria-label="Marcadores da magia">
        <button type="button" class="chip" :aria-pressed="form.concentration" data-spell-field="concentration" @click="form.concentration = !form.concentration">Concentração</button>
        <button type="button" class="chip" :aria-pressed="form.ritual" data-spell-field="ritual" @click="form.ritual = !form.ritual">Ritual</button>
      </div>

      <fieldset class="item-block">
        <legend>Rolagem</legend>
        <div class="sheet-grid">
          <label class="sheet-field">
            <span>Tipo</span>
            <select v-model="form.attackKind" data-spell-field="attackKind">
              <option value="None">nenhuma</option>
              <option value="Attack">ataque mágico</option>
              <option value="Save">salvaguarda</option>
            </select>
          </label>
          <label v-if="form.attackKind === 'Save'" class="sheet-field">
            <span>Salvaguarda de</span>
            <select v-model="form.saveAbility" data-spell-field="saveAbility">
              <option :value="null" disabled>escolha</option>
              <option v-for="code in ABILITY_ORDER" :key="code" :value="code">{{ ABILITY_LABEL[code] }}</option>
            </select>
          </label>
          <label class="sheet-field">
            <span>Dano (opcional)</span>
            <input v-model="form.damageDice" type="text" placeholder="8d6" autocomplete="off" data-spell-field="damageDice">
          </label>
          <label class="sheet-field">
            <span>Tipo de dano</span>
            <select v-model="form.damageType" data-spell-field="damageType">
              <option :value="null">sem tipo</option>
              <option v-for="type in DAMAGE_TYPES" :key="type" :value="type">{{ type }}</option>
            </select>
          </label>
        </div>
        <label class="item-check">
          <input v-model="form.addAbilityToDamage" type="checkbox" data-spell-field="addAbilityToDamage">
          <span>Somar o atributo de conjuração ao dano</span>
        </label>
      </fieldset>

      <label class="sheet-field">
        <span>Descrição</span>
        <textarea v-model="form.description" rows="5" data-spell-field="description" />
      </label>

      <fieldset v-if="catalogMode" class="item-block catalog-extra" data-block="catalog">
        <legend>Compêndio</legend>
        <label class="sheet-field">
          <span>Nome em inglês</span>
          <input v-model="extra.nameEn" type="text" maxlength="120" data-spell-field="nameEn">
        </label>
        <div class="chip-row" role="group" aria-label="Classes que conjuram">
          <button
            v-for="value in SPELL_CLASSES"
            :key="value"
            type="button"
            class="chip"
            :aria-pressed="extra.classes.includes(value)"
            :data-spell-class="value"
            @click="toggleClass(value)"
          >{{ value }}</button>
        </div>
        <label class="sheet-field">
          <span>Em círculos superiores</span>
          <textarea v-model="extra.higherLevels" rows="2" maxlength="4000" data-spell-field="higherLevels" />
        </label>
      </fieldset>

      <p v-if="shownError" class="form-error" role="alert" data-spell-error>{{ shownError }}</p>

      <div class="sheet-modal-actions">
        <button type="button" class="button ghost" @click="emit('close')">Cancelar</button>
        <button type="submit" class="button primary" :disabled="saving" data-spell-save>
          <Icon :name="saving ? 'lucide:loader-circle' : 'lucide:check'" :class="{ spin: saving }" />{{ isNew ? 'Adicionar' : 'Salvar' }}
        </button>
      </div>
    </form>
  </div>
</template>
