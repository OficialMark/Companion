<script setup lang="ts">
import type { AnimatedDie } from '~/utils/diceAnimation'
import type { DiceRoll } from '~/types/campaign'
import type { SavedRoll } from '~/types/character'
import DiceRollAnimation from '~/components/DiceRollAnimation.vue'
import RollResultDetails from '~/components/RollResultDetails.vue'
import RollConfigFields from '~/components/character/RollConfigFields.vue'
import SavedRollEditor from '~/components/character/SavedRollEditor.vue'
import { RESULT_HOLD_MS } from '~/utils/creatureRoll'
import { createResultScrollSession } from '~/utils/scrollToResult'
import { MAX_EXPRESSION_LENGTH, NO_DICE_ERROR } from '~/utils/rollExpression'
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
const manualExpression = ref<string | null>(null)
const stats = computed(() => character.character?.stats ?? null)
const resolved = computed(() => resolveRoll(config.value, stats.value, character.character))
const savedRolls = computed(() => [...(character.character?.savedRolls ?? [])].sort((a, b) => a.sortOrder - b.sortOrder))
const offline = computed(() => store.connection === 'offline')
const canRoll = computed(() => !busy.value && !offline.value && (
  manualExpression.value !== null
    ? Boolean(manualExpression.value.trim())
    : Boolean(resolved.value.expression) && !resolved.value.error
))
/** Why "Rolar" is off when nothing else explains it (no dice, no expression): a disabled main action must say what it waits for. */
const rollHint = computed(() => {
  const error = resolved.value.error
  if (canRoll.value || busy.value || offline.value || (error && error !== NO_DICE_ERROR)) return ''
  return 'Escolha os dados ou digite uma expressão para rolar.'
})

const busy = ref(false)
const error = ref('')
/** Result rolled in this page session (shown right after the animation). */
const sessionLast = ref<DiceRoll | null>(null)
/**
 * The player's own latest public roll, from the table history: after a reload or on another device the
 * result still sits under "Rolar" instead of only far away in the history (design review, round 3).
 */
const latestOwn = computed(() => {
  const me = store.session?.participantId
  let newest: DiceRoll | null = null
  for (const roll of store.snapshot?.diceRolls ?? []) {
    if (roll.participantId !== me) continue
    if (!newest || roll.createdAt > newest.createdAt) newest = roll
  }
  return newest
})
const last = computed(() => sessionLast.value ?? latestOwn.value)
const rollingDice = ref<AnimatedDie[]>([])
const settled = ref(false)
const rollingResult = ref<DiceRoll | null>(null)
const rollingTotals = ref<number[]>([])
const editorOpen = ref(false)
const editorInitial = ref<RollConfig | null>(null)

/** Spec 012, FR-007: the dice, then the result, are brought into view. */
const stage = ref<HTMLElement | null>(null)
const resultScroll = createResultScrollSession()
function revealStage() { if (!disposed) void nextTick(() => resultScroll.show(stage.value)) }
function wait(duration: number) { return new Promise<void>(resolve => window.setTimeout(resolve, duration)) }

let holdTimer: ReturnType<typeof setTimeout> | undefined
let disposed = false
onBeforeUnmount(() => { disposed = true; clearTimeout(holdTimer); resultScroll.restore() })
function clearAnimation() {
  clearTimeout(holdTimer); holdTimer = undefined
  rollingDice.value = []; settled.value = false; rollingTotals.value = []
  resultScroll.restore()
}

/** Server values go to the animated dice in expression order, both executions of maior/menor included. */
function withValues(dice: AnimatedDie[], roll: DiceRoll): AnimatedDie[] {
  const queues = new Map<number, number[]>()
  for (const { faces, value } of plannedValues(roll.results)) queues.set(faces, [...(queues.get(faces) ?? []), value])
  return dice.map(die => ({ ...die, value: queues.get(die.faces)?.shift() }))
}

function updateConfig(next: RollConfig) {
  config.value = next
  manualExpression.value = null
  error.value = ''
}

function editExpression(event: Event) {
  manualExpression.value = (event.target as HTMLInputElement).value
  error.value = ''
}

function rollCurrent() {
  if (!canRoll.value) return
  return manualExpression.value === null
    ? roll(config.value)
    : roll(null, undefined, manualExpression.value.trim())
}

