import { defineStore } from 'pinia'
import type { Session } from '~/types/campaign'
import type { Character, CharacterSheetData } from '~/types/character'

export type SaveState = 'idle' | 'saving' | 'saved' | 'error' | 'conflict'

export const AUTOSAVE_DELAY_MS = 400

let autosaveTimer: ReturnType<typeof setTimeout> | undefined

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
    apply(character: Character, { resetDraft = false } = {}) {
      this.character = character
      this.notices = character.notices ?? []
      if (resetDraft || !this.draft || !this.stale) this.draft = sheetData(character)
    },
    async load() {
      this.loading = true
      this.error = ''
      try {
        const character = await useCharacterApi().getCharacter(this.session())
        this.editSequence = this.savedSequence
        this.apply(character, { resetDraft: true })
        this.saveState = 'idle'
      } catch (error) {
        this.error = (error as Error).message
      } finally {
        this.loading = false
      }
    },
    /** Another device of the same account changed the sheet: reload unless there are unsaved edits here. */
    async onRemoteChange(participantId: string) {
      if (participantId !== useCampaignStore().session?.participantId) return
      if (this.stale || this.inFlight) return
      await this.load()
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
      const sequence = this.editSequence
      this.inFlight = true
      try {
        const character = await useCharacterApi().saveSheet(this.session(), this.draft, this.character.version)
        this.savedSequence = sequence
        this.apply(character)
        this.saveState = this.stale ? 'saving' : 'saved'
      } catch (error) {
        const status = (error as Error & { status?: number }).status
        this.saveState = status === 409 ? 'conflict' : 'error'
        this.error = (error as Error).message
        return
      } finally {
        this.inFlight = false
      }
      // Edits typed while the save was in flight go in a second save.
      if (this.stale) await this.flush()
    },
    /** Item, spell and roll actions: the server answers with the whole recalculated character. */
    async run(action: (api: ReturnType<typeof useCharacterApi>, session: Session) => Promise<Character>) {
      this.error = ''
      try {
        const character = await action(useCharacterApi(), this.session())
        this.apply(character)
        return true
      } catch (error) {
        this.error = (error as Error).message
        return false
      }
    },
    reset() {
      if (autosaveTimer) clearTimeout(autosaveTimer)
      autosaveTimer = undefined
      this.$reset()
    }
  }
})
