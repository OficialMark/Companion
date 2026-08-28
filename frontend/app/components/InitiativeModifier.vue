<script setup lang="ts">
const store = useCampaignStore()
const api = useCompanionApi()
const modifier = ref<number | null>(null)
const busy = ref(false)
const message = ref('')
const error = ref('')

watch(() => store.snapshot?.currentParticipant.initiativeModifier, (value) => {
  modifier.value = value ?? null
}, { immediate: true })

async function save() {
  if (!store.session || !store.snapshot) return
  busy.value = true
  message.value = ''
  error.value = ''
  try {
    const updated = await api.updateInitiativeModifier(store.session, modifier.value)
    store.snapshot.currentParticipant = updated
    const index = store.snapshot.participants.findIndex(participant => participant.id === updated.id)
    if (index >= 0) store.snapshot.participants[index] = updated
    message.value = 'Modificador salvo.'
  } catch (exception) {
    error.value = (exception as Error).message
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <section class="panel modifier-panel" aria-labelledby="modifier-title">
    <div>
      <p class="eyebrow">Seu personagem</p>
      <h2 id="modifier-title">Modificador de iniciativa</h2>
      <p>O mestre usará este valor ao preparar a iniciativa dos jogadores online.</p>
    </div>
    <form class="modifier-form" @submit.prevent="save">
      <label for="own-initiative-modifier">Modificador</label>
      <input id="own-initiative-modifier" v-model.number="modifier" type="number" min="-100" max="100" placeholder="0">
      <button class="button secondary" :disabled="busy || store.connection === 'offline'" type="submit">{{ busy ? 'Salvando…' : 'Salvar' }}</button>
    </form>
    <p v-if="message" class="form-success" role="status">{{ message }}</p>
    <p v-if="error" class="form-error" role="alert">{{ error }}</p>
  </section>
</template>
