import { defineStore } from 'pinia'
import { onBeforeUnmount, onMounted, watch } from 'vue'
import type { Session } from '~/types/campaign'
import type { Character, CharacterSheetData } from '~/types/character'

export type SaveState = 'idle' | 'saving' | 'saved' | 'error' | 'conflict'

export const AUTOSAVE_DELAY_MS = 400
export const RELOAD_DISCARDS_EDITS = 'Recarregar descarta as mudanças que ainda não foram salvas. Continuar?'

let autosaveTimer: ReturnType<typeof setTimeout> | undefined
/**
 * Bumped by `reset()` (another account signed in on this device). A request started under an older
 * generation never touches the store: its answer belongs to the previous session (T079).
 */
let generation = 0
/**
 * Every write of this device (sheet autosave, item, equip, spell, saved roll) runs one at a time. The
 * server bumps the sheet version on each of them and answers 409 to concurrent writes of the same
 * owner, so two parallel writes from one device would look like a conflict with another device.
 * Serialized, each request starts after the previous answer and uses its version: a 409 left over
 * means a real change from somewhere else.
 */
let writeQueue: Promise<unknown> = Promise.resolve()
function enqueueWrite<T>(task: () => Promise<T>): Promise<T> {
  const result = writeQueue.then(task, task)
  writeQueue = result.catch(() => undefined)
  return result
}

function sheetData(character: Character): CharacterSheetData {
  return structuredClone(character.sheet)
}

/**
 * The player's own character (spec 011). Every number shown as "derived" comes from
 * `character.stats`, calculated by the server; the browser never applies a rule (FR-018).
 * Sheet edits go to a local draft and are saved automatically after a short pause.
 */
