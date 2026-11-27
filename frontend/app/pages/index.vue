<script setup lang="ts">
import InventoryPanel from '~/components/character/InventoryPanel.vue'
import PlayerRollPanel from '~/components/character/PlayerRollPanel.vue'
import MasterRollPanel from '~/components/MasterRollPanel.vue'
import CharacterSheetView from '~/components/character/CharacterSheetView.vue'
import BattlemapBoard from '~/components/battlemap/BattlemapBoard.vue'
import BattlemapSetup from '~/components/battlemap/BattlemapSetup.vue'
import type { DiceRoll, StatusCondition } from '~/types/campaign'
import { useGameMode } from '~/composables/useGameMode'
import CompendiumTabs from '~/components/CompendiumTabs.vue'
import SheetSummaryBar from '~/components/SheetSummaryBar.vue'
import CreatureSheetModal from '~/components/creatures/CreatureSheetModal.vue'
import CampaignNotebook from '~/components/campaign/CampaignNotebook.vue'
import { trapTab } from '~/utils/focus'
import type { DamageBreakdownPlan } from '~/utils/playerRoll'
const store = useCampaignStore()
const realtime = useCampaignRealtime()
const gameMode = useGameMode()
const loading = ref(true)
const editingCampaignName = ref(false)
const campaignNameDraft = ref('')
const campaignNameInput = ref<HTMLInputElement | null>(null)
const campaignNameError = ref('')
const pendingAttackRoll = ref<DiceRoll | null>(null)
const damageModalOpen = ref(false)

/** Damage expression to prefill after "acertou" (the player's weapon attack, spec 011); null for the quick roller. */
const pendingDamageExpression = ref<string | null>(null)
const pendingDamageSummary = ref<string | null>(null)
const pendingDamagePlan = ref<DamageBreakdownPlan | null>(null)

function onQuickRolledD20(roll: DiceRoll) { pendingDamageExpression.value = null; pendingDamageSummary.value = null; pendingDamagePlan.value = null; pendingAttackRoll.value = roll }
function onAttackHit() { pendingAttackRoll.value = null; damageModalOpen.value = true }
function onAttackDismissed() { pendingAttackRoll.value = null; pendingDamageExpression.value = null; pendingDamageSummary.value = null; pendingDamagePlan.value = null }
function onDamageSettled() { damageModalOpen.value = false; pendingDamageExpression.value = null; pendingDamageSummary.value = null; pendingDamagePlan.value = null }

async function beginCampaignNameEdit() {
  if (!store.isMaster || !store.snapshot) return
  campaignNameDraft.value = store.snapshot.campaign.name
  campaignNameError.value = ''
  editingCampaignName.value = true
  await nextTick()
  campaignNameInput.value?.focus()
  campaignNameInput.value?.select()
}

async function saveCampaignName() {
  if (!editingCampaignName.value) return
  const name = campaignNameDraft.value.trim()
  if (!name || name.length > 80) {
    campaignNameError.value = 'O título deve ter entre 1 e 80 caracteres.'
    return
  }
  editingCampaignName.value = false
  if (name !== store.snapshot?.campaign.name && !await store.renameCampaign(name)) {
    campaignNameError.value = store.error
    editingCampaignName.value = true
    await nextTick()
    campaignNameInput.value?.focus()
  }
}

