<script setup lang="ts">
import type { AbilityKey } from '~/types/character'
import { RELOAD_DISCARDS_EDITS } from '~/stores/character'
import { trapTab } from '~/utils/focus'
import { applyDamage, applyHealing, hitPointAmount } from '~/utils/hitPoints'

/**
 * Overview of the player's sheet at the Mesa (spec 012, US3): the numbers that matter in play and a
 * quick way to take damage or heal. Hit points go through the sheet's own write queue (editSheet +
 * flush), which also updates the table's HP bars (spec 011, FR-010).
 */
const emit = defineEmits<{ close: [] }>()
const store = useCampaignStore()
const character = useCharacterStore()
const { go } = useAppView()

const ORDER: AbilityKey[] = ['str', 'dex', 'con', 'int', 'wis', 'cha']
const sheet = computed(() => character.draft)
const stats = computed(() => character.stats)
const offline = computed(() => store.connection === 'offline')
/** Offline or in conflict (409): a change could not be saved, so Dano and Cura wait. */
const blocked = computed(() => offline.value || character.saveState === 'conflict')
const ready = computed(() => Boolean(sheet.value && stats.value))
const maxHp = computed(() => stats.value?.maxHitPoints.value ?? 0)
const amount = ref<number | null>(null)
const amountInput = ref<HTMLInputElement | null>(null)
const dialog = ref<HTMLElement | null>(null)
const heading = ref<HTMLElement | null>(null)
const announce = ref('')

function signed(value: number) { return value >= 0 ? `+${value}` : `${value}` }
const subtitle = computed(() => {
  const data = sheet.value
  if (!data) return ''
  return [data.species, data.className && `${data.className}${data.subclass ? ` (${data.subclass})` : ''}`, `nível ${data.level}`]
    .filter(Boolean).join(' · ')
})
const speed = computed(() => {
  const walk = stats.value?.speed.walk ?? Object.values(stats.value?.speed ?? {})[0]
  return walk ? `${String(walk.value).replace('.', ',')} m` : '—'
})

function change(kind: 'damage' | 'heal', raw: number | null = amount.value) {
  const data = sheet.value
  const value = hitPointAmount(raw)
  if (!data || !value || blocked.value) return
  const state = { current: data.currentHitPoints, temporary: data.temporaryHitPoints }
  const next = kind === 'damage' ? applyDamage(state, value) : applyHealing(state, value, maxHp.value)
  character.editSheet({ currentHitPoints: next.current, temporaryHitPoints: next.temporary })
  void character.flush()
  announce.value = kind === 'damage' ? `${value} de dano. Vida ${next.current} de ${maxHp.value}.` : `${value} de cura. Vida ${next.current} de ${maxHp.value}.`
  amount.value = null
  // Dano and Cura turn disabled with the field empty: keep the focus in the dialog (Esc, Tab trap).
  void nextTick(() => amountInput.value?.focus())
}

const SAVE_LABEL: Record<string, string> = { saving: 'Salvando…', saved: 'Salvo', error: 'Não foi possível salvar', conflict: 'A ficha mudou em outro aparelho' }
function reloadSheet() {
  if (character.stale && !window.confirm(RELOAD_DISCARDS_EDITS)) return
  void character.load()
}
function openSheet() { emit('close'); go('ficha') }

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') { event.stopPropagation(); emit('close'); return }
  if (dialog.value) trapTab(event, dialog.value)
}
onMounted(() => { void nextTick(() => heading.value?.focus()) })
</script>

