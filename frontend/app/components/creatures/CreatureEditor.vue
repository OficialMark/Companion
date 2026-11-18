<script setup lang="ts">
import { CREATURE_SIZE_LABELS, type CreatureAction, type CreatureFields, type CreatureSize } from '~/types/creatures'
import { ABILITIES, abilityModifier, formatModifier } from '~/utils/creatureRoll'
import { blankCreature } from '~/stores/creatures'

/** Edits any creature (spec 008, US3). Saving a compendium creature creates this table's version. */
const creatures = useCreatureStore()
const images = useImageCacheStore()
const sheet = computed(() => creatures.sheet!)
const form = reactive<CreatureFields>(toFields())
const busy = ref(false)
const error = ref('')
const confirmRemove = ref(false)
const fileInput = ref<HTMLInputElement | null>(null)

function toFields(): CreatureFields {
  const source = creatures.sheet ?? { ...blankCreature() }
  const copy = JSON.parse(JSON.stringify(source)) as CreatureFields
  const fields = blankCreature()
  for (const key of Object.keys(fields) as Array<keyof CreatureFields>) (fields as unknown as Record<string, unknown>)[key] = copy[key] ?? fields[key]
  return fields
}

const sizes = Object.entries(CREATURE_SIZE_LABELS) as Array<[CreatureSize, string]>
const textFields: Array<[keyof CreatureFields, string]> = [
  ['savingThrows', 'Salvaguardas'], ['skills', 'Perícias'], ['damageVulnerabilities', 'Vulnerabilidades a dano'],
  ['damageResistances', 'Resistências a dano'], ['damageImmunities', 'Imunidades a dano'],
  ['conditionImmunities', 'Imunidades a condição'], ['senses', 'Sentidos'], ['languages', 'Idiomas']
]
const lists: Array<{ key: 'traits' | 'actions' | 'bonusActions' | 'reactions' | 'legendaryActions'; title: string }> = [
  { key: 'traits', title: 'Traços' }, { key: 'actions', title: 'Ações' }, { key: 'bonusActions', title: 'Ações bônus' },
  { key: 'reactions', title: 'Reações' }, { key: 'legendaryActions', title: 'Ações lendárias' }
]
const portrait = computed(() => sheet.value.imageUrl ? images.urls[sheet.value.imageUrl] ?? null : null)
const isNew = computed(() => !sheet.value.id)

function addEntry(list: CreatureAction[]) { list.push({ name: '', description: '' }) }
function removeEntry(list: CreatureAction[], index: number) { list.splice(index, 1) }
function emptyToNull(value: string | null) { return value?.trim() ? value.trim() : null }
function setText(key: keyof CreatureFields, value: string) { (form as unknown as Record<string, string>)[key] = value }

async function save() {
  error.value = ''
  if (!form.name.trim()) { error.value = 'Dê um nome à criatura.'; return }
  busy.value = true
  try {
    const payload: CreatureFields = {
      ...form,
      nameEn: emptyToNull(form.nameEn), alignment: emptyToNull(form.alignment), armorClassNote: emptyToNull(form.armorClassNote),
      hitDice: emptyToNull(form.hitDice), speed: emptyToNull(form.speed),
      ...Object.fromEntries(textFields.map(([key]) => [key, emptyToNull(form[key] as string | null)])),
      legendaryActionsIntro: emptyToNull(form.legendaryActionsIntro), notes: emptyToNull(form.notes),
      traits: form.traits.filter(item => item.name.trim()), actions: form.actions.filter(item => item.name.trim()),
      bonusActions: form.bonusActions.filter(item => item.name.trim()), reactions: form.reactions.filter(item => item.name.trim()),
      legendaryActions: form.legendaryActions.filter(item => item.name.trim())
    }
    await creatures.save(payload)
  } catch (exception) {
    error.value = (exception as Error).message
  } finally {
    busy.value = false
  }
}

function cancel() {
  if (isNew.value) creatures.close()
  else creatures.editing = false
}

async function remove() {
  if (!confirmRemove.value) { confirmRemove.value = true; return }
  busy.value = true; error.value = ''
  try { await creatures.remove() } catch (exception) { error.value = (exception as Error).message } finally { busy.value = false; confirmRemove.value = false }
}

async function pickImage(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0]
  ;(event.target as HTMLInputElement).value = ''
  if (!file) return
  if (!['image/png', 'image/jpeg', 'image/webp'].includes(file.type)) { error.value = 'Use uma imagem PNG, JPEG ou WebP.'; return }
  if (file.size > 1024 * 1024) { error.value = 'A imagem deve ter no máximo 1 MB.'; return }
  busy.value = true; error.value = ''
  try { await creatures.uploadImage(file) } catch (exception) { error.value = (exception as Error).message } finally { busy.value = false }
}
async function dropImage() {
  busy.value = true; error.value = ''
  try { await creatures.removeImage() } catch (exception) { error.value = (exception as Error).message } finally { busy.value = false }
}
</script>

