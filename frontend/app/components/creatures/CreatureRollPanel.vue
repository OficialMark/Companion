<script setup lang="ts">
import type { AnimatedDie } from '~/utils/diceAnimation'
import type { DiceRoll } from '~/types/campaign'
import type { Creature, RollPreset } from '~/types/creatures'
import { diceForAnimation, diceWithResults } from '~/utils/diceAnimation'
import { createResultScrollSession } from '~/utils/scrollToResult'
import { ABILITIES, MAX_BONUS, MAX_COUNT, MIN_BONUS, RESULT_HOLD_MS, ROLL_DICE, buildCreatureExpression, clampPreset, describeRoll, formatModifier, modifierFor } from '~/utils/creatureRoll'

/**
 * Roll for a creature (spec 008, US5): dice count, die, ability modifier and an extra value.
 * The server rolls (as for every roll); the label names who it was for in the history.
 */
const props = defineProps<{
  creature: Creature
  /** Starting configuration (the token's last roll, or the action shortcut). */
  preset: RollPreset
  /** Shown in the history, e.g. "Goblin 2". */
  label: string
}>()
const emit = defineEmits<{ rolled: [roll: DiceRoll, preset: RollPreset] }>()

const store = useCampaignStore()
const api = useCompanionApi()
const draft = reactive<RollPreset>(clampPreset(props.preset))
watch(() => props.preset, next => Object.assign(draft, clampPreset(next)))

const abilityMod = computed(() => modifierFor(props.creature, draft.ability))
const expression = computed(() => buildCreatureExpression(draft, abilityMod.value))
const preview = computed(() => describeRoll(draft, abilityMod.value))
const busy = ref(false)
const error = ref('')
const last = ref<DiceRoll | null>(null)
const rollingDice = ref<AnimatedDie[]>([])
const settled = ref(false)
const rollingTotals = ref<number[]>([])
const rollingResult = ref<DiceRoll | null>(null)
const offline = computed(() => store.connection === 'offline')

function step(field: 'count' | 'bonus', delta: number) {
  const min = field === 'count' ? 1 : MIN_BONUS
  const max = field === 'count' ? MAX_COUNT : MAX_BONUS
  draft[field] = Math.min(max, Math.max(min, draft[field] + delta))
}
function wait(duration: number) { return new Promise<void>(resolve => window.setTimeout(resolve, duration)) }

// The settled dice and their total stay on screen for RESULT_HOLD_MS (spec 010). The button is
// free again as soon as the result is revealed: a new roll interrupts the hold.
let holdTimer: ReturnType<typeof setTimeout> | undefined
const stage = ref<HTMLElement | null>(null)
const resultScroll = createResultScrollSession()
function clearAnimation() {
  clearTimeout(holdTimer); holdTimer = undefined
  rollingDice.value = []; settled.value = false; rollingTotals.value = []
  resultScroll.restore()
}
// The sheet can close mid-roll: the roll is still revealed (it already happened on the server and
// belongs in the history), but nothing may scroll or start a timer on a panel that is gone.
let disposed = false
onBeforeUnmount(() => { disposed = true; clearTimeout(holdTimer); resultScroll.restore() })
/** The panel sits mid-sheet: bring the dice and the result into view instead of below the fold. */
function revealStage() {
  if (disposed) return
  void nextTick(() => resultScroll.show(stage.value))
}

async function roll() {
  if (!store.session || offline.value || busy.value) return
  const preset = clampPreset({ ...draft })
  busy.value = true; error.value = ''
  clearAnimation()
  let revealedOk = false
  try {
    rollingDice.value = diceForAnimation(expression.value); settled.value = false; rollingTotals.value = []
    revealStage()
    const startedAt = performance.now()
    const result = await api.rollDice(store.session, expression.value, false, props.label)
    rollingDice.value = diceWithResults(rollingDice.value, result.results); rollingTotals.value = result.results.map(item => item.total)
    rollingResult.value = result
    await wait(Math.max(0, 600 - (performance.now() - startedAt)))
    settled.value = true
    revealStage()
    await wait(200)
    const revealed = await api.revealDiceRoll(store.session, result.id)
    if (store.snapshot && !store.snapshot.diceRolls.some(item => item.id === revealed.id)) {
      store.snapshot.diceRolls.unshift(revealed); store.snapshot.diceRolls = store.snapshot.diceRolls.slice(0, 50)
    }
    last.value = revealed
    revealedOk = true
    emit('rolled', revealed, preset)
    if (!disposed) holdTimer = setTimeout(clearAnimation, RESULT_HOLD_MS)
  } catch (exception) {
    error.value = (exception as Error).message
  } finally {
    if (!revealedOk) clearAnimation()
    busy.value = false
  }
}
</script>

