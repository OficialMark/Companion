import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useCampaignStore } from '~/stores/campaign'
import { useCreatureStore } from '~/stores/creatures'
import { useImageCacheStore } from '~/stores/imageCache'
import CreatureLibrary from '~/components/creatures/CreatureLibrary.vue'
import CreatureSheetModal from '~/components/creatures/CreatureSheetModal.vue'
import type { Creature } from '~/types/creatures'

const api = {
  listCreatures: vi.fn(), creatureTypes: vi.fn(), getCreature: vi.fn(), saveCreature: vi.fn(), createCreature: vi.fn(),
  saveCreatureNotes: vi.fn(), deleteCreature: vi.fn(), uploadCreatureImage: vi.fn(), removeCreatureImage: vi.fn(),
  addCreaturesToEncounter: vi.fn(), setRollPreset: vi.fn(), rollDice: vi.fn(), revealDiceRoll: vi.fn(), battlemapImage: vi.fn(),
  createEncounter: vi.fn(), getBattlemap: vi.fn()
}
vi.stubGlobal('useCompanionApi', () => api)
vi.stubGlobal('useCampaignStore', () => useCampaignStore())
vi.stubGlobal('useCreatureStore', () => useCreatureStore())
vi.stubGlobal('useImageCacheStore', () => useImageCacheStore())
vi.stubGlobal('DiceRollAnimation', { template: '<div />' })

export const goblin: Creature = {
  id: 'goblin', baseCreatureId: null, origin: 'Compendium', sourceBook: 'SRD 5.1', sourceUrl: null, imageUrl: null, hasImage: false,
  footprintSize: 1, initiativeModifier: 2, revision: 'r1', name: 'Goblin', nameEn: 'Goblin', size: 'Small', type: 'humanoide (goblinoide)',
  alignment: 'Neutro e mau', armorClass: 15, armorClassNote: 'armadura de couro', hitPoints: 7, hitDice: '2d6', speed: '9 m', initiative: 2,
  strength: 8, dexterity: 14, constitution: 10, intelligence: 10, wisdom: 8, charisma: 8, savingThrows: null, skills: 'Furtividade +6',
  damageVulnerabilities: null, damageResistances: null, damageImmunities: null, conditionImmunities: null, senses: 'visão no escuro 18 m',
  languages: 'Comum, Goblin', challengeRating: '1/4', experiencePoints: 50, proficiencyBonus: 2,
  traits: [{ name: 'Fuga Ágil', description: 'Desengajar ou Esconder como ação bônus.' }],
  actions: [{ name: 'Cimitarra', description: 'Ataque com Arma Corpo a Corpo: +4 para acertar.\n5 (1d6 + 2) de dano cortante.' }],
  bonusActions: [], reactions: [], legendaryActions: [], legendaryActionsIntro: null, notes: null
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
  return { campaign, creatures: useCreatureStore() }
}

beforeEach(() => {
  for (const fn of Object.values(api)) fn.mockReset()
  api.listCreatures.mockResolvedValue({ items: [{ id: 'goblin', name: 'Goblin', nameEn: 'Goblin', size: 'Small', type: 'humanoide', challengeRating: '1/4', armorClass: 15, hitPoints: 7, origin: 'Compendium', imageUrl: null }], total: 1, page: 1, pageSize: 24 })
  api.creatureTypes.mockResolvedValue(['humanoide'])
  api.getCreature.mockResolvedValue(structuredClone(goblin))
})

describe('creature library (spec 008, US3)', () => {
  it('lists, searches with debounce and filters through the API, with the SRD notice and no Pocket DM credit (spec 011, FR-040)', async () => {
    vi.useFakeTimers()
    ready()
    const wrapper = mount(CreatureLibrary)
    await flushPromises()
    expect(wrapper.text()).toContain('Goblin')
    expect(wrapper.text()).not.toContain('Pocket DM')
    expect(wrapper.find('a[href*="pocketdm"]').exists()).toBe(false)
    expect(wrapper.find('.creature-credit a[href*="creativecommons"]').exists()).toBe(true)

    await wrapper.get('[data-creature-search]').setValue('lob')
    await wrapper.get('[data-creature-search]').setValue('lobo')
    vi.advanceTimersByTime(300)
    await flushPromises()
    const lastCall = api.listCreatures.mock.calls.at(-1)!
    expect(lastCall[1]).toMatchObject({ q: 'lobo', page: 1 })
    expect(api.listCreatures.mock.calls.filter(call => call[1].q === 'lob')).toHaveLength(0) // debounced
    vi.useRealTimers()
  })

  it('opening a card loads the full sheet into the modal', async () => {
    const { creatures } = ready()
    const wrapper = mount(CreatureLibrary)
    await flushPromises()
    await wrapper.get('[data-creature-id="goblin"]').trigger('click')
    await flushPromises()
    expect(api.getCreature).toHaveBeenCalledWith(expect.anything(), 'goblin')
    expect(creatures.sheet?.name).toBe('Goblin')
  })
})

