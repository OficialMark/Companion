import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useCampaignStore } from '~/stores/campaign'
import { useCharacterStore } from '~/stores/character'
import CatalogPicker from '~/components/character/CatalogPicker.vue'
import CompendiumTabs from '~/components/CompendiumTabs.vue'

// Spec 012, US2: the master's Compêndio has Criaturas, Magias and Itens tabs; Magias and Itens are a
// read-only browser of the catalog with the full details of each row.
const api = { listCatalogItems: vi.fn(), listCatalogSpells: vi.fn(), getCatalogItem: vi.fn(), getCatalogSpell: vi.fn(), addItemFromCatalog: vi.fn(), addSpellFromCatalog: vi.fn() }
vi.stubGlobal('useCharacterApi', () => api)
vi.stubGlobal('useCampaignStore', () => useCampaignStore())
vi.stubGlobal('useCharacterStore', () => useCharacterStore())

const query = ref<Record<string, string>>({})
const replace = vi.fn(({ query: next }: { query: Record<string, string> }) => { query.value = next; return Promise.resolve() })
vi.stubGlobal('useRoute', () => reactive({ get query() { return query.value } }))
vi.stubGlobal('useRouter', () => ({ replace }))

const fireball = { id: 's1', name: 'Bola de Fogo', nameEn: 'Fireball', level: 3, school: 'evocação', ritual: false, concentration: false, classes: ['mago'] }
const fireballDetail = {
  id: 's1', name: 'Bola de Fogo', nameEn: 'Fireball', level: 3, classes: ['feiticeiro', 'mago'], higherLevels: 'Mais 1d6 por círculo.',
  template: { name: 'Bola de Fogo', level: 3, school: 'evocação', castingTime: '1 ação', range: '45 metros', components: 'V, S, M', duration: 'instantânea', ritual: false, concentration: false, description: 'Uma explosão de chamas.', attackKind: 'Save', saveAbility: 'des', damageDice: '8d6', damageType: 'fogo', addAbilityToDamage: false }
}
const rope = { id: 'i1', name: 'Corda de Cânhamo', nameEn: 'Hempen Rope', category: 'Gear', subcategory: 'Equipamento', rarity: null, requiresAttunement: false, magicBonus: 0, requiresBaseItem: false }
const ropeDetail = { ...rope, cost: '1 PO', template: { name: 'Corda de Cânhamo', category: 'Gear', quantity: 1, weight: 5, description: 'Quinze metros.', notes: null, equipped: false, proficient: false, magicBonus: 0, weapon: null, armor: null, shield: null, effects: [] } }

beforeEach(() => {
  setActivePinia(createPinia())
  useCampaignStore().session = { campaignId: 'c1', role: 'Master', participantToken: 'tok' } as never
  for (const fn of Object.values(api)) fn.mockReset()
  api.listCatalogSpells.mockResolvedValue({ items: [fireball], total: 1, page: 1, pageSize: 20, attribution: null })
  api.listCatalogItems.mockResolvedValue({ items: [rope], total: 1, page: 1, pageSize: 20, attribution: 'SRD' })
  api.getCatalogSpell.mockResolvedValue(fireballDetail)
  api.getCatalogItem.mockResolvedValue(ropeDetail)
  query.value = { tela: 'compendio' }
  replace.mockClear()
})

describe('CompendiumTabs', () => {
  const stubs = { CreatureLibrary: { template: '<div data-stub-library />' } }

  it('starts on Criaturas and keeps the chosen tab in the URL', async () => {
    const wrapper = mount(CompendiumTabs, { global: { stubs } })
    expect(wrapper.get('h1').text()).toBe('Compêndio')
    expect(wrapper.findAll('[role="tab"]').map(tab => tab.text())).toEqual(['Criaturas', 'Magias', 'Itens'])
    expect(wrapper.get('[data-compendium-tab="criaturas"]').attributes('aria-selected')).toBe('true')
    expect(wrapper.find('[data-stub-library]').exists()).toBe(true)

    await wrapper.get('[data-compendium-tab="magias"]').trigger('click')
    await flushPromises()
    expect(replace).toHaveBeenCalledWith({ query: { tela: 'compendio', aba: 'magias' } })
    expect(wrapper.get('[data-compendium-panel="magias"]').find('[data-catalog-browse]').exists()).toBe(true)
  })

  it('arrow keys, Home and End move between tabs', async () => {
    query.value = { tela: 'compendio', aba: 'itens' }
    const wrapper = mount(CompendiumTabs, { global: { stubs }, attachTo: document.body })
    await wrapper.get('[role="tablist"]').trigger('keydown', { key: 'ArrowRight' })
    expect(query.value.aba).toBeUndefined() // wraps to Criaturas, which is the default (no ?aba)
    await wrapper.get('[role="tablist"]').trigger('keydown', { key: 'End' })
    expect(query.value.aba).toBe('itens')
    await wrapper.get('[role="tablist"]').trigger('keydown', { key: 'ArrowLeft' })
    expect(query.value.aba).toBe('magias')
    wrapper.unmount()
  })
})

describe('CatalogPicker em modo navegação (Compêndio)', () => {
  it('spells: shows the English name, no Adicionar, and the details on demand (fetched once)', async () => {
    const wrapper = mount(CatalogPicker, { props: { mode: 'spells', browse: true } })
    await flushPromises()

    expect(wrapper.find('.modal-overlay').exists()).toBe(false)
    expect(wrapper.find('[data-add-catalog]').exists()).toBe(false)
    expect(wrapper.get('.catalog-row-name').text()).toContain('Fireball')

    const toggle = wrapper.get('[data-catalog-detail-toggle]')
    await toggle.trigger('click')
    await flushPromises()
    expect(toggle.attributes('aria-expanded')).toBe('true')
    const detail = wrapper.get('[data-catalog-detail]').text()
    expect(detail).toContain('3º círculo, evocação')
    expect(detail).toContain('45 metros')
    expect(detail).toContain('Uma explosão de chamas.')
    expect(detail).toContain('Mais 1d6 por círculo.')

    await toggle.trigger('click')
    await toggle.trigger('click')
    await flushPromises()
    expect(api.getCatalogSpell).toHaveBeenCalledTimes(1)
  })

  it('items: details show category, weight, cost and description', async () => {
    const wrapper = mount(CatalogPicker, { props: { mode: 'items', browse: true } })
    await flushPromises()
    await wrapper.get('[data-catalog-detail-toggle]').trigger('click')
    await flushPromises()
    const detail = wrapper.get('[data-catalog-detail]').text()
    expect(detail).toContain('Equipamento')
    expect(detail).toContain('5 kg')
    expect(detail).toContain('1 PO')
    expect(detail).toContain('Quinze metros.')
  })

  it('a failed detail shows the error in the row', async () => {
    api.getCatalogItem.mockRejectedValue(new Error('Item não encontrado no catálogo.'))
    const wrapper = mount(CatalogPicker, { props: { mode: 'items', browse: true } })
    await flushPromises()
    await wrapper.get('[data-catalog-detail-toggle]').trigger('click')
    await flushPromises()
    expect(wrapper.get('.catalog-row-detail [role="alert"]').text()).toBe('Item não encontrado no catálogo.')
  })
})
