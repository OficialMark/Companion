<script setup lang="ts">
import { shallowRef } from 'vue'
import type { DiceRoll } from '~/types/campaign'
import { CREATURE_SIZE_LABELS, ORIGIN_LABELS, type CreatureAction, type RollPreset } from '~/types/creatures'
import { ABILITIES, DEFAULT_PRESET, abilityModifier, clampPreset, formatModifier, parseActionRolls } from '~/utils/creatureRoll'
import CreatureRollPanel from './CreatureRollPanel.vue'
import CreatureEditor from './CreatureEditor.vue'

/** Full stat block, notes, rolls and "add to combat" for the master (spec 008, US5). */
const creatures = useCreatureStore()
const campaign = useCampaignStore()
const images = useImageCacheStore()
const dialog = ref<HTMLElement | null>(null)
const closeButton = ref<HTMLButtonElement | null>(null)

// Keeps the last sheet while the modal unmounts, so closing never renders a null sheet.
const lastSheet = shallowRef(creatures.sheet!)
watch(() => creatures.sheet, value => { if (value) lastSheet.value = value })
const sheet = computed(() => creatures.sheet ?? lastSheet.value)
const portrait = computed(() => sheet.value.imageUrl ? images.urls[sheet.value.imageUrl] ?? null : null)
const initials = computed(() => sheet.value.name.split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0]!.toUpperCase()).join(''))
const subtitle = computed(() => [`${CREATURE_SIZE_LABELS[sheet.value.size]} ${sheet.value.type}`, sheet.value.alignment].filter(Boolean).join(', '))
const rollLabel = computed(() => creatures.sheetToken?.name ?? sheet.value.name)

// The token's last roll comes from the board snapshot; otherwise d20 with nothing.
const tokenPreset = computed<RollPreset | null>(() => {
  const tokenId = creatures.sheetToken?.id
  const token = tokenId ? campaign.battlemap?.tokens.find(item => item.participantId === tokenId) : null
  return token?.rollPreset ? clampPreset(token.rollPreset) : null
})
const preset = ref<RollPreset>({ ...DEFAULT_PRESET })
// Keyed by value, not by object: every board refresh rebuilds the snapshot (new objects), which
// must not wipe the master's in-progress choices; only a real preset change or another sheet does.
const presetKey = computed(() => `${sheet.value?.id}|${creatures.sheetToken?.id ?? ''}|${JSON.stringify(tokenPreset.value)}`)
watch(presetKey, () => { preset.value = tokenPreset.value ?? { ...DEFAULT_PRESET } }, { immediate: true })

const details = computed(() => [
  ['Salvaguardas', sheet.value.savingThrows], ['Perícias', sheet.value.skills],
  ['Vulnerabilidades', sheet.value.damageVulnerabilities], ['Resistências', sheet.value.damageResistances],
  ['Imunidades', sheet.value.damageImmunities], ['Imunidades a condição', sheet.value.conditionImmunities],
  ['Sentidos', sheet.value.senses], ['Idiomas', sheet.value.languages]
].filter((entry): entry is [string, string] => Boolean(entry[1])))

const sections = computed(() => [
  { key: 'traits', title: 'Traços', items: sheet.value.traits },
  { key: 'actions', title: 'Ações', items: sheet.value.actions },
  { key: 'bonus', title: 'Ações bônus', items: sheet.value.bonusActions },
  { key: 'reactions', title: 'Reações', items: sheet.value.reactions },
  { key: 'legendary', title: 'Ações lendárias', items: sheet.value.legendaryActions, intro: sheet.value.legendaryActionsIntro }
].filter(section => section.items.length))

function shortcuts(action: CreatureAction) { return parseActionRolls(action.description) }
function useShortcut(next: RollPreset) {
  preset.value = { ...next }
  nextTick(() => document.querySelector<HTMLElement>('[data-roll-go]')?.focus())
}

async function rolled(_roll: DiceRoll, used: RollPreset) {
  const tokenId = creatures.sheetToken?.id
  const encounterId = campaign.battlemap?.encounterId
  if (!tokenId || !encounterId || !campaign.session) return
  try {
    campaign.battlemap = await useCompanionApi().setRollPreset(campaign.session, encounterId, tokenId, used)
  } catch { /* remembering the roll is best effort; the roll itself already happened */ }
}

// Notes
const notes = ref('')
const notesBusy = ref(false)
const notesMessage = ref('')
watch(() => sheet.value?.notes, value => { notes.value = value ?? '' }, { immediate: true })
const notesDirty = computed(() => (notes.value.trim() || null) !== (sheet.value.notes ?? null))
async function saveNotes() {
  notesBusy.value = true; notesMessage.value = ''
  try { await creatures.saveNotes(notes.value.trim() || null); notesMessage.value = 'Anotações salvas.' }
  catch (error) { notesMessage.value = (error as Error).message }
  finally { notesBusy.value = false }
}

