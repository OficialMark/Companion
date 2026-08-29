<script setup lang="ts">
import BattlemapBoard from '~/components/battlemap/BattlemapBoard.vue'
import BattlemapSetup from '~/components/battlemap/BattlemapSetup.vue'
const store = useCampaignStore()
const realtime = useCampaignRealtime()
const loading = ref(true)

async function activate() {
  if (!store.session) { loading.value = false; return }
  loading.value = true
  try {
    await store.refresh()
    await realtime.connect()
  } catch {
    // Store exposes a recoverable message or clears an invalid session.
  } finally {
    loading.value = false
  }
}

async function battlemapConfigured() {
  await store.refreshBattlemap()
}

async function moveBattlemapToken(participantId: string, column: number, row: number) {
  await store.moveBattlemapToken(participantId, column, row)
}
async function leave() {
  await realtime.disconnect()
  store.clearSession()
}

async function copyCode() {
  const code = store.snapshot?.campaign.playerCode
  if (code && navigator.clipboard) await navigator.clipboard.writeText(code)
}

function handleOffline() {
  store.connection = 'offline'
}

function handleOnline() {
  void activate()
}

onMounted(async () => {
  window.addEventListener('offline', handleOffline)
  window.addEventListener('online', handleOnline)
  store.loadSession()
  await activate()
})

watch(() => store.session?.campaignId, async (current, previous) => {
  if (current && current !== previous) await activate()
})

onBeforeUnmount(() => {
  window.removeEventListener('offline', handleOffline)
  window.removeEventListener('online', handleOnline)
  void realtime.disconnect()
})
</script>

<template>
  <CampaignAccess v-if="!store.session" />
  <main v-else class="app-shell">
    <header class="topbar">
      <div class="brand-inline"><span class="mini-mark">20</span><strong>Companion</strong></div>
      <div v-if="store.snapshot" class="campaign-heading">
        <span>{{ store.snapshot.campaign.name }}</span>
        <button class="code-badge" type="button" title="Copiar código" @click="copyCode">
          {{ store.snapshot.campaign.playerCode }}
        </button>
      </div>
      <div class="top-actions">
        <ConnectionStatus :status="store.connection" />
        <button class="icon-button" type="button" title="Sair da campanha" aria-label="Sair da campanha" @click="leave">↗</button>
      </div>
    </header>

    <div v-if="loading" class="loading-state" role="status">Abrindo a mesa…</div>
    <div v-else-if="store.error && !store.snapshot" class="fatal-state">
      <h1>Não foi possível abrir a mesa</h1><p>{{ store.error }}</p>
      <button class="button primary" type="button" @click="activate">Tentar novamente</button>
    </div>
    <div v-else-if="store.snapshot" class="dashboard">
      <section class="welcome-row">
        <div><p class="eyebrow">Bem-vindo à mesa</p><h1>{{ store.session.displayName }}</h1></div>
        <p v-if="store.connection === 'offline'" class="offline-warning" role="alert">Comandos pausados até a conexão voltar.</p>
      </section>

      <section v-if="store.isMaster && store.session.masterCode" class="master-code-panel">
        <p><strong>Código reservado do mestre</strong> — guarde e não compartilhe com jogadores.</p>
        <details><summary>Mostrar código</summary><span class="code-badge">{{ store.session.masterCode }}</span></details>
      </section>

      <div class="dashboard-grid">
        <div class="main-column">
          <DiceRoller />
          <section v-if="store.snapshot.activeEncounter" class="panel battlemap-panel" aria-labelledby="battlemap-title">
            <div class="panel-heading">
              <div><p class="eyebrow">Cena compartilhada</p><h2 id="battlemap-title">Battlemap</h2></div>
              <span v-if="store.battlemap" class="count-pill">{{ store.battlemap.columns }} × {{ store.battlemap.rows }}</span>
            </div>
            <details v-if="store.isMaster" class="battlemap-config" :open="!store.battlemap">
              <summary>{{ store.battlemap ? 'Substituir mapa' : 'Adicionar mapa' }}</summary>
              <BattlemapSetup
                :encounter-id="store.snapshot.activeEncounter.id"
                :battlemap="store.battlemap"
                @configured="battlemapConfigured"
              />
            </details>
            <BattlemapBoard
              v-if="store.battlemap && store.battlemapImageSource"
              :battlemap="store.battlemap"
              :image-source="store.battlemapImageSource"
              :current-participant-id="store.session.participantId"
              :is-master="store.isMaster"
              :online="store.connection === 'online'"
              :pending-participant-id="store.pendingBattlemapTokenId"
              @move="moveBattlemapToken"
            />
            <div v-if="store.battlemapLoading" class="battlemap-loading" role="status">Sincronizando battlemap…</div>
            <div v-else-if="!store.battlemap && !store.isMaster" class="empty-state">O mestre ainda não publicou um battlemap.</div>
            <p v-if="store.error && store.battlemap" class="form-error" role="alert">{{ store.error }}</p>
          </section>
          <EncounterSetup />
          <InitiativeTracker />
          <CombatAttack />
        </div>
        <div class="side-column">
          <ParticipantsPanel :participants="store.snapshot.participants" />
          <RollHistory :rolls="store.snapshot.diceRolls" />
        </div>
      </div>
      <InitiativeModifier />
    </div>
  </main>
</template>
