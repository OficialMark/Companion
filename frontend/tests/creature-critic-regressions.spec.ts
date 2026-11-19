import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useCampaignStore } from '~/stores/campaign'
import { useCreatureStore } from '~/stores/creatures'
import { useImageCacheStore } from '~/stores/imageCache'
import CreatureSheetModal from '~/components/creatures/CreatureSheetModal.vue'
import CreatureLibrary from '~/components/creatures/CreatureLibrary.vue'
import BattlemapSetup from '~/components/battlemap/BattlemapSetup.vue'
import { parseActionRolls } from '~/utils/creatureRoll'
import type { Creature } from '~/types/creatures'

// Regressions for the cold critic's findings on spec 008 (see specs/008-biblioteca-criaturas/tasks.md, T042).
const api = {
  listCreatures: vi.fn(), creatureTypes: vi.fn(), getCreature: vi.fn(), saveCreature: vi.fn(), battlemapImage: vi.fn(),
  setRollPreset: vi.fn(), rollDice: vi.fn(), revealDiceRoll: vi.fn(), configureBattlemap: vi.fn()
}
vi.stubGlobal('useCompanionApi', () => api)
vi.stubGlobal('useCampaignStore', () => useCampaignStore())
vi.stubGlobal('useCreatureStore', () => useCreatureStore())
vi.stubGlobal('useImageCacheStore', () => useImageCacheStore())

const goblin: Creature = {
  id: 'goblin', baseCreatureId: null, origin: 'Compendium', sourceBook: 'SRD 5.1', sourceUrl: null, imageUrl: null, hasImage: false,
  footprintSize: 1, initiativeModifier: 2, revision: 'r1', name: 'Goblin', nameEn: 'Goblin', size: 'Small', type: 'humanoide',
  alignment: null, armorClass: 15, armorClassNote: null, hitPoints: 7, hitDice: '2d6', speed: '9 m', initiative: 2,
  strength: 8, dexterity: 14, constitution: 10, intelligence: 10, wisdom: 8, charisma: 8, savingThrows: null, skills: null,
  damageVulnerabilities: null, damageResistances: null, damageImmunities: null, conditionImmunities: null, senses: null,
  languages: null, challengeRating: '1/4', experiencePoints: 50, proficiencyBonus: 2, traits: [], actions: [], bonusActions: [],
  reactions: [], legendaryActions: [], legendaryActionsIntro: null, notes: null
}

function ready() {
  setActivePinia(createPinia())
  const campaign = useCampaignStore()
  campaign.session = { campaignId: 'c1', playerCode: '1234', masterCode: '9999', participantId: 'm', displayName: 'Mestre', role: 'Master', participantToken: 't' }
  campaign.connection = 'online'
  campaign.snapshot = {
    campaign: { id: 'c1', playerCode: '1234', name: 'Mesa' },
    currentParticipant: { id: 'm', displayName: 'Mestre', role: 'Master', isOnline: true, initiativeModifier: null, currentHitPoints: null, maxHitPoints: null },
    participants: [], diceRolls: [], activeEncounter: { id: 'e1', status: 'Preparing', round: 0, currentTurnIndex: null, combatants: [] }
  }
  return { campaign, creatures: useCreatureStore(), images: useImageCacheStore() }
}

beforeEach(() => { for (const fn of Object.values(api)) fn.mockReset() })

