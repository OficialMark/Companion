<script setup lang="ts">
const store = useCampaignStore()
const api = useCompanionApi()
const mode = ref<'create' | 'join'>('create')
const campaignName = ref('')
const code = ref('')
const displayName = ref('')
const busy = ref(false)
const error = ref('')

async function submit() {
  busy.value = true
  error.value = ''
  try {
    const session = mode.value === 'create'
      ? await api.createCampaign(campaignName.value, displayName.value)
      : await api.joinCampaign(code.value, displayName.value)
    store.setSession(session)
  } catch (exception) {
    error.value = (exception as Error).message
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <main class="access-shell">
    <section class="access-card" aria-labelledby="access-title">
      <div class="brand-mark" aria-hidden="true">20</div>
      <p class="eyebrow">Sua mesa, em sintonia</p>
      <h1 id="access-title">Companion</h1>
      <p class="lead">Dados e iniciativa compartilhados para sua campanha de D&amp;D.</p>

      <div class="segmented" role="tablist" aria-label="Acesso à campanha">
        <button :class="{ active: mode === 'create' }" type="button" @click="mode = 'create'">Criar campanha</button>
        <button :class="{ active: mode === 'join' }" type="button" @click="mode = 'join'">Entrar com código</button>
      </div>

      <form class="stack" @submit.prevent="submit">
        <label v-if="mode === 'create'">
          Nome da campanha
          <input v-model="campaignName" maxlength="80" required placeholder="A Coroa de Cinzas">
        </label>
        <label v-else>
          Código da campanha
          <input v-model="code" maxlength="8" required autocomplete="off" class="code-input" placeholder="K7M9QP">
        </label>
        <label>
          Seu nome
          <input v-model="displayName" maxlength="40" required autocomplete="nickname" placeholder="Como será chamado na mesa">
        </label>
        <p v-if="error" class="form-error" role="alert">{{ error }}</p>
        <button class="button primary wide" :disabled="busy" type="submit">
          {{ busy ? 'Preparando a mesa…' : mode === 'create' ? 'Criar e entrar' : 'Entrar na campanha' }}
        </button>
      </form>
      <p class="access-note">Nenhuma conta é necessária. Sua sessão fica somente neste navegador.</p>
    </section>
  </main>
</template>
