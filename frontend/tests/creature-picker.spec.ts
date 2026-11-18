import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useCampaignStore } from '~/stores/campaign'
import { useCreatureStore } from '~/stores/creatures'
import { useImageCacheStore } from '~/stores/imageCache'
import CreaturePicker from '~/components/creatures/CreaturePicker.vue'
import type { CreatureSummary } from '~/types/creatures'

// The catalog has to be reachable from combat preparation, not only from the Criaturas tab.
const api = { listCreatures: vi.fn(), addCreaturesToEncounter: vi.fn(), getCreature: vi.fn(), battlemapImage: vi.fn(), getBattlemap: vi.fn() }
vi.stubGlobal('useCompanionApi', () => api)
vi.stubGlobal('useCampaignStore', () => useCampaignStore())
vi.stubGlobal('useCreatureStore', () => useCreatureStore())
vi.stubGlobal('useImageCacheStore', () => useImageCacheStore())

const goblin: CreatureSummary = { id: 'goblin', name: 'Goblin', nameEn: 'Goblin', size: 'Small', type: 'humanoide', challengeRating: '1/4', armorClass: 15, hitPoints: 7, origin: 'Compendium', imageUrl: null }
const encounter = { id: 'e1', status: 'Preparing', round: 0, currentTurnIndex: null, combatants: [] }

function ready() {
  setActivePinia(createPinia())
  const campaign = useCampaignStore()
  campaign.session = { campaignId: 'c1', playerCode: '1234', masterCode: '9999', participantId: 'm', displayName: 'Mestre', role: 'Master', participantToken: 't' }
  campaign.connection = 'online'
  campaign.snapshot = {
    campaign: { id: 'c1', playerCode: '1234', name: 'Mesa' },
    currentParticipant: { id: 'm', displayName: 'Mestre', role: 'Master', isOnline: true, initiativeModifier: null, currentHitPoints: null, maxHitPoints: null },
    participants: [], diceRolls: [], activeEncounter: structuredClone(encounter)
  } as never
  vi.spyOn(campaign, 'refreshBattlemap').mockResolvedValue(undefined as never)
  return campaign
}

beforeEach(() => {
  for (const fn of Object.values(api)) fn.mockReset()
  api.listCreatures.mockResolvedValue({ items: [goblin], total: 725, page: 1, pageSize: 6 })
})

