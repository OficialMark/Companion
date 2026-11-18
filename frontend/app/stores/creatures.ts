import { defineStore } from 'pinia'
import type { Creature, CreatureFields, CreatureQuery, CreatureSummary, SaveCreatureRequest } from '~/types/creatures'

export const PAGE_SIZE = 24

/** Master's creature library (spec 008). */
export const useCreatureStore = defineStore('creatures', {
  state: () => ({
    query: { q: '', size: '', type: '', crMin: null, crMax: null, origin: '', page: 1, pageSize: PAGE_SIZE } as Required<CreatureQuery>,
    items: [] as CreatureSummary[],
    total: 0,
    types: [] as string[],
    loading: false,
    error: '',
    /** Sheet currently open in the modal (null = closed). */
    sheet: null as Creature | null,
    /** When the sheet was opened from a board token: its id and display name. */
    sheetToken: null as { id: string; name: string } | null,
    sheetLoading: false,
    editing: false,
    requestSequence: 0
  }),
  getters: {
    pageCount: state => Math.max(1, Math.ceil(state.total / state.query.pageSize))
  },
  actions: {
    session() {
      const session = useCampaignStore().session
      if (!session) throw new Error('Sessão encerrada.')
      return session
    },
    async load() {
      const sequence = ++this.requestSequence
      this.loading = true
      this.error = ''
      try {
        const page = await useCompanionApi().listCreatures(this.session(), this.query)
        // Typing fast fires several searches; only the latest may win.
        if (sequence !== this.requestSequence) return
        this.items = page.items
        this.total = page.total
        void Promise.all(page.items.map(item => useImageCacheStore().ensure(item.imageUrl)))
      } catch (error) {
        if (sequence === this.requestSequence) this.error = (error as Error).message
      } finally {
        if (sequence === this.requestSequence) this.loading = false
      }
    },
    async loadTypes() {
      if (this.types.length) return
      try { this.types = await useCompanionApi().creatureTypes(this.session()) } catch { /* filter stays free text */ }
    },
    async setQuery(patch: Partial<CreatureQuery>) {
      this.query = { ...this.query, ...patch, page: patch.page ?? 1 } as Required<CreatureQuery>
      await this.load()
    },
    async open(id: string, token: { id: string; name: string } | null = null) {
      this.sheetLoading = true
      this.sheetToken = token
      this.editing = false
      this.error = ''
      try {
        this.sheet = await useCompanionApi().getCreature(this.session(), id)
        void useImageCacheStore().ensure(this.sheet.imageUrl)
      } catch (error) {
        this.error = (error as Error).message
        this.sheet = null
      } finally {
        this.sheetLoading = false
      }
    },
    newCreature() {
      this.sheet = { ...blankCreature(), id: '', baseCreatureId: null, origin: 'Custom', sourceBook: 'Mesa', sourceUrl: null, imageUrl: null, hasImage: false, footprintSize: 1, initiativeModifier: 0, revision: '' }
      this.sheetToken = null
      this.editing = true
    },
    close() {
      this.sheet = null
      this.sheetToken = null
      this.editing = false
    },
    /** Returns the saved sheet; refreshes the list so origin badges stay right. */
    async save(fields: CreatureFields) {
      const current = this.sheet
      const body: SaveCreatureRequest = { ...fields, expectedRevision: current?.revision || null }
      const api = useCompanionApi()
      const saved = current?.id ? await api.saveCreature(this.session(), current.id, body) : await api.createCreature(this.session(), body)
      this.sheet = saved
      this.editing = false
      void this.load()
      return saved
    },
    async saveNotes(notes: string | null) {
      if (!this.sheet?.id) return
      this.sheet = await useCompanionApi().saveCreatureNotes(this.session(), this.sheet.id, notes, this.sheet.revision || null)
      void this.load()
    },
    /** Custom: deleted and the modal closes. Edited: back to the compendium original. */
    async remove() {
      if (!this.sheet?.id) return
      const restored = await useCompanionApi().deleteCreature(this.session(), this.sheet.id)
      if (restored) this.sheet = restored
      else this.close()
      void this.load()
    },
    async uploadImage(file: File) {
      if (!this.sheet?.id) return
      this.sheet = await useCompanionApi().uploadCreatureImage(this.session(), this.sheet.id, file)
      void useImageCacheStore().ensure(this.sheet.imageUrl)
      void this.load()
      void useCampaignStore().refreshBattlemap().catch(() => {})
    },
    async removeImage() {
      if (!this.sheet?.id) return
      this.sheet = await useCompanionApi().removeCreatureImage(this.session(), this.sheet.id)
      void this.load()
      void useCampaignStore().refreshBattlemap().catch(() => {})
    },
    async addToEncounter(count: number) {
      const campaign = useCampaignStore()
      const encounter = campaign.snapshot?.activeEncounter
      if (!this.sheet?.id || !encounter) return
      const next = await useCompanionApi().addCreaturesToEncounter(this.session(), encounter.id, this.sheet.id, count)
      campaign.updateActiveEncounter(next)
      await campaign.refreshBattlemap().catch(() => {})
    }
  }
})

export function blankCreature(): CreatureFields {
  return {
    name: '', nameEn: null, size: 'Medium', type: 'humanoide', alignment: null, armorClass: 10, armorClassNote: null,
    hitPoints: 10, hitDice: null, speed: '9 m', initiative: null, strength: 10, dexterity: 10, constitution: 10,
    intelligence: 10, wisdom: 10, charisma: 10, savingThrows: null, skills: null, damageVulnerabilities: null,
    damageResistances: null, damageImmunities: null, conditionImmunities: null, senses: null, languages: null,
    challengeRating: '1', experiencePoints: null, proficiencyBonus: null, traits: [], actions: [], bonusActions: [],
    reactions: [], legendaryActions: [], legendaryActionsIntro: null, notes: null
  }
}