// Add to combat
const addCount = ref(1)
const addBusy = ref(false)
const addMessage = ref('')
const encounter = computed(() => campaign.snapshot?.activeEncounter ?? null)
const addBlocked = computed(() =>
  !encounter.value ? 'Inicie a preparação de um combate para adicionar criaturas.'
    : encounter.value.status !== 'Preparing' ? 'Criaturas entram durante a preparação do combate.'
      : campaign.connection === 'offline' ? 'Conexão necessária.' : '')
async function addToCombat() {
  addBusy.value = true; addMessage.value = ''
  try {
    await creatures.addToEncounter(addCount.value)
    addMessage.value = addCount.value === 1 ? `${sheet.value.name} entrou no combate.` : `${addCount.value} × ${sheet.value.name} entraram no combate.`
  } catch (error) { addMessage.value = (error as Error).message }
  finally { addBusy.value = false }
}
async function startPreparation() {
  if (!campaign.session) return
  addBusy.value = true; addMessage.value = ''
  try { campaign.snapshot!.activeEncounter = await useCompanionApi().createEncounter(campaign.session) }
  catch (error) { addMessage.value = (error as Error).message }
  finally { addBusy.value = false }
}

// Dialog behavior: Esc closes, focus starts on close and stays inside. Listened on the document
// so it still works after a click on plain text moved focus to <body>.
function keydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    event.preventDefault()
    if (creatures.editing && !window.confirm('Descartar as alterações da ficha?')) return
    creatures.close()
    return
  }
  if (event.key !== 'Tab' || !dialog.value) return
  const focusable = [...dialog.value.querySelectorAll<HTMLElement>('button:not([disabled]), input:not([disabled]), select, textarea, [href], [tabindex]:not([tabindex="-1"])')]
  if (!focusable.length) return
  const first = focusable[0]!; const last = focusable[focusable.length - 1]!
  const inside = dialog.value.contains(document.activeElement)
  if (event.shiftKey && (document.activeElement === first || !inside)) { event.preventDefault(); last.focus() }
  else if (!event.shiftKey && (document.activeElement === last || !inside)) { event.preventDefault(); first.focus() }
}
let previousFocus: HTMLElement | null = null
onMounted(() => {
  previousFocus = document.activeElement as HTMLElement | null
  nextTick(() => closeButton.value?.focus())
  document.body.classList.add('modal-open')
  document.addEventListener('keydown', keydown)
})
onBeforeUnmount(() => {
  document.removeEventListener('keydown', keydown)
  document.body.classList.remove('modal-open')
  previousFocus?.focus?.()
})
</script>