describe('CreaturePicker (catalog in combat preparation)', () => {
  it('lists the catalog on open and says how many more exist', async () => {
    ready()
    const wrapper = mount(CreaturePicker)
    await flushPromises()
    expect(api.listCreatures).toHaveBeenCalledWith(expect.anything(), expect.objectContaining({ q: '', pageSize: 6 }))
    expect(wrapper.text()).toContain('Goblin')
    expect(wrapper.text()).toContain('Mostrando 1 de 725')
  })

  it('searches as the master types (debounced)', async () => {
    vi.useFakeTimers()
    ready()
    const wrapper = mount(CreaturePicker)
    await flushPromises()
    await wrapper.get('input[type="search"]').setValue('dragão')
    await wrapper.get('input[type="search"]').setValue('dragão verm')
    vi.advanceTimersByTime(300)
    await flushPromises()
    expect(api.listCreatures).toHaveBeenCalledTimes(2)
    expect(api.listCreatures).toHaveBeenLastCalledWith(expect.anything(), expect.objectContaining({ q: 'dragão verm' }))
    vi.useRealTimers()
  })

  it('adds N copies, updates the encounter and resets the count', async () => {
    const campaign = ready()
    const next = { ...encounter, combatants: [{ id: 'c1', name: 'Goblin 1' }, { id: 'c2', name: 'Goblin 2' }, { id: 'c3', name: 'Goblin 3' }] }
    api.addCreaturesToEncounter.mockResolvedValue(next)
    const wrapper = mount(CreaturePicker)
    await flushPromises()
    const plus = wrapper.get('[aria-label="Mais uma"]')
    await plus.trigger('click'); await plus.trigger('click')
    expect(wrapper.get('[data-picker-count="goblin"]').text()).toBe('3')
    await wrapper.get('[data-picker-add="goblin"]').trigger('click')
    await flushPromises()
    expect(api.addCreaturesToEncounter).toHaveBeenCalledWith(campaign.session, 'e1', 'goblin', 3)
    expect(campaign.snapshot!.activeEncounter!.combatants).toHaveLength(3)
    expect(campaign.refreshBattlemap).toHaveBeenCalled()
    expect(wrapper.text()).toContain('3 × Goblin adicionados ao combate.')
    expect(wrapper.get('[data-picker-count="goblin"]').text()).toBe('1')
  })

  it('caps the count at 20 and never below 1', async () => {
    ready()
    const wrapper = mount(CreaturePicker)
    await flushPromises()
    expect(wrapper.get('[aria-label="Menos uma"]').attributes('disabled')).toBeDefined()
    for (let i = 0; i < 25; i++) await wrapper.get('[aria-label="Mais uma"]').trigger('click')
    expect(wrapper.get('[data-picker-count="goblin"]').text()).toBe('20')
    expect(wrapper.get('[aria-label="Mais uma"]').attributes('disabled')).toBeDefined()
  })

  it('shows the server error instead of failing silently', async () => {
    ready()
    api.addCreaturesToEncounter.mockRejectedValue(new Error('O combate já começou.'))
    const wrapper = mount(CreaturePicker)
    await flushPromises()
    await wrapper.get('[data-picker-add="goblin"]').trigger('click')
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain('O combate já começou.')
  })

  it('clicking the creature opens its sheet', async () => {
    ready()
    api.getCreature.mockResolvedValue({ ...goblin })
    const wrapper = mount(CreaturePicker)
    await flushPromises()
    await wrapper.get('[aria-label="Ver ficha de Goblin"]').trigger('click')
    await flushPromises()
    expect(api.getCreature).toHaveBeenCalledWith(expect.anything(), 'goblin')
    expect(useCreatureStore().sheet?.id).toBe('goblin')
  })

  it('explains reinforcements while combat is running', async () => {
    const campaign = ready()
    campaign.snapshot!.activeEncounter!.status = 'Active'
    const wrapper = mount(CreaturePicker)
    await flushPromises()
    expect(wrapper.find('[data-picker-running]').text()).toContain('iniciativa rolada')
  })

  it('a slow earlier search never overwrites the latest results', async () => {
    vi.useFakeTimers()
    ready()
    const wrapper = mount(CreaturePicker)
    await flushPromises()
    let slow!: (value: unknown) => void
    api.listCreatures
      .mockReturnValueOnce(new Promise(res => { slow = res }))
      .mockResolvedValueOnce({ items: [{ ...goblin, id: 'orc', name: 'Orc' }], total: 1, page: 1, pageSize: 6 })
    await wrapper.get('input[type="search"]').setValue('gob')
    vi.advanceTimersByTime(300)
    await wrapper.get('input[type="search"]').setValue('orc')
    vi.advanceTimersByTime(300)
    await flushPromises()
    slow({ items: [goblin], total: 1, page: 1, pageSize: 6 })
    await flushPromises()
    expect(wrapper.findAll('.creature-picker-row').map(row => row.text())).toEqual([expect.stringContaining('Orc')])
    vi.useRealTimers()
  })

  it('blocks adding once the combat ended or while offline', async () => {
    const campaign = ready()
    const wrapper = mount(CreaturePicker)
    await flushPromises()
    campaign.snapshot!.activeEncounter!.status = 'Ended'
    await flushPromises()
    expect(wrapper.get('[data-picker-add="goblin"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-picker-blocked]').text()).toContain('já terminou')
    campaign.snapshot!.activeEncounter!.status = 'Preparing'
    campaign.connection = 'offline'
    await flushPromises()
    expect(wrapper.get('[data-picker-blocked]').text()).toContain('Conexão')
    await wrapper.get('[data-picker-add="goblin"]').trigger('click')
    expect(api.addCreaturesToEncounter).not.toHaveBeenCalled()
  })

  it('two pickers on the page share one add at a time', async () => {
    ready()
    let answer!: (value: unknown) => void
    api.addCreaturesToEncounter.mockReturnValue(new Promise(res => { answer = res }))
    const setupPicker = mount(CreaturePicker)
    const boardPicker = mount(CreaturePicker)
    await flushPromises()
    await setupPicker.get('[data-picker-add="goblin"]').trigger('click')
    expect(boardPicker.get('[data-picker-add="goblin"]').attributes('disabled')).toBeDefined()
    await boardPicker.get('[data-picker-add="goblin"]').trigger('click')
    expect(api.addCreaturesToEncounter).toHaveBeenCalledTimes(1)
    answer({ ...encounter })
    await flushPromises()
    expect(boardPicker.get('[data-picker-add="goblin"]').attributes('disabled')).toBeUndefined()
  })
})
