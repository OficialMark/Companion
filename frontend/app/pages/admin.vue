<script setup lang="ts">
import type { AdminCampaign } from '~/types/campaign'

const api = useCompanionApi()
const adminKey = ref('')
const campaigns = ref<AdminCampaign[]>([])
const authenticated = ref(false)
const busy = ref(false)
const error = ref('')

async function load() {
  busy.value = true
  error.value = ''
  try {
    campaigns.value = await api.listAdminCampaigns(adminKey.value)
    authenticated.value = true
    if (import.meta.client) sessionStorage.setItem('companion.admin.key', adminKey.value)
  } catch (exception) {
    authenticated.value = false
    campaigns.value = []
    error.value = (exception as Error).message
  } finally {
    busy.value = false
  }
}

async function remove(campaign: AdminCampaign) {
  if (!window.confirm(`Excluir permanentemente a mesa “${campaign.name}”?`)) return
  busy.value = true
  error.value = ''
  try {
    await api.deleteAdminCampaign(adminKey.value, campaign.id)
    campaigns.value = campaigns.value.filter(item => item.id !== campaign.id)
  } catch (exception) {
    error.value = (exception as Error).message
  } finally {
    busy.value = false
  }
}

onMounted(() => {
  adminKey.value = sessionStorage.getItem('companion.admin.key') ?? ''
  if (adminKey.value) void load()
})
</script>

<template>
  <main class="admin-shell">
    <section class="panel admin-login" aria-labelledby="admin-title">
      <span class="state-icon" aria-hidden="true"><Icon name="lucide:shield" size="1.5rem" /></span>
      <p class="eyebrow">Operação da instalação</p>
      <h1 id="admin-title">Administração</h1>
      <form class="stack" @submit.prevent="load">
        <label>Credencial administrativa<input v-model="adminKey" type="password" required autocomplete="current-password"></label>
        <button class="button primary" :disabled="busy" type="submit"><Icon :name="busy ? 'lucide:loader-circle' : 'lucide:key-round'" :class="{ spin: busy }" />{{ busy ? 'Verificando…' : 'Acessar mesas' }}</button>
      </form>
      <p v-if="error" class="form-error" role="alert"><Icon name="lucide:circle-alert" />{{ error }}</p>
    </section>

    <section v-if="authenticated" class="panel admin-campaigns" aria-live="polite">
      <div class="panel-heading">
        <div><p class="eyebrow"><Icon name="lucide:layout-list" />Mesas existentes</p><h2>{{ campaigns.length }} mesa(s)</h2></div>
        <button class="button ghost" :disabled="busy" @click="load"><Icon name="lucide:refresh-cw" :class="{ spin: busy }" />Atualizar</button>
      </div>
      <div v-if="!campaigns.length" class="empty-state">
        <span class="state-icon" aria-hidden="true"><Icon name="lucide:inbox" size="1.5rem" /></span>
        <p>Nenhuma mesa armazenada.</p>
      </div>
      <div v-else class="admin-table-wrap">
        <table class="admin-table">
          <thead><tr><th>Mesa</th><th>Presença</th><th>Jogador</th><th>Mestre</th><th><span class="sr-only">Ações</span></th></tr></thead>
          <tbody>
            <tr v-for="campaign in campaigns" :key="campaign.id">
              <td class="admin-campaign-cell"><strong>{{ campaign.name }}</strong><small><Icon name="lucide:calendar" />{{ new Date(campaign.createdAt).toLocaleString('pt-BR') }} · <Icon name="lucide:users" />{{ campaign.participantCount }} participante(s)</small></td>
              <td data-label="Presença"><span :class="['presence-dot', { online: campaign.onlineCount > 0 }]" />{{ campaign.onlineCount }} online</td>
              <td data-label="Jogador"><code>{{ campaign.playerCode }}</code></td>
              <td data-label="Mestre"><code>{{ campaign.masterCode }}</code></td>
              <td><button class="button ghost danger" :disabled="busy" type="button" @click="remove(campaign)"><Icon name="lucide:trash-2" />Excluir</button></td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>
  </main>
</template>
