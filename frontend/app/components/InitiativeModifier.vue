<script setup lang="ts">
import { RELOAD_DISCARDS_EDITS, useSheetAutosaveGuards } from '~/stores/character'

/**
 * "Perfil de combate" of a player at the Mesa. Without a character sheet the player types initiative
 * and hit points here, as before spec 011. With a sheet (FR-010) initiative and max HP come from it,
 * read-only, and current HP is edited through the sheet so both screens show the same numbers.
 */
const store = useCampaignStore()
const api = useCompanionApi()
const character = useCharacterStore()
const { go } = useAppView()
useSheetAutosaveGuards()

const modifier = ref<number | null>(null)
const hitPoints = ref<number | null>(null)
const maxHitPoints = ref<number | null>(null)
const busy = ref(false)
const message = ref('')
const error = ref('')
watch(() => store.snapshot?.currentParticipant, (participant) => { modifier.value = participant?.initiativeModifier ?? null; hitPoints.value = participant?.currentHitPoints ?? null; maxHitPoints.value = participant?.maxHitPoints ?? null }, { immediate: true, deep: true })

/** The sheet is the source once it exists, whether it was found here or loaded by the Ficha or the Modo Jogo. */
const fromSheet = computed(() => Boolean(character.character && character.draft && character.stats))
watch(() => [store.isMaster, store.session?.participantToken] as const, ([master, token]) => {
  if (!master && token !== undefined && store.session) void character.loadExisting()
}, { immediate: true })

const SAVE_LABEL: Record<string, string> = { saving: 'Salvando…', saved: 'Salvo', error: 'Não foi possível salvar', conflict: 'A ficha mudou em outro aparelho' }
const initiativeText = computed(() => {
  const value = character.stats?.initiative.value ?? 0
  return value >= 0 ? `+${value}` : String(value)
})
function editCurrentHitPoints(event: Event) {
  const raw = (event.target as HTMLInputElement).valueAsNumber
  if (Number.isNaN(raw)) return
  character.editSheet({ currentHitPoints: Math.min(99999, Math.max(0, Math.trunc(raw))) })
}

/** Same rule as the Ficha: the newer server version wins, so unsaved HP is discarded only after asking. */
function reloadSheet() {
  if (character.stale && !window.confirm(RELOAD_DISCARDS_EDITS)) return
  void character.load()
}

async function save() {
  if (!store.session || !store.snapshot) return
  busy.value = true; message.value = ''; error.value = ''
  try { const updated = await api.updateCombatProfile(store.session, modifier.value, hitPoints.value, maxHitPoints.value); store.snapshot.currentParticipant = updated; const index = store.snapshot.participants.findIndex(participant => participant.id === updated.id); if (index >= 0) store.snapshot.participants[index] = updated; message.value = 'Perfil de combate salvo.' }
  catch (exception) {
    error.value = (exception as Error).message
    // 409: a sheet was created meanwhile (another tab or device). Switch to the values of the sheet.
    if ((exception as Error & { status?: number }).status === 409) await character.loadExisting()
  }
  finally { busy.value = false }
}
</script>
<template>
  <section v-if="!store.isMaster" class="panel modifier-panel" aria-labelledby="modifier-title" :data-combat-profile="fromSheet ? 'sheet' : 'manual'">
    <div><p class="eyebrow"><Icon name="lucide:user-round" />Seu personagem</p><h2 id="modifier-title">Perfil de combate</h2><p v-if="fromSheet" data-from-sheet-hint>Vêm da sua ficha: iniciativa e vida máxima mudam pela aba Ficha.</p><p v-else>O mestre usará estes valores ao preparar os jogadores online.</p></div>
    <div v-if="fromSheet" class="modifier-form combat-profile-sheet" data-combat-from-sheet>
      <div class="combat-profile-field"><span class="label-with-icon"><Icon name="lucide:zap" />Modificador de iniciativa</span><strong class="combat-profile-value" data-sheet-initiative>{{ initiativeText }}</strong></div>
      <label class="combat-profile-field" for="own-hit-points"><span class="label-with-icon"><Icon name="lucide:heart" />Vida atual</span><input id="own-hit-points" type="number" inputmode="numeric" min="0" :max="character.stats?.maxHitPoints.value" step="1" :value="character.draft?.currentHitPoints" data-sheet-current-hp @input="editCurrentHitPoints" @change="character.flush()"></label>
      <div class="combat-profile-field"><span class="label-with-icon"><Icon name="lucide:heart-pulse" />Vida máxima</span><strong class="combat-profile-value" data-sheet-max-hp>{{ character.stats?.maxHitPoints.value }}</strong></div>
      <button class="button secondary" type="button" data-open-sheet @click="go('ficha')"><Icon name="lucide:scroll-text" />Abrir ficha</button>
    </div>
    <form v-else class="modifier-form" @submit.prevent="save">
      <label class="combat-profile-field" for="own-initiative-modifier"><span class="label-with-icon"><Icon name="lucide:zap" />Modificador de iniciativa</span><input id="own-initiative-modifier" v-model.number="modifier" type="number" min="-100" max="100" placeholder="0"></label>
      <label class="combat-profile-field" for="own-hit-points"><span class="label-with-icon"><Icon name="lucide:heart" />Vida atual</span><input id="own-hit-points" v-model.number="hitPoints" type="number" min="0" max="99999" step="1" placeholder="Não informada"></label>
      <label class="combat-profile-field" for="own-max-hit-points"><span class="label-with-icon"><Icon name="lucide:heart-pulse" />Vida máxima</span><input id="own-max-hit-points" v-model.number="maxHitPoints" type="number" min="0" max="99999" step="1" placeholder="Não informada"></label>
      <button class="button secondary" :disabled="busy || store.connection === 'offline'" type="submit"><Icon :name="busy ? 'lucide:loader-circle' : 'lucide:save'" :class="{ spin: busy }" />{{ busy ? 'Salvando…' : 'Salvar' }}</button>
    </form>
    <p v-if="fromSheet && SAVE_LABEL[character.saveState]" class="field-hint" role="status" :data-sheet-save-state="character.saveState">
      {{ SAVE_LABEL[character.saveState] }}
      <button v-if="character.saveState === 'error'" type="button" class="button ghost small" data-retry-save @click="character.flush()">Tentar de novo</button>
      <button v-else-if="character.saveState === 'conflict'" type="button" class="button ghost small" data-reload-sheet @click="reloadSheet">Recarregar ficha</button>
    </p>
    <template v-else-if="!fromSheet"><p v-if="message" class="form-success" role="status"><Icon name="lucide:circle-check" />{{ message }}</p><p v-if="error" class="form-error" role="alert"><Icon name="lucide:circle-alert" />{{ error }}</p></template>
  </section>
</template>
