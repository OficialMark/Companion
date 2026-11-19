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
const copied = ref<'player' | 'master' | null>(null)

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

async function copy(kind: 'player' | 'master', value: string | null | undefined) {
  if (!value || !navigator.clipboard) return
  await navigator.clipboard.writeText(value)
  copied.value = kind
  setTimeout(() => { if (copied.value === kind) copied.value = null }, 1800)
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
        <button :class="{ active: mode === 'create' }" type="button" role="tab" :aria-selected="mode === 'create'" @click="mode = 'create'"><Icon name="lucide:plus" />Criar campanha</button>
        <button :class="{ active: mode === 'join' }" type="button" role="tab" :aria-selected="mode === 'join'" @click="mode = 'join'"><Icon name="lucide:log-in" />Entrar com código</button>
      </div>

      <div v-if="createdSession" class="stack access-codes" aria-live="polite">
        <span class="state-icon success" aria-hidden="true"><Icon name="lucide:circle-check" size="1.6rem" /></span>
        <h2>Mesa criada</h2>
        <p>Compartilhe apenas o código de jogador:</p>
        <button class="created-code" type="button" :title="copied === 'player' ? 'Copiado' : 'Copiar código de jogador'" @click="copy('player', createdSession.playerCode)">
          {{ createdSession.playerCode }}<Icon :name="copied === 'player' ? 'lucide:check' : 'lucide:copy'" size="1.1rem" />
        </button>
        <p class="access-warning"><Icon name="lucide:shield-alert" />O código de mestre é reservado. Guarde-o para entrar como mestre em outro navegador.</p>
        <button class="button secondary wide" type="button" @click="revealMasterCode = !revealMasterCode">
          <Icon :name="revealMasterCode ? 'lucide:eye-off' : 'lucide:eye'" />
          {{ revealMasterCode ? 'Ocultar código de mestre' : 'Revelar código de mestre' }}
        </button>
        <button v-if="revealMasterCode" class="created-code master-code" type="button" :title="copied === 'master' ? 'Copiado' : 'Copiar código de mestre'" @click="copy('master', createdSession.masterCode)">
          {{ createdSession.masterCode }}<Icon :name="copied === 'master' ? 'lucide:check' : 'lucide:copy'" size="1.1rem" />
        </button>
        <button class="button primary wide" type="button" @click="enterCreatedCampaign"><Icon name="lucide:crown" />Entrar como mestre</button>
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
        <p v-if="error" class="form-error" role="alert"><Icon name="lucide:circle-alert" />{{ error }}</p>
        <button class="button primary wide" :disabled="busy" type="submit">
          <Icon :name="busy ? 'lucide:loader-circle' : mode === 'create' ? 'lucide:sparkles' : 'lucide:door-open'" :class="{ spin: busy }" />
          {{ busy ? 'Preparando a mesa…' : mode === 'create' ? 'Criar e entrar' : 'Entrar na campanha' }}
        </button>
      </form>
      <p class="access-note"><Icon name="lucide:lock" />Nenhuma conta é necessária. Sua sessão fica somente neste navegador.</p>
    </section>
  </main>
</template>