<template>
  <div class="modal-overlay" role="dialog" aria-modal="true" aria-labelledby="sheet-overview-title" data-sheet-overview @click.self="emit('close')" @keydown="onKeydown">
    <div ref="dialog" class="modal-panel sheet-overview" tabindex="-1">
      <header class="sheet-overview-head">
        <div>
          <p class="eyebrow"><Icon name="lucide:scroll-text" />Sua ficha</p>
          <h2 id="sheet-overview-title" ref="heading" tabindex="-1">{{ sheet?.characterName || store.session?.displayName }}</h2>
          <p v-if="subtitle" class="sheet-overview-subtitle">{{ subtitle }}</p>
        </div>
        <button class="icon-button" type="button" aria-label="Fechar resumo da ficha" title="Fechar" data-close-overview @click="emit('close')"><Icon name="lucide:x" /></button>
      </header>

      <p v-if="!ready" class="catalog-state" role="status"><Icon name="lucide:loader-circle" class="spin" />Abrindo a ficha…</p>
      <template v-else-if="sheet && stats">
        <section class="sheet-overview-hp" aria-labelledby="overview-hp-title">
          <h3 id="overview-hp-title" class="sr-only">Pontos de vida</h3>
          <div class="sheet-overview-hp-value">
            <Icon name="lucide:heart" />
            <strong data-overview-hp>{{ sheet.currentHitPoints }}</strong><span>/ {{ maxHp }}</span>
            <span v-if="sheet.temporaryHitPoints" class="item-badge subtle" data-overview-temp-hp>+{{ sheet.temporaryHitPoints }} temp.</span>
          </div>
          <div class="hp-meter" role="meter" :aria-valuenow="sheet.currentHitPoints" aria-valuemin="0" :aria-valuemax="maxHp" aria-label="Vida atual">
            <span :style="{ width: `${maxHp ? Math.min(100, (sheet.currentHitPoints / maxHp) * 100) : 0}%` }" />
          </div>
          <form class="sheet-overview-hp-form" @submit.prevent="change('damage')">
            <button class="icon-button" type="button" aria-label="Menos 1 de vida" title="−1" :disabled="blocked || sheet.currentHitPoints + sheet.temporaryHitPoints <= 0" data-hp-minus @click="change('damage', 1)"><Icon name="lucide:minus" /></button>
            <label class="sr-only" for="overview-hp-amount">Quantidade</label>
            <input id="overview-hp-amount" ref="amountInput" v-model.number="amount" type="number" inputmode="numeric" min="1" max="9999" step="1" placeholder="Valor" data-hp-amount>
            <button class="icon-button" type="button" aria-label="Mais 1 de vida" title="+1" :disabled="blocked || sheet.currentHitPoints >= maxHp" data-hp-plus @click="change('heal', 1)"><Icon name="lucide:plus" /></button>
            <button class="button danger small" type="submit" :disabled="blocked || !hitPointAmount(amount)" data-hp-damage><Icon name="lucide:sword" />Dano</button>
            <button class="button secondary small" type="button" :disabled="blocked || !hitPointAmount(amount)" data-hp-heal @click="change('heal')"><Icon name="lucide:heart-plus" />Cura</button>
          </form>
          <p class="sr-only" aria-live="polite">{{ announce }}</p>
          <p v-if="offline" class="field-hint"><Icon name="lucide:wifi-off" />Conexão necessária para mudar a vida.</p>
          <p v-if="SAVE_LABEL[character.saveState]" class="field-hint" role="status" :data-sheet-save-state="character.saveState">
            {{ SAVE_LABEL[character.saveState] }}
            <button v-if="character.saveState === 'error'" type="button" class="button ghost small" data-retry-save @click="character.flush()">Tentar de novo</button>
            <button v-else-if="character.saveState === 'conflict'" type="button" class="button ghost small" data-reload-sheet @click="reloadSheet">Recarregar ficha</button>
          </p>
        </section>

        <dl class="sheet-overview-stats" data-overview-stats>
          <div><dt><Icon name="lucide:shield" />CA</dt><dd data-overview-ac>{{ stats.armorClass.value }}</dd></div>
          <div><dt><Icon name="lucide:zap" />Iniciativa</dt><dd data-overview-initiative>{{ signed(stats.initiative.value) }}</dd></div>
          <div><dt><Icon name="lucide:footprints" />Deslocamento</dt><dd>{{ speed }}</dd></div>
          <div><dt><Icon name="lucide:award" />Proficiência</dt><dd>{{ signed(stats.proficiencyBonus) }}</dd></div>
          <div><dt><Icon name="lucide:eye" />Percepção passiva</dt><dd>{{ stats.passivePerception.value }}</dd></div>
          <div v-if="stats.spellSaveDc"><dt><Icon name="lucide:wand-sparkles" />CD de magia</dt><dd>{{ stats.spellSaveDc.value }}</dd></div>
        </dl>

        <table class="sheet-overview-abilities" data-overview-abilities>
          <caption class="sr-only">Atributos e salvaguardas</caption>
          <thead><tr><th scope="col">Atributo</th><th scope="col">Valor</th><th scope="col">Mod.</th><th scope="col">Salvaguarda</th></tr></thead>
          <tbody>
            <tr v-for="key in ORDER" :key="key" :data-ability="key">
              <th scope="row">{{ stats.abilities[key].abbreviation }}</th>
              <td>{{ stats.abilities[key].final }}</td>
              <td>{{ signed(stats.abilities[key].modifier) }}</td>
              <td :class="{ proficient: stats.savingThrows[key].training !== 'None' }">{{ signed(stats.savingThrows[key].bonus) }}</td>
            </tr>
          </tbody>
        </table>

        <p v-if="stats.conditions.length" class="sheet-overview-conditions"><Icon name="lucide:activity" />{{ stats.conditions.join(', ') }}</p>

        <div class="sheet-modal-actions">
          <button class="button primary" type="button" data-open-sheet @click="openSheet"><Icon name="lucide:scroll-text" />Abrir ficha completa</button>
        </div>
      </template>
    </div>
  </div>
</template>