describe('creature sheet (spec 008, US4/US5)', () => {
  async function openSheet(token: { id: string; name: string } | null = null) {
    const stores = ready()
    await stores.creatures.open('goblin', token)
    const wrapper = mount(CreatureSheetModal, { attachTo: document.body })
    await flushPromises()
    return { ...stores, wrapper }
  }

  it('shows stats with modifiers and action shortcuts that prefill the roll', async () => {
    const { wrapper } = await openSheet()
    expect(wrapper.get('[data-creature-sheet]').attributes('role')).toBe('dialog')
    expect(wrapper.text()).toContain('DES')
    expect(wrapper.text()).toContain('+2')
    expect(wrapper.text()).toContain('Cimitarra')
    await wrapper.get('[data-shortcut="damage"]').trigger('click')
    await flushPromises()
    expect(wrapper.get('[data-roll-preview]').text()).toBe('1d6 + 2')
    wrapper.unmount()
  })

  it('rolls with the label of the token and remembers the preset for that token', async () => {
    const { wrapper, campaign } = await openSheet({ id: 'tok-1', name: 'Goblin 2' })
    campaign.battlemap = { encounterId: 'e1', tokens: [] } as never
    api.rollDice.mockResolvedValue({ id: 'r1', results: [{ sequence: 1, terms: [], modifier: 0, total: 15 }] })
    api.revealDiceRoll.mockResolvedValue({ id: 'r1', total: 15, expression: '1d20+2+1', results: [], label: 'Goblin 2' })
    api.setRollPreset.mockResolvedValue({ encounterId: 'e1', tokens: [] })

    await wrapper.get('[data-ability="dex"]').trigger('click')
    await wrapper.get('[aria-label="Aumentar adicional"]').trigger('click')
    expect(wrapper.get('[data-roll-preview]').text()).toBe('1d20 + 2 (DES) + 1')
    vi.useFakeTimers()
    await wrapper.get('[data-roll-go]').trigger('click')
    await vi.runAllTimersAsync()
    vi.useRealTimers()
    await flushPromises()

    expect(api.rollDice).toHaveBeenCalledWith(expect.anything(), '1d20+2+1', false, 'Goblin 2')
    expect(api.setRollPreset).toHaveBeenCalledWith(expect.anything(), 'e1', 'tok-1', { count: 1, die: 20, ability: 'dex', bonus: 1 })
    expect(wrapper.text()).toContain('15')
    wrapper.unmount()
  })

  it('pre-selects the token preset from the board snapshot', async () => {
    setActivePinia(createPinia())
    const { campaign, creatures } = ready()
    campaign.battlemap = { encounterId: 'e1', tokens: [{ participantId: 'tok-1', rollPreset: { count: 2, die: 6, ability: 'str', bonus: -1 } }] } as never
    await creatures.open('goblin', { id: 'tok-1', name: 'Goblin 1' })
    const wrapper = mount(CreatureSheetModal, { attachTo: document.body })
    await flushPromises()
    expect(wrapper.get('[data-roll-preview]').text()).toBe('2d6 − 1 (FOR) − 1')
    wrapper.unmount()
  })

  it('adds N creatures to the encounter in preparation and explains when it cannot', async () => {
    const { wrapper, campaign } = await openSheet()
    api.addCreaturesToEncounter.mockResolvedValue({ id: 'e1', status: 'Preparing', round: 0, currentTurnIndex: null, combatants: [] })
    api.getBattlemap.mockRejectedValue(Object.assign(new Error('x'), { status: 404 }))
    await wrapper.get('[aria-label="Mais uma"]').trigger('click')
    await wrapper.get('[aria-label="Mais uma"]').trigger('click')
    await wrapper.get('[data-add-to-combat]').trigger('click')
    await flushPromises()
    expect(api.addCreaturesToEncounter).toHaveBeenCalledWith(expect.anything(), 'e1', 'goblin', 3)
    expect(wrapper.text()).toContain('3 × Goblin entraram no combate')

    // Reinforcements: still allowed while combat runs, refused once it ended.
    campaign.snapshot!.activeEncounter!.status = 'Active'
    await flushPromises()
    expect(wrapper.get('[data-add-to-combat]').attributes('disabled')).toBeUndefined()
    campaign.snapshot!.activeEncounter!.status = 'Ended'
    await flushPromises()
    expect(wrapper.get('[data-add-to-combat]').attributes('disabled')).toBeDefined()
    expect(wrapper.text()).toContain('Este combate já terminou.')
    wrapper.unmount()
  })

  it('saving notes on a compendium creature goes through the notes endpoint; Esc closes', async () => {
    const { wrapper, creatures } = await openSheet()
    api.saveCreatureNotes.mockResolvedValue({ ...goblin, id: 'copy', origin: 'Edited', baseCreatureId: 'goblin', notes: 'Emboscada' })
    await wrapper.get('textarea[aria-label="Anotações pessoais"]').setValue('Emboscada')
    await wrapper.get('[data-save-notes]').trigger('click')
    await flushPromises()
    expect(api.saveCreatureNotes).toHaveBeenCalledWith(expect.anything(), 'goblin', 'Emboscada', 'r1')
    expect(creatures.sheet?.origin).toBe('Edited')

    await wrapper.get('[data-creature-sheet]').trigger('keydown', { key: 'Escape' })
    expect(creatures.sheet).toBeNull()
    wrapper.unmount()
  })

  it('the editor saves edited fields with the expected revision', async () => {
    const { wrapper } = await openSheet()
    api.saveCreature.mockResolvedValue({ ...goblin, armorClass: 18, origin: 'Edited', id: 'copy' })
    await wrapper.get('[data-edit-creature]').trigger('click')
    await wrapper.get('[data-field="armorClass"]').setValue('18')
    await wrapper.get('[data-creature-editor]').trigger('submit')
    await flushPromises()
    expect(api.saveCreature).toHaveBeenCalledWith(expect.anything(), 'goblin', expect.objectContaining({ armorClass: 18, expectedRevision: 'r1', name: 'Goblin' }))
    expect(wrapper.find('[data-creature-editor]').exists()).toBe(false)
    wrapper.unmount()
  })
})
