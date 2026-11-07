<script setup lang="ts">
const store = useCampaignStore(); const api = useCompanionApi(); const name = ref(''); const modifier = ref(0); const hitPoints = ref<number | null>(null); const busy = ref(false); const error = ref('')
async function execute(action: () => Promise<unknown>) { busy.value = true; error.value = ''; try { await action() } catch (exception) { error.value = (exception as Error).message } finally { busy.value = false } }
async function createEncounter() { if (!store.session || !store.snapshot) return; await execute(async () => { store.snapshot!.activeEncounter = await api.createEncounter(store.session!) }) }
async function addCombatant() { const encounter = store.snapshot?.activeEncounter; if (!store.session || !encounter) return; await execute(async () => { store.snapshot!.activeEncounter = await api.addCombatant(store.session!, encounter.id, name.value, modifier.value, hitPoints.value); name.value = ''; modifier.value = 0; hitPoints.value = null }) }
async function rollAll() { const encounter = store.snapshot?.activeEncounter; if (!store.session || !encounter) return; await execute(async () => { store.snapshot!.activeEncounter = await api.rollInitiative(store.session!, encounter.id) }) }
async function start() { const encounter = store.snapshot?.activeEncounter; if (!store.session || !encounter) return; await execute(async () => { store.snapshot!.activeEncounter = await api.startEncounter(store.session!, encounter.id) }) }
const combatants = computed(() => store.snapshot?.activeEncounter?.combatants ?? [])
const missingInitiative = computed(() => combatants.value.some(c => c.initiative === null))
const missingHitPoints = computed(() => combatants.value.some(c => c.currentHitPoints === null))
</script>
<template>
  <section v-if="store.isMaster && !store.snapshot?.activeEncounter" class="panel encounter-empty">
    <span class="state-icon" aria-hidden="true"><Icon name="lucide:swords" size="1.5rem" /></span>
    <div>
      <p class="eyebrow">Próxima cena</p>
      <h2>Iniciativa</h2>
      <p>Abra um combate quando os aventureiros encontrarem perigo.</p>
    </div>
    <button class="button secondary" :disabled="busy" type="button" @click="createEncounter"><Icon name="lucide:plus" />Iniciar preparação</button>
  </section>
  <section v-else-if="store.isMaster && store.snapshot?.activeEncounter?.status === 'Preparing'" class="panel encounter-setup" aria-labelledby="setup-title">
    <div class="panel-heading"><div><p class="eyebrow"><Icon name="lucide:clipboard-list" />Preparação</p><h2 id="setup-title">Combatentes</h2></div></div>
    <form class="combatant-form" @submit.prevent="addCombatant">
      <label>Nome<input v-model="name" maxlength="60" required placeholder="Goblin arqueiro"></label>
      <label><span class="label-with-icon"><Icon name="lucide:zap" />Modificador</span><input v-model.number="modifier" type="number" min="-100" max="100" required></label>
      <label><span class="label-with-icon"><Icon name="lucide:heart" />Vida</span><input id="new-combatant-hit-points" v-model.number="hitPoints" type="number" min="0" max="99999" step="1" required></label>
      <button class="button secondary" :disabled="busy" type="submit"><Icon name="lucide:user-plus" />Adicionar</button>
    </form>
    <div class="button-row">
      <button class="button ghost" :disabled="busy" type="button" @click="rollAll"><Icon name="lucide:dices" />{{ combatants.some(c => c.initiative !== null) ? 'Rolar iniciativas novamente' : 'Rolar iniciativas' }}</button>
      <button class="button primary" :disabled="busy || missingInitiative || missingHitPoints || !combatants.length" type="button" @click="start"><Icon name="lucide:play" />Começar combate</button>
    </div>
    <p v-if="combatants.length && (missingInitiative || missingHitPoints)" class="field-hint">
      <Icon name="lucide:info" />Para começar, todos precisam de {{ missingInitiative && missingHitPoints ? 'iniciativa rolada e vida definida' : missingInitiative ? 'iniciativa rolada' : 'vida definida' }}.
    </p>
    <p v-if="error" class="form-error" role="alert"><Icon name="lucide:circle-alert" />{{ error }}</p>
  </section>
</template>
