import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useCampaignStore } from '~/stores/campaign'
import { useCreatureStore } from '~/stores/creatures'
import { useImageCacheStore } from '~/stores/imageCache'
import TokenSourcePicker from '~/components/battlemap/TokenSourcePicker.vue'
import BattlemapToken from '~/components/battlemap/BattlemapToken.vue'
import type { BattlemapSnapshot, BattlemapToken as Token } from '~/types/campaign'

// Spec 016: players (and the master) drop a compendium creature or a catalog item on the board.
const api = {
  listCreatureTokenOptions: vi.fn(), createMarker: vi.fn(), listCreatures: vi.fn(), addCreaturesToEncounter: vi.fn(),
  getCreature: vi.fn(), battlemapImage: vi.fn(), getBattlemap: vi.fn()
}
const characterApi = { listCatalogItems: vi.fn() }
vi.stubGlobal('useCompanionApi', () => api)
vi.stubGlobal('useCharacterApi', () => characterApi)
vi.stubGlobal('useCampaignStore', () => useCampaignStore())
vi.stubGlobal('useCreatureStore', () => useCreatureStore())
vi.stubGlobal('useImageCacheStore', () => useImageCacheStore())

const wolf = { id: 'wolf', name: 'Lobo', size: 'Medium', type: 'fera', imageUrl: null }
const rope = { id: 'rope', name: 'Corda de cânhamo', nameEn: 'Hempen Rope', category: 'Gear', subcategory: null, rarity: null, requiresAttunement: false, magicBonus: 0, requiresBaseItem: false }
const board = { id: 'map', encounterId: 'e1', columns: 10, rows: 10, isGridVisible: true, revision: 'r', imageRevision: 'i', imageUrl: '', imageMediaType: 'image/png', imageFileName: 'm.png', updatedAt: '', tokens: [] } as BattlemapSnapshot

function ready(role: 'Master' | 'Player' = 'Player') {
  setActivePinia(createPinia())
  const campaign = useCampaignStore()
  campaign.session = { campaignId: 'c1', playerCode: '1234', masterCode: null, participantId: 'p1', displayName: 'Ayla', role, participantToken: 't' } as never
  campaign.connection = 'online'
  campaign.battlemap = structuredClone(board)
  return campaign
}

beforeEach(() => {
  for (const fn of [...Object.values(api), ...Object.values(characterApi)]) fn.mockReset()
  api.listCreatureTokenOptions.mockResolvedValue({ items: [wolf], total: 730, page: 1, pageSize: 8 })
  characterApi.listCatalogItems.mockResolvedValue({ items: [rope], total: 1, page: 1, pageSize: 8, attribution: null })
  api.createMarker.mockImplementation(async (_s, _e, body) => ({ ...board, tokens: [{ participantId: 'm1', displayName: 'X', initials: 'X', colorKey: 1, column: null, row: null, positionVersion: null, isMarker: true, sourceKind: body.creatureId ? 'creature' : 'item', sourceId: body.creatureId ?? body.itemId }] }))
})

