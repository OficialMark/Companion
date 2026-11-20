<script setup lang="ts">
import type { DiceRoll } from '~/types/campaign'
import type { Creature, CreatureAction, RollPreset } from '~/types/creatures'
import CreatureRollPanel from '~/components/creatures/CreatureRollPanel.vue'
import { DEFAULT_PRESET, parseActionRolls } from '~/utils/creatureRoll'

/**
 * The master's roll panel at the Mesa (spec 012, US1): pick one of the creatures on the table and roll
 * with its numbers (the creature roll panel of spec 008), its action shortcuts one tap away; or keep the
 * free roller. "On the table" means the active encounter's combatants linked to a library creature.
 */
const props = withDefaults(defineProps<{ gameMode?: boolean }>(), { gameMode: false })
const emit = defineEmits<{ 'rolled-d20': [roll: DiceRoll] }>()

const FREE = 'free'
const store = useCampaignStore()
const api = useCompanionApi()
const creatures = useCreatureStore()

const candidates = computed(() => (store.snapshot?.activeEncounter?.combatants ?? [])
  .filter(combatant => Boolean(combatant.creatureId))
  .map(combatant => ({ id: combatant.id, name: combatant.name, creatureId: combatant.creatureId as string })))

const selected = ref(FREE)
const current = computed(() => candidates.value.find(candidate => candidate.id === selected.value) ?? null)
const creature = ref<Creature | null>(null)
const loading = ref(false)
const error = ref('')
const notice = ref('')
const presets = reactive(new Map<string, RollPreset>())
const preset = computed<RollPreset>(() => (current.value ? presets.get(current.value.id) : null) ?? { ...DEFAULT_PRESET })

// Creature sheets do not change during a roll: one fetch per creature while the page is open.
const cache = new Map<string, Creature>()
let sequence = 0
watch(current, async (next) => {
  const mine = ++sequence
  creature.value = null; error.value = ''
  if (!next || !store.session) return
  const cached = cache.get(next.creatureId)
  if (cached) { creature.value = cached; return }
  loading.value = true
  try {
    const loaded = await api.getCreature(store.session, next.creatureId)
    cache.set(next.creatureId, loaded)
    if (mine === sequence) creature.value = loaded
  } catch (exception) {
    if (mine === sequence) error.value = (exception as Error).message
  } finally {
    if (mine === sequence) loading.value = false
  }
})

// A creature that leaves the table (encounter ended, combatant removed) falls back to the free roll.
watch(candidates, (list, previous) => {
  if (selected.value === FREE || list.some(candidate => candidate.id === selected.value)) return
  const gone = previous?.find(candidate => candidate.id === selected.value)?.name ?? 'A criatura'
  selected.value = FREE
  notice.value = `${gone} saiu da mesa: voltou para a rolagem livre.`
})
watch(selected, (value) => { if (value !== FREE) notice.value = '' })

const actions = computed(() => {
  const sheet = creature.value
  if (!sheet) return []
  return [...sheet.actions, ...sheet.bonusActions, ...sheet.reactions, ...sheet.legendaryActions]
    .map((action: CreatureAction) => ({ name: action.name, rolls: parseActionRolls(action.description) }))
    .filter(action => action.rolls.length)
})

function useShortcut(next: RollPreset) {
  if (!current.value) return
  presets.set(current.value.id, { ...next })
  void nextTick(() => document.querySelector<HTMLElement>('[data-master-roll] [data-roll-go]')?.focus())
}
function rolled(_roll: DiceRoll, used: RollPreset) {
  if (current.value) presets.set(current.value.id, { ...used })
}
function openSheet() {
  if (current.value) void creatures.open(current.value.creatureId, { id: current.value.id, name: current.value.name })
}
</script>

<template>
  <section class="panel master-roll-panel" aria-labelledby="master-roll-title" data-master-roll>
    <div class="panel-heading">
      <div>
        <p class="eyebrow"><Icon :name="props.gameMode ? 'lucide:swords' : 'lucide:dices'" />{{ props.gameMode ? 'Modo Jogo' : 'Mestre' }}</p>
        <h2 id="master-roll-title">Rolagem</h2>
      </div>
      <button v-if="current" class="button secondary small" type="button" data-open-creature-sheet @click="openSheet">
        <Icon name="lucide:scroll-text" />Ver ficha
      </button>
    </div>

    <label class="roll-source master-roll-source">
      <span class="label-with-icon"><Icon name="lucide:user-round-search" />Ficha</span>
      <select v-model="selected" data-master-roll-source>
        <option :value="FREE">Rolagem livre</option>
        <optgroup v-if="candidates.length" label="Criaturas na mesa">
          <option v-for="candidate in candidates" :key="candidate.id" :value="candidate.id" :data-candidate="candidate.id">{{ candidate.name }}</option>
        </optgroup>
      </select>
    </label>
    <p v-if="!candidates.length" class="field-hint" data-no-creatures><Icon name="lucide:info" />Sem criaturas da biblioteca no encontro ativo.</p>
    <p v-if="notice" class="field-hint" role="status" data-master-roll-notice><Icon name="lucide:info" />{{ notice }}</p>

    <template v-if="current">
      <p v-if="loading" class="field-hint" role="status"><Icon name="lucide:loader-circle" class="spin" />Abrindo a ficha…</p>
      <p v-else-if="error" class="form-error" role="alert"><Icon name="lucide:circle-alert" />{{ error }}</p>
      <template v-else-if="creature">
        <div v-if="actions.length" class="master-roll-actions" data-master-roll-actions>
          <div v-for="action in actions" :key="action.name" class="master-roll-action" role="group" :aria-label="`Rolagens de ${action.name}`">
            <span class="master-roll-action-name">{{ action.name }}</span>
            <button
              v-for="shortcut in action.rolls"
              :key="shortcut.label"
              type="button"
              class="roll-chip shortcut"
              :data-shortcut="shortcut.kind"
              @click="useShortcut(shortcut.preset)"
            ><Icon :name="shortcut.kind === 'hit' ? 'lucide:target' : 'lucide:swords'" />{{ shortcut.label }}</button>
          </div>
        </div>
        <CreatureRollPanel :creature="creature" :preset="preset" :label="current.name" @rolled="rolled" />
      </template>
    </template>
    <template v-else>
      <QuickDiceRoller v-if="props.gameMode" class="nested-roller" @rolled-d20="roll => emit('rolled-d20', roll)" />
      <DiceRoller v-else class="nested-roller" />
    </template>
  </section>
</template>