<template>
  <form class="creature-editor" data-creature-editor @submit.prevent="save">
    <p v-if="sheet.origin === 'Compendium'" class="field-hint"><Icon name="lucide:info" />Salvar cria a versão desta mesa. O original do compêndio continua disponível para restaurar.</p>

    <fieldset class="creature-fieldset">
      <legend>Imagem do token</legend>
      <div class="creature-image-row">
        <span class="creature-portrait large" :class="{ 'has-image': portrait }"><img v-if="portrait" :src="portrait" alt=""><Icon v-else name="lucide:image" /></span>
        <div class="creature-image-actions">
          <input ref="fileInput" type="file" accept="image/png,image/jpeg,image/webp" class="sr-only" aria-label="Imagem da criatura" @change="pickImage">
          <button class="button secondary compact" type="button" :disabled="busy || isNew" @click="fileInput?.click()"><Icon name="lucide:image-up" />{{ sheet.hasImage ? 'Trocar imagem' : 'Enviar imagem' }}</button>
          <button v-if="sheet.hasImage" class="button ghost compact" type="button" :disabled="busy" @click="dropImage"><Icon name="lucide:image-off" />Remover</button>
          <small class="field-hint">{{ isNew ? 'Salve a criatura para enviar a imagem.' : 'PNG, JPEG ou WebP até 1 MB. Aparece nos tokens.' }}</small>
        </div>
      </div>
    </fieldset>

    <fieldset class="creature-fieldset creature-grid">
      <legend>Identidade</legend>
      <label class="span-2">Nome<input v-model="form.name" maxlength="120" required data-field="name"></label>
      <label class="span-2">Nome em inglês<input v-model="form.nameEn" maxlength="120"></label>
      <label>Tamanho<select v-model="form.size"><option v-for="[value, label] in sizes" :key="value" :value="value">{{ label }}</option></select></label>
      <label>Tipo<input v-model="form.type" maxlength="80" required></label>
      <label class="span-2">Alinhamento<input v-model="form.alignment" maxlength="80"></label>
      <label>ND<input v-model="form.challengeRating" maxlength="10" required placeholder="1/4"></label>
      <label>XP<input v-model.number="form.experiencePoints" type="number" min="0"></label>
      <label>Proficiência<input v-model.number="form.proficiencyBonus" type="number" min="0" max="20"></label>
    </fieldset>

    <fieldset class="creature-fieldset creature-grid">
      <legend>Combate</legend>
      <label>CA<input v-model.number="form.armorClass" type="number" min="0" max="50" required data-field="armorClass"></label>
      <label class="span-2">Nota da CA<input v-model="form.armorClassNote" maxlength="120" placeholder="armadura natural"></label>
      <label>PV<input v-model.number="form.hitPoints" type="number" min="1" max="9999" required></label>
      <label>Dados de vida<input v-model="form.hitDice" maxlength="40" placeholder="2d6"></label>
      <label>Iniciativa<input v-model.number="form.initiative" type="number" min="-20" max="30" placeholder="DES"></label>
      <label class="span-2">Deslocamento<input v-model="form.speed" maxlength="200"></label>
    </fieldset>

    <fieldset class="creature-fieldset">
      <legend>Atributos</legend>
      <div class="creature-abilities editing">
        <label v-for="ability in ABILITIES" :key="ability.key" class="creature-ability">
          <span>{{ ability.label }}</span>
          <input v-model.number="form[ability.field]" type="number" min="1" max="30" :aria-label="ability.label">
          <small>{{ formatModifier(abilityModifier(Number(form[ability.field]) || 10)) }}</small>
        </label>
      </div>
    </fieldset>

    <fieldset class="creature-fieldset creature-grid">
      <legend>Detalhes</legend>
      <label v-for="[key, label] in textFields" :key="key" class="span-2">{{ label }}<input :value="form[key] ?? ''" maxlength="500" @input="setText(key, ($event.target as HTMLInputElement).value)"></label>
    </fieldset>

    <fieldset v-for="list in lists" :key="list.key" class="creature-fieldset">
      <legend>{{ list.title }}</legend>
      <label v-if="list.key === 'legendaryActions'" class="creature-list-intro">Introdução<textarea v-model="form.legendaryActionsIntro" rows="2" maxlength="2000" /></label>
      <div v-for="(entry, index) in form[list.key]" :key="index" class="creature-list-entry">
        <input v-model="entry.name" maxlength="120" :aria-label="`${list.title}: nome ${index + 1}`" placeholder="Nome">
        <textarea v-model="entry.description" rows="2" maxlength="4000" :aria-label="`${list.title}: descrição ${index + 1}`" placeholder="Descrição" />
        <button class="icon-button small danger" type="button" :aria-label="`Remover ${entry.name || 'item'}`" @click="removeEntry(form[list.key], index)"><Icon name="lucide:trash-2" /></button>
      </div>
      <button class="button ghost compact" type="button" :disabled="form[list.key].length >= 40" @click="addEntry(form[list.key])"><Icon name="lucide:plus" />Adicionar</button>
    </fieldset>

    <fieldset class="creature-fieldset">
      <legend>Anotações pessoais</legend>
      <textarea v-model="form.notes" rows="3" maxlength="4000" aria-label="Anotações pessoais" />
    </fieldset>

    <p v-if="error" class="form-error" role="alert"><Icon name="lucide:circle-alert" />{{ error }}</p>
    <div class="creature-editor-actions">
      <button v-if="!isNew && sheet.origin !== 'Compendium'" class="button ghost danger" type="button" data-remove-creature :disabled="busy" @click="remove">
        <Icon :name="sheet.origin === 'Edited' ? 'lucide:undo-2' : 'lucide:trash-2'" />
        {{ confirmRemove ? 'Confirmar' : sheet.origin === 'Edited' ? 'Restaurar original' : 'Excluir criatura' }}
      </button>
      <button class="button ghost push-end" type="button" :disabled="busy" @click="cancel">Cancelar</button>
      <button class="button primary" type="submit" data-save-creature :disabled="busy"><Icon :name="busy ? 'lucide:loader-circle' : 'lucide:save'" :class="{ spin: busy }" />Salvar</button>
    </div>
  </form>
</template>