async function roll(source: RollConfig | null, label?: string, manual = '') {
  if (!store.session || offline.value || busy.value) return
  const snapshot = source && cloneConfig(source)
  const built = snapshot && resolveRoll(snapshot, stats.value, character.character)
  const expression = built?.expression ?? manual
  if (built?.error || !expression) { error.value = built?.error ?? ''; return }
  busy.value = true; error.value = ''
  clearAnimation()
  let revealedOk = false
  try {
    let startedAt = performance.now()
    if (built && snapshot) {
      rollingDice.value = plannedDice(built.dice, built.selection, snapshot.repetitions)
      revealStage()
    }
    const damagePlan = snapshot?.kind === 'Damage' ? weaponDamageBreakdownPlan(snapshot, stats.value) : null
    const rollLabel = label ?? built?.label ?? null
    const result = damagePlan
      ? await api.rollDice(store.session, expression, false, rollLabel, damagePlan)
      : await api.rollDice(store.session, expression, false, rollLabel)
    if (built) {
      rollingDice.value = withValues(rollingDice.value, result)
    } else {
      rollingDice.value = plannedValues(result.results).map((die, index) => ({ ...die, id: `manual-${index}` }))
      startedAt = performance.now()
      revealStage()
    }
    rollingTotals.value = result.results.map(item => item.total)
    rollingResult.value = result
    await wait(Math.max(0, 600 - (performance.now() - startedAt)))
    settled.value = true
    revealStage()
    await wait(200)
    const revealed = await api.revealDiceRoll(store.session, result.id)
    if (store.snapshot && !store.snapshot.diceRolls.some(item => item.id === revealed.id)) {
      store.snapshot.diceRolls.unshift(revealed); store.snapshot.diceRolls = store.snapshot.diceRolls.slice(0, 50)
    }
    sessionLast.value = revealed
    revealedOk = true
    emit('rolled', revealed)
    if (snapshot?.kind === 'Attack' && snapshot.source?.type === 'weapon') {
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

/** A shortcut rolls from the current sheet without changing the panel selections. */
async function useSavedRoll(saved: SavedRoll) {
  if (saved.incomplete) return
  manualExpression.value = null
  await roll(configFromSavedRoll(saved.data), saved.data.name)
}

function openEditor() {
  editorInitial.value = cloneConfig(config.value)
  editorOpen.value = true
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

    <RollConfigFields :model-value="config" :stats="stats" :character="character.character" :disabled="busy" preselect-single-weapon @update:model-value="updateConfig">
      <template #expression="{ visibleError }">
        <label class="roll-expression">
          <span class="roll-expression-label">Expressão</span>
          <input
            class="roll-expression-input"
            type="text"
            data-expression
            :value="manualExpression ?? resolved.expression"
            :maxlength="MAX_EXPRESSION_LENGTH"
            :disabled="busy"
            spellcheck="false"
            autocomplete="off"
            placeholder="Ex.: 2d6+3"
            @input="editExpression"
            @keydown.enter.prevent="rollCurrent"
          >
          <span v-if="manualExpression !== null" class="roll-expression-empty">Expressão manual. Altere uma opção acima para voltar à expressão do painel.</span>
        </label>
        <p v-if="manualExpression === null && visibleError" class="form-error" data-roll-error role="alert"><Icon name="lucide:circle-alert" />{{ visibleError }}</p>
      </template>
    </RollConfigFields>

    <div class="player-roll-actions">
      <button class="button primary roll-go" type="button" data-roll-go :disabled="!canRoll" :aria-describedby="rollHint ? 'roll-go-hint' : undefined" @click="rollCurrent">
        <Icon :name="busy ? 'lucide:loader-circle' : 'lucide:dices'" :class="{ spin: busy }" />{{ busy ? 'Rolando…' : 'Rolar' }}
      </button>
      <button class="button ghost small" type="button" data-save-roll :disabled="busy || manualExpression !== null" @click="openEditor">
        <Icon name="lucide:bookmark-plus" />Salvar como rolagem
      </button>
      <p v-if="rollHint" id="roll-go-hint" class="field-hint roll-go-hint" data-roll-hint><Icon name="lucide:info" />{{ rollHint }}</p>
      <!-- Wide screens: this row is stuck at the foot of the dice column and is the one place the result
           shows (the card below is hidden there: two copies read as a glitch, review round 7). On phones
           it is display:none, so only the card is read out. -->
      <p v-if="last && !rollingDice.length" class="roll-last-chip" data-roll-last-chip aria-live="polite">
        <Icon name="lucide:sparkles" /><span class="roll-last-chip-label">{{ last.label || 'Último resultado' }}</span><strong>{{ last.total }}</strong>
      </p>
    </div>

    <div v-if="rollingDice.length" ref="stage" class="roll-stage" data-roll-stage>
      <DiceRollAnimation :dice="rollingDice" :settled="settled" :totals="rollingTotals" :roll="rollingResult" />
    </div>
    <div v-else-if="last" class="player-roll-result" aria-live="polite" data-roll-last>
      <p class="roll-last"><Icon name="lucide:sparkles" />{{ last.label || 'Último resultado' }} <strong>{{ last.total }}</strong></p>
      <RollResultDetails :roll="last" total-in-heading />
    </div>
    <p v-if="offline" class="field-hint"><Icon name="lucide:wifi-off" />Conexão necessária para rolar.</p>
    <p v-if="error" class="form-error" role="alert" data-roll-send-error><Icon name="lucide:circle-alert" />{{ error }}</p>

    <SavedRollEditor v-if="editorOpen" :initial="editorInitial" @close="editorOpen = false" @saved="editorOpen = false" />
  </section>
</template>