<template>
  <section class="creature-roll" aria-labelledby="creature-roll-title" :data-roll-showing="rollingDice.length ? (settled ? 'result' : 'rolling') : undefined">
    <h3 id="creature-roll-title" class="creature-section-title"><Icon name="lucide:dices" />Rolar para {{ label }}</h3>

    <div class="creature-roll-row">
      <div class="roll-stepper" role="group" aria-label="Quantidade de dados">
        <button class="icon-button small" type="button" aria-label="Menos um dado" :disabled="busy || draft.count <= 1" @click="step('count', -1)"><Icon name="lucide:minus" /></button>
        <output class="roll-stepper-value" aria-live="polite">{{ draft.count }}×</output>
        <button class="icon-button small" type="button" aria-label="Mais um dado" :disabled="busy || draft.count >= MAX_COUNT" @click="step('count', 1)"><Icon name="lucide:plus" /></button>
      </div>
      <div class="roll-dice" role="radiogroup" aria-label="Dado">
        <button
          v-for="die in ROLL_DICE"
          :key="die"
          type="button"
          role="radio"
          class="roll-chip"
          :data-die="die"
          :class="{ selected: draft.die === die }"
          :aria-checked="draft.die === die"
          :disabled="busy"
          @click="draft.die = die"
        >d{{ die }}</button>
      </div>
    </div>

    <div class="roll-abilities" role="radiogroup" aria-label="Atributo modificador">
      <button type="button" role="radio" class="roll-chip" data-ability="none" :class="{ selected: !draft.ability }" :aria-checked="!draft.ability" :disabled="busy" @click="draft.ability = null">Nenhum</button>
      <button
        v-for="ability in ABILITIES"
        :key="ability.key"
        type="button"
        role="radio"
        class="roll-chip roll-ability"
        :data-ability="ability.key"
        :class="{ selected: draft.ability === ability.key }"
        :aria-checked="draft.ability === ability.key"
        :disabled="busy"
        @click="draft.ability = ability.key"
      ><span>{{ ability.label }}</span><small>{{ formatModifier(modifierFor(creature, ability.key)) }}</small></button>
    </div>

    <div class="creature-roll-row">
      <div class="roll-stepper" role="group" aria-label="Valor adicional">
        <span class="roll-stepper-label">Adicional</span>
        <button class="icon-button small" type="button" aria-label="Diminuir adicional" :disabled="busy || draft.bonus <= MIN_BONUS" @click="step('bonus', -1)"><Icon name="lucide:minus" /></button>
        <output class="roll-stepper-value" data-roll-bonus aria-live="polite">{{ formatModifier(draft.bonus) }}</output>
        <button class="icon-button small" type="button" aria-label="Aumentar adicional" :disabled="busy || draft.bonus >= MAX_BONUS" @click="step('bonus', 1)"><Icon name="lucide:plus" /></button>
      </div>
      <button class="button primary roll-go" type="button" data-roll-go :disabled="busy || offline" @click="roll">
        <Icon :name="busy ? 'lucide:loader-circle' : 'lucide:dices'" :class="{ spin: busy }" />
        <span>Rolar <code data-roll-preview>{{ preview }}</code></span>
      </button>
    </div>

    <div v-if="rollingDice.length" ref="stage" class="roll-stage" data-roll-stage>
      <DiceRollAnimation :dice="rollingDice" :settled="settled" :totals="rollingTotals" :roll="rollingResult" />
    </div>
    <p v-else-if="last" class="roll-last" aria-live="polite">
      <Icon name="lucide:sparkles" />Último resultado: <strong>{{ last.total }}</strong> <small>({{ last.expression }})</small>
    </p>
    <p v-if="offline" class="field-hint"><Icon name="lucide:wifi-off" />Conexão necessária para rolar.</p>
    <p v-if="error" class="form-error" role="alert"><Icon name="lucide:circle-alert" />{{ error }}</p>
  </section>
</template>
