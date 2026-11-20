<script setup lang="ts">
import { toRaw } from 'vue'
import type { CharacterFeature, CharacterFeatureData, EffectType, FeatureRecovery } from '~/types/character'
import { RECOVERY_LABEL } from '~/types/character'
import { EFFECT_SPECS, EFFECT_TYPES, MAX_EFFECTS, OP_LABEL, defaultEffect } from './ItemEditorModal.vue'

/**
 * Create or edit a class, species or feat feature (spec 013, US2). The counter is optional; the server
 * keeps the uses already spent when the feature is edited and starts a new counter full.
 */
const props = defineProps<{ feature?: CharacterFeature | null }>()
const emit = defineEmits<{ close: [] }>()

const character = useCharacterStore()
const isNew = !props.feature
const source = props.feature ? structuredClone(toRaw(props.feature.data)) : null
const form = reactive({
  name: source?.name ?? '',
  source: source?.source ?? '',
  description: source?.description ?? '',
  hasCounter: source ? source.maxUses !== null : false,
  maxUses: source?.maxUses ?? 1,
  // A feature with a counter keeps its recovery ("Não recupera" included); a new counter starts on long rest.
  recovery: (source && source.maxUses !== null ? source.recovery : 'LongRest') as FeatureRecovery,
  effects: structuredClone(toRaw(source?.effects ?? []))
})
const localError = ref('')
const saving = ref(false)
const nameInput = ref<HTMLInputElement | null>(null)
let opener: Element | null = null

const RECOVERIES: FeatureRecovery[] = ['ShortRest', 'LongRest', 'None']

function addEffect() {
  if (form.effects.length < MAX_EFFECTS) form.effects.push(defaultEffect('DamageBonus'))
}
function onEffectType(index: number, event: Event) {
  form.effects[index] = defaultEffect((event.target as HTMLSelectElement).value as EffectType)
}
function targetOptions(type: EffectType) {
  const targets = EFFECT_SPECS[type].targets
  return Array.isArray(targets) ? targets : []
}
function removeEffect(index: number) { form.effects.splice(index, 1) }
const num = (value: unknown) => value === '' || value === null || value === undefined || Number.isNaN(Number(value)) ? null : Number(value)

function validate(): string {
  if (!form.name.trim()) return 'Dê um nome à característica.'
  if (form.hasCounter) {
    const max = Number(form.maxUses)
    if (!Number.isInteger(max) || max < 1 || max > 999) return 'Os usos máximos devem ficar entre 1 e 999.'
  }
  for (const effect of form.effects) {
    const spec = EFFECT_SPECS[effect.type]
    if (spec.targets === 'text' && !(effect.target ?? '').trim()) return `Escreva o texto do efeito "${spec.label}".`
    if (!spec.textual && num(effect.value) === null) return `Informe o valor do efeito "${spec.label}".`
  }
  return ''
}

function payload(): CharacterFeatureData {
  const max = form.hasCounter ? Number(form.maxUses) : null
  const effects = form.effects.map((effect) => {
    const spec = EFFECT_SPECS[effect.type]
    return {
      type: effect.type,
      target: spec.targets === null ? null : (effect.target ?? '').trim() || null,
      op: spec.ops.includes(effect.op) ? effect.op : 'Add',
      value: spec.textual ? null : num(effect.value)
    }
  })
  return {
    name: form.name.trim(),
    source: form.source.trim() || null,
    description: form.description.trim() || null,
    maxUses: max,
    // The server owns the counter: it keeps the uses already spent (capped by a lower maximum) and
    // starts a new counter full, whatever this field says.
    currentUses: max ?? 0,
    recovery: max === null ? 'None' : form.recovery,
    effects
  }
}

async function save() {
  localError.value = validate()
  if (localError.value) return
  const data = payload()
  const feature = props.feature
  saving.value = true
  const ok = await character.run((api, session) => feature ? api.saveFeature(session, feature.id, data) : api.addFeature(session, data))
  saving.value = false
  if (ok) emit('close')
}

