<script setup lang="ts">
import type { FogMode } from '~/utils/fogMask'
import type { FogTool } from '~/composables/useFogOfWar'

const props = defineProps<{
  mode: FogMode
  tool: FogTool
  brushSize: number
  busy?: boolean
  /** The publish preview has no "done" step: the fog is sent with the image. */
  closable?: boolean
  context?: 'board' | 'preview'
}>()

const emit = defineEmits<{
  'update:mode': [mode: FogMode]
  'update:tool': [tool: FogTool]
  'update:brushSize': [size: number]
  'cover-all': []
  'reveal-all': []
  close: []
}>()

const brushSizes = [1, 3, 5]
const hint = computed(() => props.tool === 'rect'
  ? `Arraste para ${props.mode === 'cover' ? 'cobrir' : 'revelar'} um retângulo.`
  : props.context === 'preview'
    ? `Arraste sobre a prévia para ${props.mode === 'cover' ? 'cobrir' : 'revelar'} células.`
    : `Arraste para ${props.mode === 'cover' ? 'cobrir' : 'revelar'} células. Pinça, roda ou botão direito movem o mapa.`)
</script>

<template>
  <div class="fog-toolbar" role="toolbar" aria-label="Ferramentas de névoa">
    <div class="fog-toolbar-row">
      <div class="fog-segment" role="radiogroup" aria-label="Ação da névoa">
        <button type="button" role="radio" class="fog-option" data-fog-mode="cover" :class="{ selected: mode === 'cover' }" :aria-checked="mode === 'cover'" title="Ocultar áreas" @click="emit('update:mode', 'cover')"><Icon name="lucide:cloud" /><span>Ocultar</span></button>
        <button type="button" role="radio" class="fog-option" data-fog-mode="reveal" :class="{ selected: mode === 'reveal' }" :aria-checked="mode === 'reveal'" title="Revelar áreas" @click="emit('update:mode', 'reveal')"><Icon name="lucide:eraser" /><span>Revelar</span></button>
      </div>
      <div class="fog-segment" role="radiogroup" aria-label="Ferramenta">
        <button
          v-for="size in brushSizes"
          :key="size"
          type="button"
          role="radio"
          class="fog-option compact"
          :data-fog-brush="size"
          :class="{ selected: tool === 'brush' && brushSize === size }"
          :aria-checked="tool === 'brush' && brushSize === size"
          :title="`Pincel ${size}x${size}`"
          @click="emit('update:tool', 'brush'); emit('update:brushSize', size)"
        ><Icon name="lucide:brush" /><span>{{ size }}x{{ size }}</span></button>
        <button type="button" role="radio" class="fog-option compact" data-fog-tool="rect" :class="{ selected: tool === 'rect' }" :aria-checked="tool === 'rect'" title="Retângulo" aria-label="Retângulo" @click="emit('update:tool', 'rect')"><Icon name="lucide:square-dashed" /><span class="fog-option-label-wide">Retângulo</span></button>
      </div>
    </div>
    <div class="fog-toolbar-row fog-toolbar-actions">
      <button type="button" class="button ghost compact" data-fog-cover-all :disabled="busy" @click="emit('cover-all')"><Icon name="lucide:cloud-fog" /><span>Cobrir tudo</span></button>
      <button type="button" class="button ghost compact" data-fog-reveal-all :disabled="busy" @click="emit('reveal-all')"><Icon name="lucide:sun-dim" /><span>Revelar tudo</span></button>
      <button v-if="closable !== false" type="button" class="button primary compact push-end fog-done" data-fog-close aria-label="Concluir edição da névoa" title="Concluir (Esc)" @click="emit('close')"><Icon name="lucide:check" /><span class="fog-done-label">Concluir</span></button>
    </div>
    <p class="fog-hint"><Icon name="lucide:info" />{{ hint }}</p>
  </div>
</template>
