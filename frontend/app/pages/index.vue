<script setup lang="ts">
import BattlemapBoard from '~/components/battlemap/BattlemapBoard.vue'
import BattlemapSetup from '~/components/battlemap/BattlemapSetup.vue'
import type { DiceRoll } from '~/types/campaign'
import { useGameMode } from '~/composables/useGameMode'
const store = useCampaignStore()
const realtime = useCampaignRealtime()
const gameMode = useGameMode()
const loading = ref(true)
const pendingAttackRoll = ref<DiceRoll | null>(null)
const damageModalOpen = ref(false)

function onQuickRolledD20(roll: DiceRoll) { pendingAttackRoll.value = roll }
function onAttackHit() { pendingAttackRoll.value = null; damageModalOpen.value = true }
function onAttackDismissed() { pendingAttackRoll.value = null }
function onDamageSettled() { damageModalOpen.value = false }

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

async function moveBattlemapToken(participantId: string, column: number | null, row: number | null) {
  await store.moveBattlemapToken(participantId, column, row)
}
async function commitAttackArea(tokenId: string, draft: { shape: 'Circle' | 'Square' | 'Cone' | 'Cylinder'; radius: number; width: number | null; directionDegrees: number | null }) {
  await store.setAttackArea(tokenId, draft.shape, draft.radius, draft.width, draft.directionDegrees)
}
async function removeAttackArea(tokenId: string) {
  await store.removeAttackArea(tokenId)
}
async function leave() {
  await realtime.disconnect()
  store.clearSession()
}

const codeCopied = ref(false)
let copiedTimer: ReturnType<typeof setTimeout> | undefined
async function copyCode() {
  const code = store.snapshot?.campaign.playerCode
  if (!code || !navigator.clipboard) return
  await navigator.clipboard.writeText(code)
  codeCopied.value = true
  if (copiedTimer) clearTimeout(copiedTimer)
  copiedTimer = setTimeout(() => { codeCopied.value = false }, 1800)
}

// Tokens whose combatant dropped to 0 HP get a "defeated" marker on the battlemap.
const defeatedTokenIds = computed(() => (store.snapshot?.activeEncounter?.combatants ?? [])
  .filter(combatant => combatant.isDefeated)
  .flatMap(combatant => combatant.participantId ? [combatant.participantId, combatant.id] : [combatant.id]))

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
  if (copiedTimer) clearTimeout(copiedTimer)
  void realtime.disconnect()
})
</script>

