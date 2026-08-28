<script setup lang="ts">
const store = useCampaignStore()
const api = useCompanionApi()
const busy = ref(false)
const error = ref('')
const drafts = reactive<Record<string, { name: string; modifier: number }>>({})

watch(() => store.snapshot?.activeEncounter?.combatants, (combatants) => {
  for (const combatant of combatants ?? []) {
    if (!drafts[combatant.id]) drafts[combatant.id] = { name: combatant.name, modifier: combatant.initiativeModifier }
  }
}, { immediate: true, deep: true })

async function reroll(combatantId: string | null = null) {
  const encounter = store.snapshot?.activeEncounter
  if (!store.session || !encounter) return
  busy.value = true
  error.value = ''
  try {
    store.snapshot!.activeEncounter = await api.rollInitiative(store.session, encounter.id, combatantId)
  } catch (exception) {
    error.value = (exception as Error).message
  } finally {
    busy.value = false
  }
}

async function saveCombatant(combatantId: string) {
  const encounter = store.snapshot?.activeEncounter
  const draft = drafts[combatantId]
  if (!store.session || !encounter || !draft) return
  busy.value = true
  error.value = ''
  try {
    store.snapshot!.activeEncounter = await api.updateCombatant(
      store.session, encounter.id, combatantId, draft.name, draft.modifier)
  } catch (exception) {
    error.value = (exception as Error).message
  } finally {
    busy.value = false
  }
}

async function removeCombatant(combatantId: string) {
  const encounter = store.snapshot?.activeEncounter
  if (!store.session || !encounter) return
  busy.value = true
  error.value = ''
  try {
    store.snapshot!.activeEncounter = await api.removeCombatant(store.session, encounter.id, combatantId)
    delete drafts[combatantId]
  } catch (exception) {
    error.value = (exception as Error).message
  } finally {
    busy.value = false
  }
}

async function update(action: 'next' | 'end') {
  const encounter = store.snapshot?.activeEncounter
  if (!store.session || !encounter) return
  busy.value = true
  error.value = ''
  try {
    store.snapshot!.activeEncounter = action === 'next'
      ? await api.nextTurn(store.session, encounter.id)
      : await api.endEncounter(store.session, encounter.id)
    if (action === 'end') store.snapshot!.activeEncounter = null
  } catch (exception) {
    error.value = (exception as Error).message
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <section v-if="store.snapshot?.activeEncounter" class="panel initiative-panel" aria-labelledby="initiative-title">
    <div class="panel-heading">
      <div><p class="eyebrow">Combate</p><h2 id="initiative-title">Iniciativa</h2></div>
      <span class="round-pill">{{ store.snapshot.activeEncounter.status === 'Active' ? `Rodada ${store.snapshot.activeEncounter.round}` : 'Preparando' }}</span>
    </div>
    <ol class="initiative-list">
      <li v-for="combatant in store.snapshot.activeEncounter.combatants" :key="combatant.id" :class="{ current: combatant.isCurrent }">
        <span class="initiative-value">{{ combatant.initiative ?? '—' }}</span>
        <span v-if="!store.isMaster">{{ combatant.name }}<small>Mod. {{ combatant.initiativeModifier >= 0 ? '+' : '' }}{{ combatant.initiativeModifier }}</small></span>
        <span v-else class="combatant-editor">
          <input v-if="drafts[combatant.id]" v-model="drafts[combatant.id]!.name" :aria-label="`Nome de ${combatant.name}`" maxlength="60">
          <input v-if="drafts[combatant.id]" v-model.number="drafts[combatant.id]!.modifier" :aria-label="`Modificador de ${combatant.name}`" type="number" min="-100" max="100">
        </span>
        <span class="initiative-actions">
          <button
            v-if="store.isMaster"
            class="initiative-reroll"
            :disabled="busy"
            type="button"
            :aria-label="`Rolar iniciativa novamente para ${combatant.name}`"
            title="Rolar novamente"
            @click="reroll(combatant.id)"
          >↻</button>
          <button v-if="store.isMaster" class="initiative-reroll" :disabled="busy" type="button" :aria-label="`Salvar ${combatant.name}`" title="Salvar" @click="saveCombatant(combatant.id)">✓</button>
          <button v-if="store.isMaster" class="initiative-reroll danger" :disabled="busy" type="button" :aria-label="`Remover ${combatant.name}`" title="Remover" @click="removeCombatant(combatant.id)">×</button>
          <span v-if="combatant.isCurrent" class="turn-marker">Turno</span>
        </span>
      </li>
    </ol>
    <div v-if="store.isMaster && store.snapshot.activeEncounter.status === 'Active'" class="button-row">
      <button class="button ghost" :disabled="busy" type="button" @click="reroll()">Rolar todas novamente</button>
      <button class="button primary" :disabled="busy" type="button" @click="update('next')">Próximo turno</button>
      <button class="button ghost danger" :disabled="busy" type="button" @click="update('end')">Encerrar</button>
    </div>
    <p v-if="error" class="form-error" role="alert">{{ error }}</p>
  </section>
</template>
