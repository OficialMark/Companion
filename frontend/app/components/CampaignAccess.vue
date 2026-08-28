<script setup lang="ts">
const store = useCampaignStore()
const api = useCompanionApi()
const mode = ref<'create' | 'join'>('create')
const campaignName = ref('')
const code = ref('')
const displayName = ref('')
const busy = ref(false)
const error = ref('')
const createdSession = ref<Awaited<ReturnType<typeof api.createCampaign>> | null>(null)
const revealMasterCode = ref(false)

async function submit() {
  busy.value = true
  error.value = ''
  try {
    const session = mode.value === 'create'
      ? await api.createCampaign(campaignName.value, displayName.value)
      : await api.joinCampaign(code.value, displayName.value)
    if (mode.value === 'create') createdSession.value = session
    else store.setSession(session)
  } catch (exception) {
    error.value = (exception as Error).message
  } finally {
    busy.value = false
  }
}

function enterCreatedCampaign() {
  if (createdSession.value) store.setSession(createdSession.value)
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

      <div v-if="createdSession" class="stack access-codes" aria-live="polite">
        <h2>Mesa criada</h2>
        <p>Compartilhe apenas o código de jogador:</p>
        <strong class="created-code">{{ createdSession.playerCode }}</strong>
        <p>O código de mestre é reservado. Guarde-o para entrar como mestre em outro navegador.</p>
        <button class="button secondary wide" type="button" @click="revealMasterCode = !revealMasterCode">
          {{ revealMasterCode ? 'Ocultar código de mestre' : 'Revelar código de mestre' }}
        </button>
        <strong v-if="revealMasterCode" class="created-code master-code">{{ createdSession.masterCode }}</strong>
        <button class="button primary wide" type="button" @click="enterCreatedCampaign">Entrar como mestre</button>
      </div>
      <form v-else class="stack" @submit.prevent="submit">
        <label v-if="mode === 'create'">
          Nome da campanha
          <input v-model="campaignName" maxlength="80" required placeholder="A Coroa de Cinzas">
        </label>
        <label v-else>
          Código da campanha
          <input v-model="code" maxlength="4" minlength="4" pattern="[0-9]{4}" inputmode="numeric" required autocomplete="off" class="code-input" placeholder="0123">
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
