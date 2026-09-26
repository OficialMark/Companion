<script setup lang="ts">
import type { DiceRoll } from '~/types/campaign'

const props = defineProps<{ roll: DiceRoll }>()
const emit = defineEmits<{ hit: [roll: DiceRoll]; dismissed: [] }>()
const confirmButton = ref<HTMLButtonElement | null>(null)
let timer: ReturnType<typeof setTimeout> | undefined

function respond(hit: boolean) {
  if (timer) clearTimeout(timer)
  if (hit) emit('hit', props.roll)
  else emit('dismissed')
}

function handleKeydown(event: KeyboardEvent) { if (event.key === 'Escape') respond(false) }

onMounted(() => {
  timer = setTimeout(() => respond(false), 10000)
  window.addEventListener('keydown', handleKeydown)
  confirmButton.value?.focus()
})
onBeforeUnmount(() => {
  if (timer) clearTimeout(timer)
  window.removeEventListener('keydown', handleKeydown)
})
</script>
<template>
  <div class="modal-overlay" role="alertdialog" aria-modal="true" aria-labelledby="attack-confirm-title">
    <div class="modal-panel attack-confirm-modal">
      <p class="eyebrow">Resultado: <strong>{{ roll.total }}</strong></p>
      <h2 id="attack-confirm-title">Acertou?</h2>
      <div class="button-row">
        <button ref="confirmButton" class="button primary attack-hit-confirm" type="button" @click="respond(true)">Sim</button>
        <button class="button ghost attack-hit-deny" type="button" @click="respond(false)">Não</button>
      </div>
    </div>
  </div>
</template>