<template>
  <CampaignAccess v-if="!store.session" />
  <main v-else class="app-shell">
    <header class="topbar">
      <div class="brand-inline"><span class="mini-mark" aria-hidden="true">20</span><strong>Companion</strong></div>
      <div v-if="store.snapshot" class="campaign-heading">
        <span class="campaign-name" :title="store.snapshot.campaign.name">{{ store.snapshot.campaign.name }}</span>
        <button
          class="code-badge"
          :class="{ copied: codeCopied }"
          type="button"
          :title="codeCopied ? 'Código copiado' : 'Copiar código de jogador'"
          :aria-label="`Código de jogador ${store.snapshot.campaign.playerCode}. ${codeCopied ? 'Copiado' : 'Copiar'}`"
          @click="copyCode"
        >
          <Icon :name="codeCopied ? 'lucide:check' : 'lucide:copy'" />
          {{ store.snapshot.campaign.playerCode }}
        </button>
      </div>
      <div class="top-actions">
        <GameModeToggle />
        <ConnectionStatus :status="store.connection" />
        <button class="icon-button" type="button" title="Sair da campanha" aria-label="Sair da campanha" @click="leave">
          <Icon name="lucide:log-out" />
        </button>
      </div>
    </header>

    <div v-if="loading" class="loading-state" role="status">
      <Icon name="lucide:loader-circle" class="spin" size="2rem" />
      <span>Abrindo a mesa…</span>
    </div>
    <div v-else-if="store.error && !store.snapshot" class="fatal-state">
      <span class="state-icon danger"><Icon name="lucide:circle-alert" size="1.8rem" /></span>
      <h1>Não foi possível abrir a mesa</h1><p>{{ store.error }}</p>
      <button class="button primary" type="button" @click="activate"><Icon name="lucide:refresh-cw" />Tentar novamente</button>
    </div>
    <div v-else-if="store.snapshot" class="dashboard">
      <section v-if="!gameMode.active.value" class="welcome-row">
        <div><p class="eyebrow">Bem-vindo à mesa</p><h1>{{ store.session.displayName }}</h1></div>
        <p v-if="store.connection === 'offline'" class="offline-warning" role="alert">
          <Icon name="lucide:wifi-off" />Comandos pausados até a conexão voltar.
        </p>
      </section>

      <section v-if="!gameMode.active.value && store.isMaster && store.session.masterCode" class="master-code-panel">
        <span class="master-code-icon" aria-hidden="true"><Icon name="lucide:key-round" /></span>
        <p><strong>Código reservado do mestre</strong> — guarde e não compartilhe com jogadores.</p>
        <details>
          <summary><Icon name="lucide:eye" />Mostrar código</summary>
          <span class="code-badge">{{ store.session.masterCode }}</span>
        </details>
      </section>

      <div class="dashboard-grid" :class="{ 'game-mode': gameMode.active.value }">
        <div class="main-column">
          <QuickDiceRoller v-if="gameMode.active.value" @rolled-d20="onQuickRolledD20" />
          <DiceRoller v-else />
          <section v-if="store.snapshot.activeEncounter" class="panel battlemap-panel" aria-labelledby="battlemap-title">
            <div class="panel-heading">
              <div><p class="eyebrow"><Icon name="lucide:map" />Cena compartilhada</p><h2 id="battlemap-title">Battlemap</h2></div>
              <span v-if="store.battlemap" class="count-pill" title="Colunas × linhas"><Icon name="lucide:grid-3x3" />{{ store.battlemap.columns }} × {{ store.battlemap.rows }}</span>
            </div>
            <details v-if="store.isMaster" class="battlemap-config" :open="!store.battlemap">
              <summary>
                <Icon :name="store.battlemap ? 'lucide:image-up' : 'lucide:image-plus'" />
                <span>{{ store.battlemap ? 'Substituir mapa' : 'Adicionar mapa' }}</span>
                <Icon name="lucide:chevron-right" class="summary-chevron" />
              </summary>
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
              :defeated-ids="defeatedTokenIds"
              @move="moveBattlemapToken"
              @attack-area-commit="commitAttackArea"
              @attack-area-remove="removeAttackArea"
            />
            <div v-if="store.battlemapLoading" class="battlemap-loading" role="status">
              <Icon name="lucide:loader-circle" class="spin" size="1.6rem" />Sincronizando battlemap…
            </div>
            <div v-else-if="!store.battlemap && !store.isMaster" class="empty-state">
              <span class="state-icon"><Icon name="lucide:map" size="1.6rem" /></span>
              <p>O mestre ainda não publicou um battlemap.</p>
            </div>
            <p v-if="store.error && store.battlemap" class="form-error" role="alert"><Icon name="lucide:circle-alert" />{{ store.error }}</p>
          </section>
          <EncounterSetup v-if="!gameMode.active.value" />
          <InitiativeTracker />
          <CombatAttack />
        </div>
        <div class="side-column">
          <ParticipantsPanel v-if="!gameMode.active.value" :participants="store.snapshot.participants" />
          <RollHistory :rolls="store.snapshot.diceRolls" />
        </div>
      </div>
      <InitiativeModifier />
    </div>

    <AttackConfirmModal
      v-if="pendingAttackRoll"
      :roll="pendingAttackRoll"
      @hit="onAttackHit"
      @dismissed="onAttackDismissed"
    />
    <DamageRollModal
      v-if="damageModalOpen"
      @resolved="onDamageSettled"
      @cancelled="onDamageSettled"
    />
  </main>
</template>
