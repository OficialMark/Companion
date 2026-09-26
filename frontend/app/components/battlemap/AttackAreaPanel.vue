<script setup lang="ts">
import type { AttackAreaDraft } from '~/composables/useAttackAreas'
import type { AttackAreaShape } from '~/types/campaign'

const props = withDefaults(defineProps<{
  draft: AttackAreaDraft
  hasExisting: boolean
  maxRadius?: number
}>(), { maxRadius: 20 })

const emit = defineEmits<{
  change: [draft: AttackAreaDraft]
  commit: [draft: AttackAreaDraft]
  remove: []
  close: []
}>()

const shapes: Array<{ value: AttackAreaShape; label: string; icon: string }> = [
  { value: 'Circle', label: 'Círculo', icon: '◯' },
  { value: 'Square', label: 'Quadrado', icon: '▢' },
  { value: 'Cone', label: 'Cone', icon: '◣' },
  { value: 'Cylinder', label: 'Feixe', icon: '▭' }
]

const isDirectional = computed(() => props.draft.shape === 'Cone' || props.draft.shape === 'Cylinder')

function selectShape(shape: AttackAreaShape) {
  const next: AttackAreaDraft = shape === 'Circle' || shape === 'Square'
    ? { shape, radius: props.draft.radius, width: null, directionDegrees: null }
    : { shape, radius: props.draft.radius, width: props.draft.width ?? 1, directionDegrees: props.draft.directionDegrees ?? 0 }
  emit('change', next)
  emit('commit', next)
}

function onRadiusInput(event: Event) {
  const radius = Number((event.target as HTMLInputElement).value)
  emit('change', { ...props.draft, radius })
}
function onWidthInput(event: Event) {
  const width = Number((event.target as HTMLInputElement).value)
  emit('change', { ...props.draft, width })
}
function onDirectionInput(event: Event) {
  const directionDegrees = Number((event.target as HTMLInputElement).value)
  emit('change', { ...props.draft, directionDegrees })
}
function onSliderChange() {
  emit('commit', props.draft)
}
</script>

<template>
  <div class="attack-area-panel" role="dialog" aria-label="Configurar área de ataque" @click.stop>
    <div class="attack-area-shapes">
      <button
        v-for="shape in shapes"
        :key="shape.value"
        type="button"
        class="attack-area-shape"
        :class="{ selected: draft.shape === shape.value }"
        :aria-pressed="draft.shape === shape.value"
        @click="selectShape(shape.value)"
      ><span aria-hidden="true">{{ shape.icon }}</span>{{ shape.label }}</button>
    </div>

    <label class="attack-area-slider">
      <span>{{ isDirectional ? 'Distância' : 'Raio' }}: {{ draft.radius }} {{ draft.radius === 1 ? 'bloco' : 'blocos' }}</span>
      <input
        type="range" min="1" :max="maxRadius" step="1"
        :value="draft.radius"
        @input="onRadiusInput"
        @change="onSliderChange"
      >
    </label>

    <template v-if="isDirectional">
      <label class="attack-area-slider">
        <span>Largura: {{ draft.width }} {{ draft.width === 1 ? 'bloco' : 'blocos' }}</span>
        <input
          type="range" min="1" :max="maxRadius" step="1"
          :value="draft.width ?? 1"
          @input="onWidthInput"
          @change="onSliderChange"
        >
      </label>
      <label class="attack-area-slider">
        <span>Direção: {{ draft.directionDegrees }}°</span>
        <input
          type="range" min="0" max="359" step="5"
          :value="draft.directionDegrees ?? 0"
          @input="onDirectionInput"
          @change="onSliderChange"
        >
      </label>
    </template>

    <div class="attack-area-actions">
      <button v-if="hasExisting" class="button ghost danger" type="button" @click="emit('remove'); emit('close')">Remover</button>
      <button class="button primary" type="button" @click="emit('close')">Concluir</button>
    </div>
  </div>
</template>
