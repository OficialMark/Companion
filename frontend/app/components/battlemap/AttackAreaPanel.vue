<script setup lang="ts">
import type { AttackAreaDraft } from '~/composables/useAttackAreas'
import type { AttackAreaShape } from '~/types/campaign'

const props = withDefaults(defineProps<{
  draft: AttackAreaDraft
  hasExisting: boolean
  maxRadius?: number
  tokenName?: string
}>(), { maxRadius: 20, tokenName: '' })

const emit = defineEmits<{
  change: [draft: AttackAreaDraft]
  commit: [draft: AttackAreaDraft]
  remove: []
  close: []
}>()

const shapes: Array<{ value: AttackAreaShape; label: string; icon: string; iconClass?: string }> = [
  { value: 'Circle', label: 'Círculo', icon: 'lucide:circle' },
  { value: 'Square', label: 'Quadrado', icon: 'lucide:square' },
  { value: 'Cone', label: 'Cone', icon: 'lucide:triangle', iconClass: 'rotate-cone' },
  { value: 'Cylinder', label: 'Feixe', icon: 'lucide:rectangle-horizontal' }
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
const blocks = (value: number | null) => `${value ?? 1} ${value === 1 ? 'bloco' : 'blocos'}`
</script>

<template>
  <div class="attack-area-panel" role="dialog" aria-label="Configurar área de ataque" @click.stop>
    <div class="attack-area-header">
      <p class="eyebrow"><Icon name="lucide:wand-sparkles" />Área de efeito{{ tokenName ? ` · ${tokenName}` : '' }}</p>
      <button class="icon-button small" type="button" aria-label="Fechar painel de área" title="Fechar" @click="emit('close')"><Icon name="lucide:x" /></button>
    </div>
    <div class="attack-area-shapes" role="group" aria-label="Formato da área">
      <button
        v-for="shape in shapes"
        :key="shape.value"
        type="button"
        class="attack-area-shape"
        :class="{ selected: draft.shape === shape.value }"
        :aria-pressed="draft.shape === shape.value"
        @click="selectShape(shape.value)"
      ><Icon :name="shape.icon" :class="shape.iconClass" />{{ shape.label }}</button>
    </div>

    <label class="attack-area-slider">
      <span class="slider-label"><Icon name="lucide:radius" />{{ isDirectional ? 'Distância' : 'Raio' }}<strong>{{ blocks(draft.radius) }}</strong></span>
      <input
        type="range" min="1" :max="maxRadius" step="1"
        :value="draft.radius"
        @input="onRadiusInput"
        @change="onSliderChange"
      >
    </label>

    <template v-if="isDirectional">
      <label class="attack-area-slider">
        <span class="slider-label"><Icon name="lucide:move-horizontal" />Largura<strong>{{ blocks(draft.width) }}</strong></span>
        <input
          type="range" min="1" :max="maxRadius" step="1"
          :value="draft.width ?? 1"
          @input="onWidthInput"
          @change="onSliderChange"
        >
      </label>
      <label class="attack-area-slider">
        <span class="slider-label">
          <Icon name="lucide:compass" />Direção
          <strong><Icon name="lucide:arrow-right" class="direction-arrow" :style="{ transform: `rotate(${draft.directionDegrees ?? 0}deg)` }" />{{ draft.directionDegrees }}°</strong>
        </span>
        <input
          type="range" min="0" max="359" step="5"
          :value="draft.directionDegrees ?? 0"
          @input="onDirectionInput"
          @change="onSliderChange"
        >
      </label>
    </template>

    <div class="attack-area-actions">
      <button v-if="hasExisting" class="button ghost danger" type="button" @click="emit('remove'); emit('close')"><Icon name="lucide:trash-2" />Remover</button>
      <button class="button primary" type="button" @click="emit('close')"><Icon name="lucide:check" />Concluir</button>
    </div>
  </div>
</template>
