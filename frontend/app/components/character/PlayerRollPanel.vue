<script setup lang="ts">
import type { AnimatedDie } from '~/utils/diceAnimation'
import type { DiceRoll } from '~/types/campaign'
import type { SavedRoll, SavedRollData } from '~/types/character'
import DiceRollAnimation from '~/components/DiceRollAnimation.vue'
import RollConfigFields from '~/components/character/RollConfigFields.vue'
import SavedRollEditor from '~/components/character/SavedRollEditor.vue'
import { RESULT_HOLD_MS } from '~/utils/creatureRoll'
import { scrollToResult } from '~/utils/scrollToResult'
import {
  cloneConfig, configFromSavedRoll, emptyConfig, plannedDice, plannedValues, resolveRoll, weaponDamageExpression, weaponDamageSummary, weaponDamageBreakdownPlan, type DamageBreakdownPlan, type RollConfig
} from '~/utils/playerRoll'

/**
 * Roll panel of the player in the Modo Jogo (spec 011, US4 to US6). Builds the roll from the player's
 * own character (numbers calculated by the server), shows the canonical expression live and sends it
 * through the existing dice route with a descriptive label. Rolls are revealed after the animation,
 * like the creature roll panel.
 */
const props = withDefaults(defineProps<{ gameMode?: boolean }>(), { gameMode: true })
const emit = defineEmits<{
  'open-inventory': []
  /** An attack with a weapon was rolled: the page decides whether to ask "Acertou?". */
  'attack-rolled': [roll: DiceRoll, damageExpression: string | null, damageSummary: string | null, damagePlan: DamageBreakdownPlan | null]
  rolled: [roll: DiceRoll]
}>()

const store = useCampaignStore()
const api = useCompanionApi()
const character = useCharacterStore()

const config = ref<RollConfig>(emptyConfig())
const stats = computed(() => character.character?.stats ?? null)
const resolved = computed(() => resolveRoll(config.value, stats.value, character.character))
const savedRolls = computed(() => [...(character.character?.savedRolls ?? [])].sort((a, b) => a.sortOrder - b.sortOrder))
const offline = computed(() => store.connection === 'offline')
const canRoll = computed(() => !busy.value && !offline.value && Boolean(resolved.value.expression) && !resolved.value.error)

const busy = ref(false)
const error = ref('')
const last = ref<DiceRoll | null>(null)
const rollingDice = ref<AnimatedDie[]>([])
const settled = ref(false)
const rollingTotals = ref<number[]>([])
const editorOpen = ref(false)
const editorInitial = ref<RollConfig | null>(null)

/** Spec 012, FR-007: the dice, then the result, are brought into view. */
const stage = ref<HTMLElement | null>(null)
function revealStage() { if (!disposed) void nextTick(() => scrollToResult(stage.value)) }
function wait(duration: number) { return new Promise<void>(resolve => window.setTimeout(resolve, duration)) }

let holdTimer: ReturnType<typeof setTimeout> | undefined
let disposed = false
onBeforeUnmount(() => { disposed = true; clearTimeout(holdTimer) })
function clearAnimation() {
  clearTimeout(holdTimer); holdTimer = undefined
  rollingDice.value = []; settled.value = false; rollingTotals.value = []
}

/** Server values go to the animated dice in expression order, both executions of maior/menor included. */
function withValues(dice: AnimatedDie[], roll: DiceRoll): AnimatedDie[] {
  const queues = new Map<number, number[]>()
  for (const { faces, value } of plannedValues(roll.results)) queues.set(faces, [...(queues.get(faces) ?? []), value])
  return dice.map(die => ({ ...die, value: queues.get(die.faces)?.shift() }))
}

async function roll(source: RollConfig = config.value, label?: string) {
  if (!store.session || offline.value || busy.value) return
  const snapshot = cloneConfig(source)
  const built = resolveRoll(snapshot, stats.value, character.character)
  if (built.error || !built.expression) { error.value = built.error ?? ''; return }
  busy.value = true; error.value = ''
  clearAnimation()
  let revealedOk = false
  try {
    rollingDice.value = plannedDice(built.dice, built.selection, snapshot.repetitions); settled.value = false; revealStage()
    const startedAt = performance.now()
    const damagePlan = snapshot.kind === 'Damage' ? weaponDamageBreakdownPlan(snapshot, stats.value) : null
    const result = damagePlan
      ? await api.rollDice(store.session, built.expression, false, label ?? built.label, damagePlan)
      : await api.rollDice(store.session, built.expression, false, label ?? built.label)
    rollingDice.value = withValues(rollingDice.value, result); rollingTotals.value = result.results.map(item => item.total)
    await wait(Math.max(0, 600 - (performance.now() - startedAt))); settled.value = true; await wait(200)
    const revealed = await api.revealDiceRoll(store.session, result.id)
    if (store.snapshot && !store.snapshot.diceRolls.some(item => item.id === revealed.id)) {
      store.snapshot.diceRolls.unshift(revealed); store.snapshot.diceRolls = store.snapshot.diceRolls.slice(0, 50)
    }
    last.value = revealed
    revealedOk = true
    emit('rolled', revealed)
    if (snapshot.kind === 'Attack' && snapshot.source?.type === 'weapon') {
      emit('attack-rolled', revealed, weaponDamageExpression(snapshot, stats.value), weaponDamageSummary(snapshot, stats.value), weaponDamageBreakdownPlan(snapshot, stats.value))
    }
    if (!disposed) holdTimer = setTimeout(clearAnimation, RESULT_HOLD_MS)
  } catch (exception) {
    error.value = (exception as Error).message
  } finally {
    if (!revealedOk) clearAnimation()
    busy.value = false
  }
}