const shownError = computed(() => localError.value || (saving.value ? '' : character.error))

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
      aria-labelledby="feature-editor-title"
      data-feature-editor
      novalidate
      @submit.prevent="save"
    >
      <header class="sheet-modal-header">
        <h2 id="feature-editor-title">{{ isNew ? 'Nova característica' : 'Editar característica' }}</h2>
        <button type="button" class="icon-button" aria-label="Fechar" @click="emit('close')"><Icon name="lucide:x" /></button>
      </header>

      <div class="sheet-grid">
        <label class="sheet-field wide">
          <span>Nome</span>
          <input ref="nameInput" v-model="form.name" type="text" maxlength="80" required data-feature-field="name">
        </label>
        <label class="sheet-field wide">
          <span>Origem</span>
          <input v-model="form.source" type="text" maxlength="60" placeholder="ex.: Paladino 5, Juramento da Conquista, Talento" data-feature-field="source">
        </label>
      </div>

      <fieldset class="item-block">
        <legend>Usos</legend>
        <label class="item-check">
          <input v-model="form.hasCounter" type="checkbox" data-feature-field="hasCounter">
          <span>Tem contador de usos</span>
        </label>
        <div v-if="form.hasCounter" class="sheet-grid">
          <label class="sheet-field">
            <span>Usos máximos</span>
            <input v-model.number="form.maxUses" type="number" min="1" max="999" inputmode="numeric" data-feature-field="maxUses">
          </label>
          <label class="sheet-field">
            <span>Recupera em</span>
            <select v-model="form.recovery" data-feature-field="recovery">
              <option v-for="value in RECOVERIES" :key="value" :value="value">{{ RECOVERY_LABEL[value] }}</option>
            </select>
          </label>
        </div>
      </fieldset>

      <fieldset class="item-block" data-feature-effects>
        <legend>Efeitos</legend>
        <p v-if="!form.effects.length" class="sheet-hint">Nenhum efeito. Adicione bônus e outras regras que devem entrar nos cálculos da ficha.</p>
        <ul class="effect-rows">
          <li v-for="(effect, index) in form.effects" :key="index" class="effect-row" :data-feature-effect-row="index">
            <label class="sheet-field">
              <span>Tipo</span>
              <select :value="effect.type" data-feature-effect-type @change="onEffectType(index, $event)">
                <option v-for="type in EFFECT_TYPES" :key="type" :value="type">{{ EFFECT_SPECS[type].label }}</option>
              </select>
            </label>
            <label v-if="targetOptions(effect.type).length" class="sheet-field">
              <span>Alvo</span>
              <select v-model="effect.target" data-feature-effect-target>
                <option v-for="target in targetOptions(effect.type)" :key="target.value" :value="target.value">{{ target.label }}</option>
              </select>
            </label>
            <label v-else-if="EFFECT_SPECS[effect.type].targets === 'text'" class="sheet-field">
              <span>Texto</span>
              <input v-model="effect.target" type="text" maxlength="80" data-feature-effect-target>
            </label>
            <label v-if="EFFECT_SPECS[effect.type].ops.length > 1" class="sheet-field">
              <span>Operação</span>
              <select v-model="effect.op" data-feature-effect-op>
                <option v-for="op in EFFECT_SPECS[effect.type].ops" :key="op" :value="op">{{ OP_LABEL[op] }}</option>
              </select>
            </label>
            <label v-if="!EFFECT_SPECS[effect.type].textual" class="sheet-field">
              <span>Valor</span>
              <input v-model.number="effect.value" type="number" :min="effect.op === 'Min' ? 0 : -30" :max="effect.op === 'Min' ? 300 : 30" inputmode="numeric" data-feature-effect-value>
            </label>
            <button type="button" class="icon-button effect-remove" :aria-label="`Remover efeito ${index + 1}`" data-feature-effect-remove @click="removeEffect(index)">
              <Icon name="lucide:trash-2" />
            </button>
          </li>
        </ul>
        <button type="button" class="button ghost small" :disabled="form.effects.length >= MAX_EFFECTS" data-feature-add-effect @click="addEffect">
          <Icon name="lucide:plus" />Adicionar efeito
        </button>
      </fieldset>

      <label class="sheet-field">
        <span>Descrição</span>
        <textarea v-model="form.description" rows="6" maxlength="8000" data-feature-field="description" />
      </label>

      <p v-if="shownError" class="form-error" role="alert" data-feature-error>{{ shownError }}</p>

      <div class="sheet-modal-actions">
        <button type="button" class="button ghost" @click="emit('close')">Cancelar</button>
        <button type="submit" class="button primary" :disabled="saving" data-feature-save>
          <Icon :name="saving ? 'lucide:loader-circle' : 'lucide:check'" :class="{ spin: saving }" />{{ isNew ? 'Adicionar' : 'Salvar' }}
        </button>
      </div>
    </form>
  </div>
</template>