describe('TokenSourcePicker', () => {
  it('lists compendium creatures for a player and adds one as a reserve token', async () => {
    const campaign = ready()
    const wrapper = mount(TokenSourcePicker)
    await flushPromises()
    expect(api.listCreatureTokenOptions).toHaveBeenCalledWith(expect.anything(), '', 8)
    expect(wrapper.text()).toContain('Lobo')
    expect(wrapper.text()).toContain('Média · fera')
    expect(wrapper.text()).toContain('Mostrando 1 de 730')
    // The player has no "combat" mode: that is the master's encounter tool.
    expect(wrapper.find('[data-token-source-mode]').exists()).toBe(false)

    await wrapper.get('[data-token-source-creature="wolf"] button').trigger('click')
    await flushPromises()
    expect(api.createMarker).toHaveBeenCalledWith(expect.anything(), 'e1', { creatureId: 'wolf' })
    expect(campaign.battlemap?.tokens[0]?.sourceKind).toBe('creature')
    expect(wrapper.get('[data-token-source-done]').text()).toContain('Lobo foi para a reserva')
  })

  it('switches to items and adds a catalog item', async () => {
    ready()
    const wrapper = mount(TokenSourcePicker)
    await flushPromises()
    await wrapper.get('[data-token-source-tab="items"]').trigger('click')
    await flushPromises()
    expect(characterApi.listCatalogItems).toHaveBeenCalledWith(expect.anything(), { q: '', page: 1, pageSize: 8 })
    expect(wrapper.get('[data-token-source-tab="items"]').attributes('aria-selected')).toBe('true')
    expect(wrapper.text()).toContain('Corda de cânhamo')
    expect(wrapper.text()).toContain('Equipamento')

    await wrapper.get('[data-token-source-item="rope"] button').trigger('click')
    await flushPromises()
    expect(api.createMarker).toHaveBeenCalledWith(expect.anything(), 'e1', { itemId: 'rope' })
  })

  it('searches as the user types (debounced, one request for a burst)', async () => {
    vi.useFakeTimers()
    ready()
    const wrapper = mount(TokenSourcePicker)
    await flushPromises()
    await wrapper.get('[data-token-source-search]').setValue('lo')
    await wrapper.get('[data-token-source-search]').setValue('lobo')
    vi.advanceTimersByTime(300)
    await flushPromises()
    expect(api.listCreatureTokenOptions).toHaveBeenCalledTimes(2)
    expect(api.listCreatureTokenOptions).toHaveBeenLastCalledWith(expect.anything(), 'lobo', 8)
    vi.useRealTimers()
  })

  it('shows the server error and keeps the list when adding fails', async () => {
    ready()
    api.createMarker.mockRejectedValueOnce(new Error('Criatura não encontrada no compêndio.'))
    const wrapper = mount(TokenSourcePicker)
    await flushPromises()
    await wrapper.get('[data-token-source-creature="wolf"] button').trigger('click')
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain('Criatura não encontrada')
    expect(wrapper.find('[data-token-source-done]').exists()).toBe(false)
    expect(wrapper.text()).toContain('Lobo')
  })

  it('disables adding while offline', async () => {
    const campaign = ready()
    campaign.connection = 'offline'
    const wrapper = mount(TokenSourcePicker)
    await flushPromises()
    expect(wrapper.get('[data-token-source-creature="wolf"] button').attributes('disabled')).toBeDefined()
    expect(wrapper.text()).toContain('Conexão necessária')
  })

  it('offers the master a reinforcement switch only while the combat runs (spec 008 reinforcements)', async () => {
    const campaign = ready('Master')
    api.listCreatures.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 6 })
    campaign.snapshot = { activeEncounter: { id: 'e1', status: 'Preparing', round: 0, currentTurnIndex: null, combatants: [] } } as never
    const wrapper = mount(TokenSourcePicker, { props: { isMaster: true } })
    await flushPromises()
    // Preparing: the Combatentes panel is on screen, so no second "add to combat" list here.
    expect(wrapper.find('[data-token-source-reinforcement]').exists()).toBe(false)

    campaign.snapshot = { activeEncounter: { id: 'e1', status: 'Active', round: 1, currentTurnIndex: 0, combatants: [] } } as never
    await flushPromises()
    await wrapper.get('[data-token-source-reinforcement]').setValue(true)
    await flushPromises()
    expect(wrapper.find('[data-creature-picker]').exists()).toBe(true)
    expect(wrapper.find('[data-token-source-creature="wolf"]').exists()).toBe(false)

    // Items never go into combat.
    await wrapper.get('[data-token-source-tab="items"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-token-source-reinforcement]').exists()).toBe(false)
    expect(wrapper.find('[data-creature-picker]').exists()).toBe(false)
  })

  it('never offers the reinforcement switch to a player, even mid-combat', async () => {
    const campaign = ready('Player')
    campaign.snapshot = { activeEncounter: { id: 'e1', status: 'Active', round: 1, currentTurnIndex: 0, combatants: [] } } as never
    const wrapper = mount(TokenSourcePicker)
    await flushPromises()
    expect(wrapper.find('[data-token-source-reinforcement]').exists()).toBe(false)
  })

  it('is a token picker only for the master while preparing: combat goes through the Combatentes panel', async () => {
    ready('Master')
    const wrapper = mount(TokenSourcePicker, { props: { isMaster: true } })
    await flushPromises()
    expect(wrapper.find('[data-token-source-creature="wolf"]').exists()).toBe(true)
    // One catalog per purpose on the screen (design review): no second "add to combat" list here.
    expect(wrapper.find('[data-creature-picker]').exists()).toBe(false)
    expect(api.listCreatures).not.toHaveBeenCalled()
    await wrapper.get('[data-token-source-creature="wolf"] button').trigger('click')
    await flushPromises()
    expect(api.createMarker).toHaveBeenCalledWith(expect.anything(), 'e1', { creatureId: 'wolf' })
    expect(api.addCreaturesToEncounter).not.toHaveBeenCalled()
  })
})

describe('BattlemapToken shape (spec 016)', () => {
  const base: Token = { participantId: 't1', displayName: 'Baú', initials: 'BA', colorKey: 2, column: 1, row: 1, positionVersion: 'v', isMarker: true }
  const mountToken = (token: Token) => mount({
    components: { BattlemapToken },
    setup: () => ({ token }),
    template: '<svg><BattlemapToken :token="token" :x="0" :y="0" :size="40" :selected="false" :controllable="true" :pending="false" /></svg>'
  })

  it('draws catalog items as rounded squares', () => {
    const wrapper = mountToken({ ...base, sourceKind: 'item', sourceId: 'chest' })
    expect(wrapper.get('[data-battlemap-token]').classes()).toContain('item')
    expect(wrapper.get('.token-body').element.tagName).toBe('rect')
  })

  it('keeps creatures and free-text tokens round', () => {
    for (const sourceKind of ['creature', null] as const) {
      const wrapper = mountToken({ ...base, sourceKind })
      expect(wrapper.get('.token-body').element.tagName).toBe('circle')
    }
  })
})
