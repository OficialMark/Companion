<script setup lang="ts">
import type { AttackAreaDraft } from '~/composables/useAttackAreas'
import type { TokenAppearanceDraft } from '~/composables/useTokenAppearance'
import type { AttackAreaShape, StatusCondition } from '~/types/campaign'
import { STATUS_CONDITIONS } from '~/utils/statusConditions'
import { tokenPanelTab as tab, type TokenPanelTab } from '~/composables/useTokenAppearance'

const props = withDefaults(defineProps<{
  draft: AttackAreaDraft
  hasExisting: boolean
  maxRadius?: number
  tokenName?: string
  appearanceDraft?: TokenAppearanceDraft
  isMarker?: boolean
  /**
   * HP visibility toggle (spec 007, US3): only rendered for the master on creature tokens.
   * null = not applicable (player characters, markers, or a creature without HP tracking).
   */
  hitPointsHidden?: boolean | null
  /** Master on a creature token linked to the library (spec 008): shows "Ficha". */
  canOpenSheet?: boolean
}>(), {
  maxRadius: 20,
  tokenName: '',
  appearanceDraft: () => ({ footprintSize: 1, statusConditions: [] }),
  isMarker: false,
  hitPointsHidden: null,
  canOpenSheet: false
})

const emit = defineEmits<{
  change: [draft: AttackAreaDraft]
  commit: [draft: AttackAreaDraft]
  remove: []
  close: []
  'appearance-change': [draft: TokenAppearanceDraft]
  'appearance-commit': [draft: TokenAppearanceDraft]
  'remove-token': []
  'hit-points-visibility': [hidden: boolean]
  'open-sheet': []
}>()

const tabs: Array<{ value: TokenPanelTab; label: string; icon: string }> = [
  { value: 'area', label: 'Área', icon: 'lucide:wand-sparkles' },
  { value: 'token', label: 'Token', icon: 'lucide:scaling' },
  { value: 'conditions', label: 'Condições', icon: 'lucide:sparkle' }
]

const footprintSizes = [1, 2, 3, 4, 5]

function selectFootprintSize(footprintSize: number) {
  const next: TokenAppearanceDraft = { ...props.appearanceDraft, footprintSize }
  emit('appearance-change', next)
  emit('appearance-commit', next)
}

const statusConditionOptions = STATUS_CONDITIONS
const activeConditionCount = computed(() => props.appearanceDraft.statusConditions.length)

function toggleStatusCondition(condition: StatusCondition) {
  const active = props.appearanceDraft.statusConditions.includes(condition)
  const statusConditions = active
    ? props.appearanceDraft.statusConditions.filter(item => item !== condition)
    : [...props.appearanceDraft.statusConditions, condition]
  const next: TokenAppearanceDraft = { ...props.appearanceDraft, statusConditions }
  emit('appearance-change', next)
  emit('appearance-commit', next)
}

function clearConditions() {
  const next: TokenAppearanceDraft = { ...props.appearanceDraft, statusConditions: [] }
  emit('appearance-change', next)
  emit('appearance-commit', next)
}

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

function onTabKeydown(event: KeyboardEvent, index: number) {
  const delta = event.key === 'ArrowRight' ? 1 : event.key === 'ArrowLeft' ? -1 : 0
  if (!delta) return
  event.preventDefault()
  const next = tabs[(index + delta + tabs.length) % tabs.length]!
  tab.value = next.value
  nextTick(() => (document.getElementById(`token-panel-tab-${next.value}`) as HTMLElement | null)?.focus())
}
</script>

