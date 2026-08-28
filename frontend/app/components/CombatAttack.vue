<script setup lang="ts">
import type { DiceRoll } from '~/types/campaign'
import { createUuid } from '~/utils/createUuid'
const store = useCampaignStore(); const requestId = ref(''); const api = useCompanionApi(); const open = ref(false); const roll = ref<DiceRoll | null>(null); const selected = ref<string[]>([]); const busy = ref(false); const error = ref(''); const message = ref('')
const encounter = computed(() => store.snapshot?.activeEncounter ?? null)
const canAttack = computed(() => encounter.value?.status === 'Active' && (store.isMaster || encounter.value.combatants.some(c => c.participantId === store.session?.participantId)))
function rolled(result: DiceRoll) { roll.value = result; requestId.value = createUuid(); selected.value = []; error.value = '' }
function cancel() { open.value = false; roll.value = null; selected.value = []; error.value = '' }
async function confirm() {
  if (!store.session || !encounter.value || !roll.value || selected.value.length === 0) return
  busy.value = true; error.value = ''; message.value = ''
  try { const result = await api.resolveAttack(store.session, encounter.value.id, roll.value.id, selected.value, requestId.value); if (store.snapshot) store.snapshot.activeEncounter = result.encounter; message.value = `Ataque de ${result.attack.damage} aplicado.`; open.value = false; roll.value = null; selected.value = [] }
  catch (exception) { error.value = (exception as Error).message }
  finally { busy.value = false }
}
</script>
<template>
  <section v-if="canAttack" class="panel attack-panel" aria-labelledby="attack-title">
    <div class="panel-heading"><div><p class="eyebrow">Ação livre</p><h2 id="attack-title">Ataque</h2></div><button v-if="!open" class="button primary attack-open" :disabled="store.connection === 'offline'" type="button" @click="open = true">Atacar</button></div>
    <template v-if="open">
      <DiceRoller v-if="!roll" mode="attack" @rolled="rolled" />
      <div v-else class="attack-targets"><p>Resultado: <strong>{{ roll.total }}</strong>. Selecione os alvos:</p><label v-for="combatant in encounter?.combatants" :key="combatant.id" class="attack-target"><input v-model="selected" type="checkbox" :value="combatant.id">{{ combatant.name }}</label><div class="button-row"><button class="button primary attack-confirm" :disabled="busy || selected.length === 0" type="button" @click="confirm">{{ busy ? 'Aplicando…' : 'Confirmar dano' }}</button><button class="button ghost attack-cancel" :disabled="busy" type="button" @click="cancel">Cancelar</button></div></div>
      <button v-if="!roll" class="button ghost attack-cancel" type="button" @click="cancel">Cancelar</button>
    </template>
    <p v-if="message" class="form-success" role="status">{{ message }}</p><p v-if="error" class="form-error" role="alert">{{ error }}</p>
  </section>
</template>