<script setup lang="ts">
import { useGameMode } from '~/composables/useGameMode'
const store = useCampaignStore(); const api = useCompanionApi(); const gameMode = useGameMode(); const busy = ref(false); const error = ref('')
const drafts = reactive<Record<string, { name: string; modifier: number; hitPoints: number | null }>>({})
watch(() => store.snapshot?.activeEncounter?.combatants, (combatants) => { for (const combatant of combatants ?? []) drafts[combatant.id] = { name: combatant.name, modifier: combatant.initiativeModifier, hitPoints: combatant.currentHitPoints } }, { immediate: true, deep: true })

function formatModifier(value: number) { return `${value >= 0 ? '+' : ''}${value}` }

async function run(action: () => Promise<void>) {
  busy.value = true; error.value = ''
  try { await action() } catch (e) { error.value = (e as Error).message } finally { busy.value = false }
}
async function reroll(combatantId: string | null = null) {
  const encounter = store.snapshot?.activeEncounter; if (!store.session || !encounter) return
  await run(async () => { store.snapshot!.activeEncounter = await api.rollInitiative(store.session!, encounter.id, combatantId) })
}
async function saveCombatant(combatantId: string) {
  const encounter = store.snapshot?.activeEncounter; const draft = drafts[combatantId]; const current = encounter?.combatants.find(item => item.id === combatantId)
  if (!store.session || !encounter || !draft || !current) return
  await run(async () => {
    store.snapshot!.activeEncounter = encounter.status === 'Active' && draft.name === current.name && draft.modifier === current.initiativeModifier && draft.hitPoints !== null
      ? await api.updateHitPoints(store.session!, encounter.id, combatantId, draft.hitPoints)
      : await api.updateCombatant(store.session!, encounter.id, combatantId, draft.name, draft.modifier, draft.hitPoints)
  })
}
async function removeCombatant(combatantId: string) {
  const encounter = store.snapshot?.activeEncounter; if (!store.session || !encounter) return
  await run(async () => { store.snapshot!.activeEncounter = await api.removeCombatant(store.session!, encounter.id, combatantId); delete drafts[combatantId] })
}
async function saveOwnHitPoints(combatantId: string) {
  const encounter = store.snapshot?.activeEncounter; const draft = drafts[combatantId]
  if (!store.session || !encounter || draft?.hitPoints === null || draft?.hitPoints === undefined) return
  const hitPoints = draft.hitPoints
  await run(async () => { store.snapshot!.activeEncounter = await api.updateHitPoints(store.session!, encounter.id, combatantId, hitPoints) })
}
async function update(action: 'next' | 'end') {
  const encounter = store.snapshot?.activeEncounter; if (!store.session || !encounter) return
  await run(async () => {
    store.snapshot!.activeEncounter = action === 'next' ? await api.nextTurn(store.session!, encounter.id) : await api.endEncounter(store.session!, encounter.id)
    if (action === 'end') store.snapshot!.activeEncounter = null
  })
}
</script>
<template>
  <section v-if="store.snapshot?.activeEncounter" class="panel initiative-panel" aria-labelledby="initiative-title">
    <div class="panel-heading">
      <div><p class="eyebrow"><Icon name="lucide:swords" />Combate</p><h2 id="initiative-title">Iniciativa</h2></div>
      <span class="round-pill"><Icon :name="store.snapshot.activeEncounter.status === 'Active' ? 'lucide:hourglass' : 'lucide:clipboard-list'" />{{ store.snapshot.activeEncounter.status === 'Active' ? `Rodada ${store.snapshot.activeEncounter.round}` : 'Preparando' }}</span>
    </div>
    <p v-if="!store.snapshot.activeEncounter.combatants.length" class="empty-inline"><Icon name="lucide:users" />Nenhum combatente adicionado ainda.</p>
    <ol class="initiative-list">
      <li v-for="combatant in store.snapshot.activeEncounter.combatants" :key="combatant.id" :class="{ current: combatant.isCurrent, defeated: combatant.isDefeated }">
        <span class="initiative-value" :title="combatant.initiative === null ? 'Iniciativa ainda não rolada' : `Iniciativa ${combatant.initiative}`">
          <Icon v-if="combatant.isDefeated" name="lucide:skull" class="initiative-defeated-icon" />
          <template v-else>{{ combatant.initiative ?? '—' }}</template>
        </span>
        <span v-if="store.damageEffects?.[combatant.id]" :key="store.damageEffects?.[combatant.id]!.nonce" class="damage-pop" role="status" :aria-label="`${combatant.name} sofreu ${store.damageEffects?.[combatant.id]!.amount} de dano`" @animationend="store.clearDamageEffect(combatant.id, store.damageEffects?.[combatant.id]?.nonce)">-{{ store.damageEffects?.[combatant.id]!.amount }}</span>

        <span v-if="!store.isMaster" class="combatant-info">
          <span class="combatant-name">{{ combatant.name }}<span v-if="combatant.isDefeated" class="status-chip danger" title="Derrubado (0 de vida)"><Icon name="lucide:skull" />Caído</span></span>
          <small class="combatant-stats"><span title="Modificador de iniciativa"><Icon name="lucide:zap" />Mod. {{ formatModifier(combatant.initiativeModifier) }}</span></small>
          <span v-if="combatant.currentHitPoints !== null && combatant.participantId === store.session?.participantId && !gameMode.active.value" class="own-hit-points">
            <label :for="`own-combat-hit-points-${combatant.id}`"><Icon name="lucide:heart" />Vida</label>
            <input :id="`own-combat-hit-points-${combatant.id}`" v-model.number="drafts[combatant.id]!.hitPoints" type="number" min="0" max="99999">
            <button class="initiative-reroll" :disabled="busy" type="button" :aria-label="`Salvar vida de ${combatant.name}`" title="Salvar vida" @click="saveOwnHitPoints(combatant.id)"><Icon name="lucide:check" /></button>
          </span>
          <span v-else-if="combatant.currentHitPoints !== null" class="own-hit-points-readonly hp-chip"><Icon name="lucide:heart" />Vida {{ combatant.currentHitPoints }}</span>
        </span>
        <span v-else-if="gameMode.active.value" class="combatant-readonly combatant-info">
          <span class="combatant-name">{{ combatant.name }}<span v-if="combatant.isDefeated" class="status-chip danger" title="Derrubado (0 de vida)"><Icon name="lucide:skull" />Caído</span></span>
          <small class="combatant-stats">
            <span title="Modificador de iniciativa"><Icon name="lucide:zap" />Mod. {{ formatModifier(combatant.initiativeModifier) }}</span>
            <span v-if="combatant.currentHitPoints !== null" class="hp-chip"><Icon name="lucide:heart" />Vida {{ combatant.currentHitPoints }}</span>
          </small>
        </span>
        <span v-else class="combatant-editor">
          <label v-if="drafts[combatant.id]" class="inline-field inline-field-name" title="Nome">
            <input v-model="drafts[combatant.id]!.name" :aria-label="`Nome de ${combatant.name}`" maxlength="60">
          </label>
          <label v-if="drafts[combatant.id]" class="inline-field" title="Modificador de iniciativa">
            <Icon name="lucide:zap" />
            <input v-model.number="drafts[combatant.id]!.modifier" :aria-label="`Modificador de ${combatant.name}`" type="number" min="-100" max="100">
          </label>
          <label v-if="drafts[combatant.id]" class="inline-field hp-field" title="Vida">
            <Icon name="lucide:heart" />
            <input v-model.number="drafts[combatant.id]!.hitPoints" :aria-label="`Vida de ${combatant.name}`" type="number" min="0" max="99999">
          </label>
        </span>

        <span class="initiative-actions">
          <span v-if="combatant.isDefeated && store.isMaster && !gameMode.active.value" class="status-chip danger" title="Derrubado (0 de vida)"><Icon name="lucide:skull" />Caído</span>
          <span v-if="combatant.isCurrent" class="turn-marker"><Icon name="lucide:play" />Turno</span>
          <template v-if="store.isMaster">
            <button class="initiative-reroll" :disabled="busy" type="button" :aria-label="`Rolar iniciativa novamente para ${combatant.name}`" title="Rolar iniciativa novamente" @click="reroll(combatant.id)"><Icon name="lucide:dices" /></button>
            <button class="initiative-reroll" :disabled="busy" type="button" :aria-label="`Salvar ${combatant.name}`" title="Salvar alterações" @click="saveCombatant(combatant.id)"><Icon name="lucide:check" /></button>
            <button class="initiative-reroll danger" :disabled="busy" type="button" :aria-label="`Remover ${combatant.name}`" title="Remover do combate" @click="removeCombatant(combatant.id)"><Icon name="lucide:trash-2" /></button>
          </template>
        </span>
      </li>
    </ol>
    <div v-if="store.isMaster && store.snapshot.activeEncounter.status === 'Active'" class="button-row">
      <button class="button primary" :disabled="busy" type="button" @click="update('next')"><Icon name="lucide:skip-forward" />Próximo turno</button>
      <button class="button ghost" :disabled="busy" type="button" @click="reroll()"><Icon name="lucide:dices" />Rolar todas novamente</button>
      <button class="button ghost danger push-end" :disabled="busy" type="button" @click="update('end')"><Icon name="lucide:flag" />Encerrar</button>
    </div>
    <p v-if="error" class="form-error" role="alert"><Icon name="lucide:circle-alert" />{{ error }}</p>
  </section>
</template>