<template>
  <div class="modal-overlay creature-sheet-overlay" @click.self="creatures.close()">
    <div ref="dialog" class="creature-sheet" role="dialog" aria-modal="true" aria-labelledby="creature-sheet-title" tabindex="-1" data-creature-sheet>
      <header class="creature-sheet-header">
        <span class="creature-portrait large" :class="{ 'has-image': portrait }">
          <img v-if="portrait" :src="portrait" alt="">
          <template v-else>{{ initials || '?' }}</template>
        </span>
        <div class="creature-sheet-heading">
          <p class="eyebrow"><Icon name="lucide:skull" />{{ creatures.sheetToken ? `Token · ${creatures.sheetToken.name}` : 'Ficha da criatura' }}</p>
          <h2 id="creature-sheet-title">{{ sheet.name || 'Nova criatura' }}</h2>
          <p class="creature-sheet-subtitle">{{ subtitle }}<template v-if="sheet.nameEn && sheet.nameEn !== sheet.name"> · <i>{{ sheet.nameEn }}</i></template></p>
          <p class="creature-badges">
            <span class="creature-badge" :data-origin="sheet.origin">{{ ORIGIN_LABELS[sheet.origin] }}</span>
            <span class="creature-badge subtle">{{ sheet.sourceBook }}</span>
            <span class="creature-badge subtle">ND {{ sheet.challengeRating }}</span>
          </p>
        </div>
        <button ref="closeButton" class="icon-button" type="button" aria-label="Fechar ficha" title="Fechar (Esc)" @click="creatures.close()"><Icon name="lucide:x" /></button>
      </header>

      <div class="creature-sheet-body">
        <CreatureEditor v-if="creatures.editing" />
        <template v-else>
          <dl class="creature-quick-stats">
            <div><dt><Icon name="lucide:shield" />CA</dt><dd>{{ sheet.armorClass }}<small v-if="sheet.armorClassNote">{{ sheet.armorClassNote }}</small></dd></div>
            <div><dt><Icon name="lucide:heart" />PV</dt><dd>{{ sheet.hitPoints }}<small v-if="sheet.hitDice">{{ sheet.hitDice }}</small></dd></div>
            <div><dt><Icon name="lucide:footprints" />Deslocamento</dt><dd class="text">{{ sheet.speed || '—' }}</dd></div>
            <div><dt><Icon name="lucide:zap" />Iniciativa</dt><dd>{{ formatModifier(sheet.initiativeModifier) }}</dd></div>
            <div><dt><Icon name="lucide:crown" />ND</dt><dd>{{ sheet.challengeRating }}<small v-if="sheet.experiencePoints != null">{{ sheet.experiencePoints.toLocaleString('pt-BR') }} XP</small></dd></div>
          </dl>

          <div class="creature-abilities" role="list" aria-label="Atributos">
            <div v-for="ability in ABILITIES" :key="ability.key" class="creature-ability" role="listitem" :aria-label="`${ability.label} ${sheet[ability.field]} (${formatModifier(abilityModifier(sheet[ability.field]))})`">
              <span>{{ ability.label }}</span>
              <strong>{{ sheet[ability.field] }}</strong>
              <small>{{ formatModifier(abilityModifier(sheet[ability.field])) }}</small>
            </div>
          </div>

          <CreatureRollPanel :creature="sheet" :preset="preset" :label="rollLabel" @rolled="rolled" />

          <dl v-if="details.length" class="creature-details">
            <div v-for="[label, value] in details" :key="label"><dt>{{ label }}</dt><dd>{{ value }}</dd></div>
          </dl>

          <section v-for="section in sections" :key="section.key" class="creature-section" :data-section="section.key">
            <h3 class="creature-section-title">{{ section.title }}</h3>
            <p v-if="section.intro" class="creature-section-intro">{{ section.intro }}</p>
            <article v-for="(item, index) in section.items" :key="`${section.key}-${index}`" class="creature-entry">
              <p><strong>{{ item.name }}.</strong> <span class="creature-entry-text">{{ item.description }}</span></p>
              <div v-if="shortcuts(item).length" class="creature-entry-rolls" role="group" :aria-label="`Rolagens de ${item.name}`">
                <button
                  v-for="shortcut in shortcuts(item)"
                  :key="shortcut.label"
                  type="button"
                  class="roll-chip shortcut"
                  :data-shortcut="shortcut.kind"
                  @click="useShortcut(shortcut.preset)"
                ><Icon :name="shortcut.kind === 'hit' ? 'lucide:target' : 'lucide:swords'" />{{ shortcut.label }}</button>
              </div>
            </article>
          </section>

          <section class="creature-section creature-notes">
            <h3 class="creature-section-title"><Icon name="lucide:notebook-pen" />Anotações pessoais</h3>
            <textarea v-model="notes" rows="4" maxlength="4000" aria-label="Anotações pessoais" placeholder="Táticas, segredos, lembretes. Só você vê." :disabled="!sheet.id" />
            <div class="creature-notes-actions">
              <small class="field-hint">{{ notesMessage || (sheet.origin === 'Compendium' ? 'Salvar cria a versão desta mesa.' : 'Só o mestre vê.') }}</small>
              <button class="button secondary compact" type="button" data-save-notes :disabled="!notesDirty || notesBusy" @click="saveNotes"><Icon name="lucide:save" />Salvar anotações</button>
            </div>
          </section>
        </template>
      </div>

      <footer v-if="!creatures.editing" class="creature-sheet-footer">
        <div class="creature-add" role="group" aria-label="Adicionar ao combate">
          <div class="roll-stepper">
            <button class="icon-button small" type="button" aria-label="Menos uma" :disabled="addCount <= 1" @click="addCount--"><Icon name="lucide:minus" /></button>
            <output class="roll-stepper-value" data-add-count>{{ addCount }}</output>
            <button class="icon-button small" type="button" aria-label="Mais uma" :disabled="addCount >= 20" @click="addCount++"><Icon name="lucide:plus" /></button>
          </div>
          <button v-if="!encounter" class="button secondary" type="button" :disabled="addBusy || campaign.connection === 'offline'" @click="startPreparation"><Icon name="lucide:swords" />Preparar combate</button>
          <button v-else class="button primary" type="button" data-add-to-combat :disabled="Boolean(addBlocked) || addBusy" :title="addBlocked" @click="addToCombat">
            <Icon name="lucide:user-plus" />Adicionar ao combate
          </button>
        </div>
        <button class="button ghost" type="button" data-edit-creature @click="creatures.editing = true"><Icon name="lucide:pencil" />Editar ficha</button>
        <p v-if="addBlocked || addMessage" class="field-hint creature-add-message" role="status"><Icon name="lucide:info" />{{ addBlocked || addMessage }}</p>
      </footer>
    </div>
  </div>
</template>
