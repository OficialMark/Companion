<script setup lang="ts">
import type { EffectType, ItemEffect } from '~/types/character'
import { EFFECT_SPECS, EFFECT_TYPES, MAX_EFFECTS, OP_LABEL, defaultEffect } from '~/utils/effects'

/**
 * The effect rows of an item (spec 011) or a feature (spec 014): type, target, operation and value.
 * Edits the parent's list in place; the parent normalizes and the server validates.
 */
const effects = defineModel<ItemEffect[]>({ required: true })
defineProps<{ hint: string }>()

function addEffect() {
  if (effects.value.length >= MAX_EFFECTS) return
  effects.value.push(defaultEffect('ArmorClass'))
}
function onEffectType(index: number, event: Event) {
  effects.value[index] = defaultEffect((event.target as HTMLSelectElement).value as EffectType)
}
function targetOptions(type: EffectType) {
  const targets = EFFECT_SPECS[type].targets
  return Array.isArray(targets) ? targets : []
}
function removeEffect(index: number) { effects.value.splice(index, 1) }
</script>

<template>
  <fieldset class="item-block" data-block="effects">
    <legend>Efeitos</legend>
    <p v-if="!effects.length" class="sheet-hint">{{ hint }}</p>
    <ul class="effect-rows">
      <li v-for="(effect, index) in effects" :key="index" class="effect-row" :data-effect-row="index">
        <label class="sheet-field">
          <span>Tipo</span>
          <select :value="effect.type" data-effect-type @change="onEffectType(index, $event)">
            <option v-for="type in EFFECT_TYPES" :key="type" :value="type">{{ EFFECT_SPECS[type].label }}</option>
          </select>
        </label>
        <label v-if="targetOptions(effect.type).length" class="sheet-field">
          <span>Alvo</span>
          <select v-model="effect.target" data-effect-target>
            <option v-for="target in targetOptions(effect.type)" :key="target.value" :value="target.value">{{ target.label }}</option>
          </select>
        </label>
        <label v-else-if="EFFECT_SPECS[effect.type].targets === 'text'" class="sheet-field">
          <span>Texto</span>
          <input v-model="effect.target" type="text" maxlength="80" placeholder="ex.: fogo, envenenado" data-effect-target>
        </label>
        <label v-if="EFFECT_SPECS[effect.type].ops.length > 1" class="sheet-field">
          <span>Operação</span>
          <select v-model="effect.op" data-effect-op>
            <option v-for="op in EFFECT_SPECS[effect.type].ops" :key="op" :value="op">{{ OP_LABEL[op] }}</option>
          </select>
        </label>
        <label v-if="!EFFECT_SPECS[effect.type].textual" class="sheet-field">
          <span>Valor</span>
          <input v-model.number="effect.value" type="number" :min="effect.op === 'Min' ? 0 : -30" :max="effect.op === 'Min' ? 300 : 30" inputmode="numeric" data-effect-value>
        </label>
        <button type="button" class="icon-button effect-remove" :aria-label="`Remover efeito ${index + 1}`" data-effect-remove @click="removeEffect(index)">
          <Icon name="lucide:trash-2" />
        </button>
      </li>
    </ul>
    <button type="button" class="button ghost small" :disabled="effects.length >= MAX_EFFECTS" data-add-effect @click="addEffect">
      <Icon name="lucide:plus" />Adicionar efeito
    </button>
  </fieldset>
</template>
