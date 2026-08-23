<script setup lang="ts">
const store = useCampaignStore()
const api = useCompanionApi()
const name = ref('')
const modifier = ref(0)
const busy = ref(false)
const error = ref('')

async function execute(action: () => Promise<unknown>) {
  busy.value = true
  error.value = ''
  try { await action() } catch (exception) { error.value = (exception as Error).message } finally { busy.value = false }
}

async function createEncounter() {
  if (!store.session || !store.snapshot) return
  await execute(async () => { store.snapshot!.activeEncounter = await api.createEncounter(store.session!) })
}

async function addCombatant() {
  const encounter = store.snapshot?.activeEncounter
  if (!store.session || !encounter) return
  await execute(async () => {
    store.snapshot!.activeEncounter = await api.addCombatant(store.session!, encounter.id, name.value, modifier.value)
    name.value = ''
    modifier.value = 0
  })
}

async function rollAll() {
  const encounter = store.snapshot?.activeEncounter
  if (!store.session || !encounter) return
  await execute(async () => { store.snapshot!.activeEncounter = await api.rollInitiative(store.session!, encounter.id) })
}

async function start() {
  const encounter = store.snapshot?.activeEncounter
  if (!store.session || !encounter) return
  await execute(async () => { store.snapshot!.activeEncounter = await api.startEncounter(store.session!, encounter.id) })
}
</script>

<template>
  <section v-if="store.isMaster && !store.snapshot?.activeEncounter" class="panel encounter-empty">
    <p class="eyebrow">Próxima cena</p>
    <h2>Iniciativa</h2>
    <p>Abra um combate quando os aventureiros encontrarem perigo.</p>
    <button class="button secondary" :disabled="busy" type="button" @click="createEncounter">Iniciar preparação</button>
  </section>

  <section v-else-if="store.isMaster && store.snapshot?.activeEncounter?.status === 'Preparing'" class="panel encounter-setup" aria-labelledby="setup-title">
    <div class="panel-heading">
      <div><p class="eyebrow">Preparação</p><h2 id="setup-title">Combatentes</h2></div>
    </div>
    <form class="combatant-form" @submit.prevent="addCombatant">
      <label>Nome<input v-model="name" maxlength="60" required placeholder="Goblin arqueiro"></label>
      <label>Modificador<input v-model.number="modifier" type="number" min="-100" max="100" required></label>
      <button class="button secondary" :disabled="busy" type="submit">Adicionar</button>
    </form>
    <div class="button-row">
      <button class="button ghost" :disabled="busy || !store.snapshot.activeEncounter.combatants.length" type="button" @click="rollAll">
        {{ store.snapshot.activeEncounter.combatants.some(c => c.initiative !== null) ? 'Rolar iniciativas novamente' : 'Rolar iniciativas' }}
      </button>
      <button class="button primary" :disabled="busy || store.snapshot.activeEncounter.combatants.some(c => c.initiative === null) || !store.snapshot.activeEncounter.combatants.length" type="button" @click="start">Começar combate</button>
    </div>
    <p v-if="error" class="form-error" role="alert">{{ error }}</p>
  </section>
</template>