/** A saved roll is rebuilt from the current sheet (FR-034), loaded into the panel and rolled with its name. */
async function useSavedRoll(saved: SavedRoll) {
  if (saved.incomplete) return
  config.value = configFromSavedRoll(saved.data)
  await roll(config.value, saved.data.name)
}

function openEditor() {
  editorInitial.value = cloneConfig(config.value)
  editorOpen.value = true
}

function applySavedRoll(data: SavedRollData) {
  config.value = configFromSavedRoll(data)
  editorOpen.value = false
}
</script>

<template>
  <section class="panel player-roll-panel" aria-labelledby="player-roll-title" data-player-roll-panel :data-roll-showing="rollingDice.length ? (settled ? 'result' : 'rolling') : undefined">
    <div class="panel-heading">
      <div><p class="eyebrow"><Icon :name="props.gameMode ? 'lucide:swords' : 'lucide:scroll-text'" />{{ props.gameMode ? 'Modo Jogo' : 'Sua ficha' }}</p><h2 id="player-roll-title">Rolagem</h2></div>
      <button class="button secondary small" type="button" data-open-inventory @click="emit('open-inventory')">
        <Icon name="lucide:backpack" />Mochila
      </button>
    </div>

    <div v-if="savedRolls.length" class="saved-roll-bar" role="group" aria-label="Rolagens salvas">
      <button
        v-for="saved in savedRolls"
        :key="saved.id"
        type="button"
        class="saved-roll-chip"
        :data-saved-roll="saved.id"
        :data-incomplete="saved.incomplete ? '' : undefined"
        :disabled="saved.incomplete || busy || offline"
        :title="saved.incomplete ? 'Arma ou magia ausente: edite a rolagem' : `Rolar ${saved.data.name}`"
        :aria-label="saved.incomplete ? `${saved.data.name}. Arma ou magia ausente: edite a rolagem` : `Rolar ${saved.data.name}`"
        @click="useSavedRoll(saved)"
      >
        <Icon :name="saved.data.icon" /><span>{{ saved.data.name }}</span>
        <Icon v-if="saved.incomplete" name="lucide:triangle-alert" class="saved-roll-warning" />
      </button>
    </div>

    <RollConfigFields v-model="config" :stats="stats" :character="character.character" :disabled="busy" preselect-single-weapon />

    <div class="player-roll-actions">
      <button class="button primary roll-go" type="button" data-roll-go :disabled="!canRoll" @click="roll()">
        <Icon :name="busy ? 'lucide:loader-circle' : 'lucide:dices'" :class="{ spin: busy }" />{{ busy ? 'Rolando…' : 'Rolar' }}
      </button>
      <button class="button ghost small" type="button" data-save-roll :disabled="busy" @click="openEditor">
        <Icon name="lucide:bookmark-plus" />Salvar como rolagem
      </button>
    </div>

    <div v-if="rollingDice.length" ref="stage" class="roll-stage" data-roll-stage>
      <DiceRollAnimation :dice="rollingDice" :settled="settled" :totals="rollingTotals" />
    </div>
    <p v-else-if="last" class="roll-last" aria-live="polite" data-roll-last>
      <Icon name="lucide:sparkles" />{{ last.label || 'Último resultado' }}: <strong>{{ last.total }}</strong> <small>({{ last.expression }})</small>
    </p>
    <p v-if="offline" class="field-hint"><Icon name="lucide:wifi-off" />Conexão necessária para rolar.</p>
    <p v-if="error" class="form-error" role="alert" data-roll-send-error><Icon name="lucide:circle-alert" />{{ error }}</p>

    <SavedRollEditor v-if="editorOpen" :initial="editorInitial" @close="editorOpen = false" @saved="applySavedRoll" />
  </section>
</template>