<template>
  <div class="attack-area-panel" role="dialog" :aria-label="`Menu do token${tokenName ? ` ${tokenName}` : ''}`" @click.stop>
    <div class="attack-area-header">
      <p class="eyebrow token-panel-title"><Icon name="lucide:target" /><span>{{ tokenName || 'Token' }}</span></p>
      <button class="icon-button small" type="button" aria-label="Fechar menu do token" title="Fechar (Esc)" @click="emit('close')"><Icon name="lucide:x" /></button>
    </div>

    <div class="token-panel-tabs" role="tablist" aria-label="Seções do menu do token">
      <button
        v-for="(item, index) in tabs"
        :id="`token-panel-tab-${item.value}`"
        :key="item.value"
        type="button"
        role="tab"
        class="token-panel-tab"
        :class="{ selected: tab === item.value }"
        :aria-selected="tab === item.value"
        :aria-controls="`token-panel-section-${item.value}`"
        :tabindex="tab === item.value ? 0 : -1"
        :data-tab="item.value"
        @click="tab = item.value"
        @keydown="onTabKeydown($event, index)"
      >
        <Icon :name="item.icon" />
        <span>{{ item.label }}</span>
        <span v-if="item.value === 'conditions' && activeConditionCount" class="token-panel-tab-count" :aria-label="`${activeConditionCount} ativas`">{{ activeConditionCount }}</span>
      </button>
    </div>

    <section v-show="tab === 'area'" id="token-panel-section-area" role="tabpanel" aria-labelledby="token-panel-tab-area" class="token-panel-section">
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
      <button v-if="hasExisting" class="button ghost danger compact token-panel-inline-action" type="button" @click="emit('remove')"><Icon name="lucide:trash-2" />Remover área</button>
    </section>

    <section v-show="tab === 'token'" id="token-panel-section-token" role="tabpanel" aria-labelledby="token-panel-tab-token" class="token-panel-section">
      <p class="token-panel-label">Tamanho no grid</p>
      <div class="token-size-options" role="group" aria-label="Tamanho do token">
        <button
          v-for="size in footprintSizes"
          :key="size"
          type="button"
          class="token-size-option"
          :class="{ selected: appearanceDraft.footprintSize === size }"
          :aria-pressed="appearanceDraft.footprintSize === size"
          @click="selectFootprintSize(size)"
        >{{ size }}x{{ size }}</button>
      </div>

      <label v-if="hitPointsHidden !== null" class="token-hp-toggle" :class="{ off: hitPointsHidden }">
        <input
          type="checkbox"
          data-hp-visibility-toggle
          :checked="!hitPointsHidden"
          @change="emit('hit-points-visibility', !($event.target as HTMLInputElement).checked)"
        >
        <Icon :name="hitPointsHidden ? 'lucide:eye-off' : 'lucide:eye'" class="token-hp-toggle-icon" />
        <span>
          <strong>HP visível para jogadores</strong>
          <small>{{ hitPointsHidden ? 'Só você vê a vida desta criatura.' : 'Todos veem a barra e os números de vida.' }}</small>
        </span>
      </label>

      <button v-if="isMarker" class="button ghost danger compact marker-remove-button token-panel-inline-action" type="button" @click="emit('remove-token'); emit('close')"><Icon name="lucide:user-x" />Remover token</button>
    </section>

    <section v-show="tab === 'conditions'" id="token-panel-section-conditions" role="tabpanel" aria-labelledby="token-panel-tab-conditions" class="token-panel-section">
      <div class="token-condition-options" role="group" aria-label="Condições de status">
        <button
          v-for="option in statusConditionOptions"
          :key="option.value"
          type="button"
          class="token-condition-option"
          :data-condition="option.value"
          :style="{ '--condition-color': option.color }"
          :class="{ selected: appearanceDraft.statusConditions.includes(option.value) }"
          :aria-pressed="appearanceDraft.statusConditions.includes(option.value)"
          :title="option.label"
          @click="toggleStatusCondition(option.value)"
        ><span class="token-condition-icon" aria-hidden="true"><Icon :name="option.icon" /></span><span class="token-condition-label">{{ option.label }}</span></button>
      </div>
      <button v-if="activeConditionCount" class="button ghost compact token-panel-inline-action" type="button" @click="clearConditions"><Icon name="lucide:eraser" />Limpar condições</button>
    </section>

    <div class="attack-area-actions">
      <button v-if="canOpenSheet" class="button secondary push-start" type="button" data-open-sheet @click="emit('open-sheet')"><Icon name="lucide:scroll-text" />Ficha e rolagens</button>
      <button class="button primary" type="button" @click="emit('close')"><Icon name="lucide:check" />Concluir</button>
    </div>
  </div>
</template>