function cancelCampaignNameEdit() {
  editingCampaignName.value = false
  campaignNameError.value = ''
}

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
async function commitTokenAppearance(tokenId: string, draft: { footprintSize: number; statusConditions: StatusCondition[] }) {
  await store.setTokenAppearance(tokenId, draft.footprintSize, draft.statusConditions)
}
async function createMarker(label: string) {
  await store.createMarker(label)
}
async function removeMarker(markerId: string) {
  await store.removeMarker(markerId)
}
async function commitFog(mask: string | null) {
  await store.setFog(mask)
}
async function setHitPointsVisibility(combatantId: string | null, hidden: boolean) {
  await store.setHitPointsVisibility(combatantId, hidden)
}
async function leave() {
  await realtime.disconnect()
  await store.leaveCampaign()
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
// Master's screen (spec 009): the table, or the full-width creature compendium. Kept in the URL.
const { view, go } = useAppView()
function onGameModeToggled() {
  // Game/full mode applies to the table. If toggled while viewing the sheet or campaign notes,
  // return to the table after applying the requested mode.
  if (view.value !== 'mesa') go('mesa')
}
const creatures = useCreatureStore()
const images = useImageCacheStore()
watch(() => store.isMaster, master => { if (!master) creatures.close() })
// Spec 011: the player's own sheet; a new session (another account) starts from scratch.
const character = useCharacterStore()
watch(() => store.session?.participantToken, () => character.reset())
// Modo Jogo of a player with a character: the roll panel replaces the quick roller (US4). The character
// is loaded on entering the Modo Jogo; without one (or while it fails to load) the quick roller stays.
const isPlayer = computed(() => store.session?.role === 'Player')
// Spec 012: the player rolls from the sheet in the full view too, not only in the Modo Jogo.
const showPlayerPanel = computed(() => isPlayer.value && Boolean(character.character))
// The token is part of the source: a new session resets the store above and loads its own character.
watch(() => [isPlayer.value, store.session?.participantToken] as const, ([wanted, token]) => {
  if (wanted && token && !character.character && !character.loading) void character.load()
}, { immediate: true })
/** Applying damage requires an active combatant. Configured extra damage can also be rolled outside combat. */
const playerInActiveEncounter = computed(() => {
  const encounter = store.snapshot?.activeEncounter
  return encounter?.status === 'Active' && encounter.combatants.some(combatant => combatant.participantId === store.session?.participantId)
})
function onPlayerAttackRolled(roll: DiceRoll, damageExpression: string | null, damageSummary: string | null, damagePlan: DamageBreakdownPlan | null) {
  if (!playerInActiveEncounter.value && !(damageExpression && damagePlan?.components.length)) return
  pendingDamageExpression.value = damageExpression
  pendingDamageSummary.value = damageSummary
  pendingDamagePlan.value = damagePlan
  pendingAttackRoll.value = roll
}
// Game-mode bag (FR-031, FR-039): a real modal. Focus enters on open, Tab stays inside, focus returns
// to the "Mochila" button on close. Esc is handled on the overlay itself, so an editor or the catalog
// opened from the bag (which stop their own Esc) closes first and keeps the bag open.
const inventoryOpen = ref(false)
const inventoryDialog = ref<HTMLElement | null>(null)
const inventoryHeading = ref<HTMLElement | null>(null)
function onInventoryKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    event.stopPropagation()
    inventoryOpen.value = false
    return
  }
  if (inventoryDialog.value) trapTab(event, inventoryDialog.value)
}
watch(inventoryOpen, async (open) => {
  await nextTick()
  if (open) inventoryHeading.value?.focus()
  else document.querySelector<HTMLElement>('[data-open-inventory]')?.focus()
})
// Board photos: participants' avatars plus library images of creature tokens.
watch(() => store.battlemap?.tokens.map(token => token.imageUrl).filter(Boolean).join('|'), () => {
  for (const token of store.battlemap?.tokens ?? []) void images.ensure(token.imageUrl)
}, { immediate: true })
const tokenPhotos = computed(() => {
  const photos: Record<string, string> = { ...store.avatarUrls }
  for (const token of store.battlemap?.tokens ?? []) {
    const url = token.imageUrl ? images.urls[token.imageUrl] : undefined
    if (url) photos[token.participantId] = url
  }
  return photos
})
function openCreatureSheet(tokenId: string, creatureId: string, name: string) {
  void creatures.open(creatureId, { id: tokenId, name })
}

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
    <header class="topbar" :class="{ 'has-view-nav': store.isMaster || store.session.role === 'Player' }">
      <div class="brand-inline"><span class="mini-mark" aria-hidden="true">20</span><strong>Companion</strong><span v-if="store.isMaster && store.snapshot" class="brand-campaign" :title="store.snapshot.campaign.name">{{ store.snapshot.campaign.name }}</span></div>
      <!-- display: contents on desktop (nav and campaign are grid columns); its own row on phones. -->
      <div class="topbar-center">
      <ViewNav />
      <div v-if="store.snapshot" class="campaign-heading">
        <input
          v-if="editingCampaignName"
          ref="campaignNameInput"
          v-model="campaignNameDraft"
          class="campaign-name campaign-name-input"
          aria-label="Título da mesa"
          maxlength="80"
          @keydown.enter.prevent="saveCampaignName"
          @keydown.esc.prevent="cancelCampaignNameEdit"
          @blur="saveCampaignName"
        >
        <span
          v-else
          class="campaign-name"
          :class="{ 'campaign-name-editable': store.isMaster }"
          :title="store.isMaster ? 'Dê um duplo clique para alterar o título' : store.snapshot.campaign.name"
          @dblclick.stop="beginCampaignNameEdit"
        >{{ store.snapshot.campaign.name }}</span>
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
        <span v-if="campaignNameError" class="campaign-name-error" role="alert">{{ campaignNameError }}</span>
      </div>
      </div>
      <div class="top-actions">
        <MasterCodeToggle />
        <GameModeToggle @toggled="onGameModeToggled" />
        <ConnectionStatus :status="store.connection" />
        <UserMenu @leave="leave" />
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
      <template v-if="view === 'campanha'">
        <CampaignNotebook />
      </template>
      <template v-else-if="view === 'ficha'">
        <p v-if="store.connection === 'offline'" class="offline-warning compendio-offline" role="alert">
          <Icon name="lucide:wifi-off" />Comandos pausados até a conexão voltar.
        </p>
        <CharacterSheetView />
      </template>
      <template v-else-if="view === 'compendio'">
        <!-- The Mesa shows this inside its welcome row; the Compendium has none, so it gets its own. -->
        <p v-if="store.connection === 'offline'" class="offline-warning compendio-offline" role="alert" data-compendio-offline>
          <Icon name="lucide:wifi-off" />Comandos pausados até a conexão voltar.
        </p>
        <CompendiumTabs />
      </template>
      <template v-else>
      <section v-if="!gameMode.active.value" class="welcome-row">
        <div><p class="eyebrow">Bem-vindo à mesa</p><h1>{{ store.session.displayName }}</h1></div>
        <p v-if="store.connection === 'offline'" class="offline-warning" role="alert">
          <Icon name="lucide:wifi-off" />Comandos pausados até a conexão voltar.
        </p>
      </section>


      <!-- Three zones: people (participants + combat), map, dice (roll + history). main.css lays them out as
           1, 2 or 3 columns by width: the map is the hero; on phones they stack as map, roll, combat,
           participants, history. -->
      <div class="dashboard-grid" :class="{ 'game-mode': gameMode.active.value }">
        <div class="table-zone table-people">
          <div v-if="!gameMode.active.value" class="table-participants">
            <ParticipantsPanel :participants="store.snapshot.participants" :avatars="store.avatarUrls" />
          </div>
          <!-- Turn order first: it is what the table checks most after the map; the creature catalog of
               the preparation goes under it (design review, round 2). -->
          <div class="table-combat">
            <InitiativeTracker />
            <CombatAttack />
            <EncounterSetup v-if="!gameMode.active.value" />
          </div>
        </div>
        <div class="table-zone table-map">
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
              :avatars="tokenPhotos"
              :combatants="store.snapshot.activeEncounter?.combatants ?? []"
              :encounter-status="store.snapshot.activeEncounter?.status ?? null"
              :participants="store.snapshot.participants"
              @move="moveBattlemapToken"
              @attack-area-commit="commitAttackArea"
              @attack-area-remove="removeAttackArea"
              @token-appearance-commit="commitTokenAppearance"
              @create-marker="createMarker"
              @marker-remove="removeMarker"
              :fog-saving="store.fogSaving"
              @fog-commit="commitFog"
              @hit-points-visibility="setHitPointsVisibility"
              @open-creature-sheet="openCreatureSheet"
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
          <section v-else class="panel battlemap-panel battlemap-idle" aria-labelledby="battlemap-idle-title">
            <span class="state-icon"><Icon name="lucide:map" size="1.6rem" /></span>
            <div>
              <h2 id="battlemap-idle-title">Battlemap</h2>
              <p>{{ store.isMaster ? 'Inicie a preparação de um combate para publicar um mapa.' : 'O mapa aparece aqui quando o mestre preparar um combate.' }}</p>
            </div>
          </section>
        </div>
        <!-- Rolling sits next to its results (proximity): the roll panel, then the public history. -->
        <div class="table-zone table-dice">
          <div class="table-roll">
            <MasterRollPanel v-if="store.isMaster" :game-mode="gameMode.active.value" @rolled-d20="onQuickRolledD20" />
            <PlayerRollPanel v-else-if="showPlayerPanel" :game-mode="gameMode.active.value" @attack-rolled="onPlayerAttackRolled" @open-inventory="inventoryOpen = true" />
            <QuickDiceRoller v-else-if="gameMode.active.value" @rolled-d20="onQuickRolledD20" />
            <DiceRoller v-else />
          </div>
          <div class="table-history">
            <RollHistory :rolls="store.snapshot.diceRolls" />
          </div>
        </div>
      </div>
      <SheetSummaryBar />
      </template>
    </div>

    <CreatureSheetModal v-if="store.isMaster && creatures.sheet" />
    <AttackConfirmModal
      v-if="pendingAttackRoll"
      :roll="pendingAttackRoll"
      :auto-dismiss="!pendingDamagePlan?.components.length"
      @hit="onAttackHit"
      @dismissed="onAttackDismissed"
    />
    <DamageRollModal
      v-if="damageModalOpen"
      :expression="pendingDamageExpression ?? undefined"
      :damage-summary="pendingDamageSummary"
      :damage-plan="pendingDamagePlan"
      @resolved="onDamageSettled"
      @cancelled="onDamageSettled"
    />
    <div
      v-if="inventoryOpen && showPlayerPanel"
      ref="inventoryDialog"
      class="modal-overlay"
      role="dialog"
      aria-modal="true"
      aria-labelledby="game-inventory-title"
      data-game-inventory
      @click.self="inventoryOpen = false"
      @keydown="onInventoryKeydown"
    >
      <!-- tabindex -1: a click on plain text keeps the focus inside the dialog, where Esc is listened. -->
      <div class="modal-panel game-inventory-modal" tabindex="-1">
        <div class="game-inventory-head">
          <h2 id="game-inventory-title" ref="inventoryHeading" tabindex="-1"><Icon name="lucide:backpack" />Mochila</h2>
          <button class="icon-button" type="button" aria-label="Fechar mochila" title="Fechar mochila" data-close-inventory @click="inventoryOpen = false"><Icon name="lucide:x" /></button>
        </div>
        <InventoryPanel compact hide-title />
      </div>
    </div>
  </main>
</template>
