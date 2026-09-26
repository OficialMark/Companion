<script setup lang="ts">
const store = useCampaignStore()
const api = useCompanionApi()
const modifier = ref<number | null>(null)
const hitPoints = ref<number | null>(null)
const maxHitPoints = ref<number | null>(null)
const busy = ref(false)
const message = ref('')
const error = ref('')
watch(() => store.snapshot?.currentParticipant, (participant) => { modifier.value = participant?.initiativeModifier ?? null; hitPoints.value = participant?.currentHitPoints ?? null; maxHitPoints.value = participant?.maxHitPoints ?? null }, { immediate: true, deep: true })
async function save() {
  if (!store.session || !store.snapshot) return
  busy.value = true; message.value = ''; error.value = ''
  try { const updated = await api.updateCombatProfile(store.session, modifier.value, hitPoints.value, maxHitPoints.value); store.snapshot.currentParticipant = updated; const index = store.snapshot.participants.findIndex(participant => participant.id === updated.id); if (index >= 0) store.snapshot.participants[index] = updated; message.value = 'Perfil de combate salvo.' }
  catch (exception) { error.value = (exception as Error).message }
  finally { busy.value = false }
}
</script>
<template>
  <section v-if="!store.isMaster" class="panel modifier-panel" aria-labelledby="modifier-title">
    <div><p class="eyebrow">Seu personagem</p><h2 id="modifier-title">Perfil de combate</h2><p>O mestre usará estes valores ao preparar os jogadores online.</p></div>
    <form class="modifier-form" @submit.prevent="save">
      <label class="combat-profile-field" for="own-initiative-modifier"><span>Modificador de iniciativa</span><input id="own-initiative-modifier" v-model.number="modifier" type="number" min="-100" max="100" placeholder="0"></label>
      <label class="combat-profile-field" for="own-hit-points"><span>Vida atual</span><input id="own-hit-points" v-model.number="hitPoints" type="number" min="0" max="99999" step="1" placeholder="Não informada"></label>
      <label class="combat-profile-field" for="own-max-hit-points"><span>Vida máxima</span><input id="own-max-hit-points" v-model.number="maxHitPoints" type="number" min="0" max="99999" step="1" placeholder="Não informada"></label>
      <button class="button secondary" :disabled="busy || store.connection === 'offline'" type="submit">{{ busy ? 'Salvando…' : 'Salvar' }}</button>
    </form>
    <p v-if="message" class="form-success" role="status">{{ message }}</p><p v-if="error" class="form-error" role="alert">{{ error }}</p>
  </section>
</template>