export const useCharacterStore = defineStore('character', {
  state: () => ({
    character: null as Character | null,
    draft: null as CharacterSheetData | null,
    loading: false,
    error: '',
    saveState: 'idle' as SaveState,
    /** Increments on every local edit of the sheet draft. */
    editSequence: 0,
    /** Edit sequence included in the save in flight (or last saved). */
    savedSequence: 0,
    inFlight: false,
    /** A CharacterChanged received while local edits are being saved; consumed after the write settles. */
    remoteChangePending: false,
    notices: [] as string[]
  }),
  getters: {
    stats: state => state.character?.stats ?? null,
    /** True while the draft has edits the server has not confirmed: derived values are stale. */
    stale: state => state.editSequence !== state.savedSequence,
    equippedWeapons: state => (state.character?.items ?? []).filter(item => item.data.category === 'Weapon' && item.data.equipped)
  },
  actions: {
    session() {
      const session = useCampaignStore().session
      if (!session) throw new Error('Sessão encerrada.')
      return session
    },
    /**
     * Guard for one request: the returned check is false once the store was reset or the session
     * changed after the request started, so a late answer of the previous account is dropped.
     */
    ticket() {
      const started = generation
      const token = useCampaignStore().session?.participantToken
      return () => started === generation && useCampaignStore().session?.participantToken === token
    },
    apply(character: Character, { resetDraft = false, preserveDraft = false } = {}) {
      this.character = character
      this.notices = character.notices ?? []
      if (!preserveDraft && (resetDraft || !this.draft || !this.stale)) this.draft = sheetData(character)
    },
    async load({ preserveLocalEdits = false } = {}) {
      const current = this.ticket()
      const sequence = this.editSequence
      const canApply = () => current() && (!preserveLocalEdits || sequence === this.editSequence)
      this.loading = true
      this.error = ''
      try {
        // In the write queue too: the first GET creates the sheet, and a reload must not overtake a
        // pending write from this device and then be overwritten by that write's older answer.
        const session = this.session()
        const character = await enqueueWrite(() => useCharacterApi().getCharacter(session))
        if (!canApply()) return
        this.editSequence = this.savedSequence
        this.apply(character, { resetDraft: true })
        this.remoteChangePending = false
        if (!preserveLocalEdits) this.saveState = 'idle'
      } catch (error) {
        if (canApply()) this.error = (error as Error).message
      } finally {
        if (current()) this.loading = false
      }
    },
    /** Another device of the same account changed the sheet: reload unless there are unsaved edits here. */
    async onRemoteChange(participantId: string) {
      if (participantId !== useCampaignStore().session?.participantId) return
      // Mid-save: remember it and reload once the write settles, instead of dropping another
      // device's change until the next one.
      if (this.stale || this.inFlight) {
        this.remoteChangePending = true
        return
      }
      await this.reconcile()
    },
    /**
     * Refresh after CharacterChanged. An echo from this device keeps the save status and
     * must not overwrite a new edit made while the refresh is in flight.
     */
    async reconcile() {
      await this.load({ preserveLocalEdits: true })
    },
    editSheet(patch: Partial<CharacterSheetData>) {
      if (!this.draft) return
      this.draft = { ...this.draft, ...patch }
      this.editSequence++
      this.saveState = 'saving'
      if (autosaveTimer) clearTimeout(autosaveTimer)
      autosaveTimer = setTimeout(() => { void this.flush() }, AUTOSAVE_DELAY_MS)
    },
    async flush() {
      if (autosaveTimer) clearTimeout(autosaveTimer)
      autosaveTimer = undefined
      if (!this.draft || !this.character || this.inFlight || !this.stale) return
      const current = this.ticket()
      // inFlight covers the wait in the queue too: a second flush meanwhile would only repeat this one.
      this.inFlight = true
      let saved = false
      try {
        saved = await enqueueWrite(async () => {
          if (!current() || !this.draft || !this.character || !this.stale) return false
          // Read at the head of the queue: edits typed while waiting go in, and the version is the one
          // of the last answer (an equip that ran just before bumped it).
          const sequence = this.editSequence
          try {
            const character = await useCharacterApi().saveSheet(this.session(), this.draft, this.character.version)
            if (!current()) return false
            this.savedSequence = sequence
            // Keep the exact local text (including a trailing space and caret position) after the
            // server confirms its normalized snapshot. An explicit reload still uses server text.
            this.apply(character, { preserveDraft: true })
            this.error = ''
            this.saveState = this.stale ? 'saving' : 'saved'
            return true
          } catch (error) {
            if (!current()) return false
            const status = (error as Error & { status?: number }).status
            this.saveState = status === 409 ? 'conflict' : 'error'
            this.error = (error as Error).message
            return false
          }
        })
      } finally {
        if (current()) this.inFlight = false
      }
      // Edits typed while the save was in flight go in a second save.
      if (saved && current() && this.stale) await this.flush()
      else if (current() && this.remoteChangePending && !this.stale && this.saveState !== 'conflict') {
        this.remoteChangePending = false
        await this.reconcile()
      }
    },
    /**
     * Item, spell and roll actions: the server answers with the whole recalculated character. Queued
     * with the sheet saves, so two writes of this device never race each other into a false 409.
     */
    async run(action: (api: ReturnType<typeof useCharacterApi>, session: Session) => Promise<Character>) {
      const current = this.ticket()
      this.error = ''
      return enqueueWrite(async () => {
        if (!current()) return false
        try {
          const character = await action(useCharacterApi(), this.session())
          if (!current()) return false
          this.apply(character)
          return true
        } catch (error) {
          if (current()) this.error = (error as Error).message
          return false
        }
      })
    },
    reset() {
      generation++
      // The previous account's writes still settle, but nothing of the next account waits for them.
      writeQueue = Promise.resolve()
      if (autosaveTimer) clearTimeout(autosaveTimer)
      autosaveTimer = undefined
      this.$reset()
    }
  }
})

/**
 * Keeps unsaved sheet edits from getting lost (T077): saves again when the connection comes back
 * and warns before the tab closes while an edit is still unsaved. The beforeunload listener exists
 * only while there is something to lose, so it never blocks the browser's back-forward cache.
 * Call it from the setup of a component that edits the sheet.
 */
export function useSheetAutosaveGuards() {
  const character = useCharacterStore()
  function onOnline() {
    if (character.stale && character.saveState !== 'conflict') void character.flush()
  }
  function onBeforeUnload(event: BeforeUnloadEvent) {
    if (!character.stale) return
    event.preventDefault()
    // Older browsers show the warning only when returnValue is set.
    event.returnValue = ''
  }
  let guarding = false
  function guard(on: boolean) {
    if (on === guarding) return
    guarding = on
    if (on) window.addEventListener('beforeunload', onBeforeUnload)
    else window.removeEventListener('beforeunload', onBeforeUnload)
  }
  let mounted = false
  const stopWatch = watch(() => character.stale, stale => { if (mounted) guard(stale) })
  onMounted(() => {
    mounted = true
    window.addEventListener('online', onOnline)
    guard(character.stale)
  })
  onBeforeUnmount(() => {
    mounted = false
    stopWatch()
    window.removeEventListener('online', onOnline)
    guard(false)
  })
}