describe('critic regressions (spec 008)', () => {
  it('#3 five tokens sharing one image start ONE request; clear() revokes the blob URLs', async () => {
    const { images } = ready()
    let resolve!: (blob: Blob) => void
    api.battlemapImage.mockReturnValue(new Promise<Blob>(res => { resolve = res }))
    const created = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:goblin')
    const revoked = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {})
    const pending = Array.from({ length: 5 }, () => images.ensure('/api/img?rev=1'))
    resolve(new Blob(['x']))
    await Promise.all(pending)
    expect(api.battlemapImage).toHaveBeenCalledTimes(1)
    expect(created).toHaveBeenCalledTimes(1)
    images.clear()
    expect(revoked).toHaveBeenCalledWith('blob:goblin')
    created.mockRestore(); revoked.mockRestore()
  })

  it('#5 a board refresh with the same preset does not wipe the choices being made', async () => {
    const { campaign, creatures } = ready()
    campaign.battlemap = { encounterId: 'e1', tokens: [{ participantId: 'tok-1', rollPreset: { count: 1, die: 20, ability: 'str', bonus: 0 } }] } as never
    api.getCreature.mockResolvedValue(structuredClone(goblin))
    await creatures.open('goblin', { id: 'tok-1', name: 'Goblin 1' })
    const wrapper = mount(CreatureSheetModal, { attachTo: document.body })
    await flushPromises()
    await wrapper.get('[data-die="8"]').trigger('click')
    await wrapper.get('[data-ability="dex"]').trigger('click')
    // Someone moves a token: the snapshot is replaced by new objects with the same preset.
    campaign.battlemap = { encounterId: 'e1', tokens: [{ participantId: 'tok-1', rollPreset: { count: 1, die: 20, ability: 'str', bonus: 0 } }] } as never
    await flushPromises()
    expect(wrapper.get('[data-roll-preview]').text()).toBe('1d8 + 2 (DES)')
    wrapper.unmount()
  })

  it('#8 clearing an optional number sends null, never an empty string', async () => {
    const { creatures } = ready()
    api.getCreature.mockResolvedValue(structuredClone(goblin))
    api.saveCreature.mockResolvedValue({ ...goblin, initiative: null })
    await creatures.open('goblin')
    const wrapper = mount(CreatureSheetModal, { attachTo: document.body })
    await flushPromises()
    await wrapper.get('[data-edit-creature]').trigger('click')
    await wrapper.get('input[placeholder="DES"]').setValue('')
    await wrapper.get('[data-creature-editor]').trigger('submit')
    await flushPromises()
    const body = api.saveCreature.mock.calls[0]![2]
    expect(body.initiative).toBeNull()
    expect(body.armorClass).toBe(15)
    wrapper.unmount()
  })

  it('a required number left empty is reported instead of sent', async () => {
    const { creatures } = ready()
    api.getCreature.mockResolvedValue(structuredClone(goblin))
    await creatures.open('goblin')
    const wrapper = mount(CreatureSheetModal, { attachTo: document.body })
    await flushPromises()
    await wrapper.get('[data-edit-creature]').trigger('click')
    await wrapper.get('[data-field="armorClass"]').setValue('')
    await wrapper.get('[data-creature-editor]').trigger('submit')
    await flushPromises()
    expect(api.saveCreature).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Preencha: CA')
    wrapper.unmount()
  })

  it('Esc works after focus fell to <body> (document-level listener)', async () => {
    const { creatures } = ready()
    api.getCreature.mockResolvedValue(structuredClone(goblin))
    await creatures.open('goblin')
    const wrapper = mount(CreatureSheetModal, { attachTo: document.body })
    await flushPromises()
    ;(document.activeElement as HTMLElement | null)?.blur()
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }))
    expect(creatures.sheet).toBeNull()
    wrapper.unmount()
  })

  it('a slow response for an earlier creature never replaces the latest one', async () => {
    const { creatures } = ready()
    let slow!: (value: Creature) => void
    api.getCreature
      .mockReturnValueOnce(new Promise<Creature>(res => { slow = res }))
      .mockResolvedValueOnce({ ...goblin, id: 'orc', name: 'Orc' })
    const first = creatures.open('goblin', { id: 'tok-a', name: 'Goblin 1' })
    await creatures.open('orc', { id: 'tok-b', name: 'Orc 1' })
    slow(structuredClone(goblin))
    await first
    expect(creatures.sheet?.name).toBe('Orc')
    expect(creatures.sheetToken?.id).toBe('tok-b')
  })

  it('the ND selects show the active filter and reset with Limpar', async () => {
    ready()
    api.listCreatures.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 24 })
    api.creatureTypes.mockResolvedValue([])
    const wrapper = mount(CreatureLibrary)
    await flushPromises()
    await wrapper.get('[aria-label="ND máximo"]').setValue('1/4')
    await flushPromises()
    expect((wrapper.get('[aria-label="ND máximo"]').element as HTMLSelectElement).value).toBe('1/4')
    await wrapper.findAll('button').find(button => button.text().includes('Limpar'))!.trigger('click')
    await flushPromises()
    expect((wrapper.get('[aria-label="ND máximo"]').element as HTMLSelectElement).value).toBe('')
  })

  it('action parsing only treats real signs as bonuses (the old class matched digits and letters)', () => {
    expect(parseActionRolls('Ataque Corpo a Corpo: 15 m de alcance.')).toEqual([])
    expect(parseActionRolls('Ataque: +5 para acertar.').map(item => item.label)).toEqual(['Acerto +5'])
    expect(parseActionRolls('Ataque: −1 para acertar. 3 (1d6 − 1) de dano.').map(item => item.label)).toEqual(['Acerto -1', 'Dano 1d6-1'])
  })

  it('the "fog restarted" notice survives intermediate grid values while typing', async () => {
    ready()
    const wrapper = mount(BattlemapSetup, { props: { encounterId: 'e1', battlemap: null } })
    const fileInput = wrapper.get('input[type="file"]').element as HTMLInputElement
    Object.defineProperty(fileInput, 'files', { value: [new File(['x'], 'map.png', { type: 'image/png' })], configurable: true })
    await fileInput.dispatchEvent(new Event('change'))
    await nextTick()
    await wrapper.get('[data-fog-reveal-all]').trigger('click') // the master shaped the fog
    await wrapper.get('[aria-label="Colunas do grid"]').setValue('1')
    await wrapper.get('[aria-label="Colunas do grid"]').setValue('12')
    expect(wrapper.find('[data-fog-restarted]').exists()).toBe(true)
  })

  it('round 2 N3: a slow open() never lands on the blank "Nova criatura" sheet', async () => {
    const { creatures } = ready()
    let slow!: (value: Creature) => void
    api.getCreature.mockReturnValueOnce(new Promise<Creature>(res => { slow = res }))
    const opening = creatures.open('goblin')
    creatures.newCreature()
    slow(structuredClone(goblin))
    await opening
    expect(creatures.sheet?.id).toBe('')
    expect(creatures.editing).toBe(true)
  })

  it('round 2 N4: closing with x or the backdrop while editing asks first', async () => {
    const { creatures } = ready()
    api.getCreature.mockResolvedValue(structuredClone(goblin))
    await creatures.open('goblin')
    const wrapper = mount(CreatureSheetModal, { attachTo: document.body })
    await flushPromises()
    await wrapper.get('[data-edit-creature]').trigger('click')
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
    await wrapper.get('[aria-label="Fechar ficha"]').trigger('click')
    expect(confirm).toHaveBeenCalled()
    expect(creatures.sheet).not.toBeNull()
    confirm.mockReturnValue(true)
    await wrapper.get('.creature-sheet-overlay').trigger('pointerdown')
    await wrapper.get('.creature-sheet-overlay').trigger('click')
    expect(creatures.sheet).toBeNull()
    confirm.mockRestore()
    wrapper.unmount()
  })

  it('round 3: a save answered after the sheet was closed does not reopen it', async () => {
    const { creatures } = ready()
    api.getCreature.mockResolvedValue(structuredClone(goblin))
    api.listCreatures.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 24 })
    let answer!: (value: Creature) => void
    api.saveCreature.mockReturnValue(new Promise<Creature>(res => { answer = res }))
    await creatures.open('goblin')
    const saving = creatures.save({ ...structuredClone(goblin) })
    creatures.close()
    answer({ ...goblin, armorClass: 18 })
    await saving
    expect(creatures.sheet).toBeNull()
  })
})
