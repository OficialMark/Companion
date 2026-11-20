import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useCampaignStore } from '~/stores/campaign'
import { useCharacterStore } from '~/stores/character'
import CatalogPicker from '~/components/character/CatalogPicker.vue'
import type { CatalogItemSummary, CatalogPage, CatalogSpellSummary } from '~/types/character'
import { makeCharacter } from './character-fixture'

// Spec 011, US7: catalog search waits for a pause, sends filters to the server, pages, adds items
// and spells, asks for the base item of generic magic items, and never lets a slow answer win.
const api = { listCatalogItems: vi.fn(), listCatalogSpells: vi.fn(), addItemFromCatalog: vi.fn(), addSpellFromCatalog: vi.fn() }
const request = vi.fn()
vi.stubGlobal('useCharacterApi', () => api)
vi.stubGlobal('useCompanionApi', () => ({ request }))
vi.stubGlobal('useCampaignStore', () => useCampaignStore())
vi.stubGlobal('useCharacterStore', () => useCharacterStore())

const ATTRIBUTION = 'Este trabalho inclui material do SRD 5.2.1, licenciado sob CC-BY-4.0.'

function summary(id: string, extra: Partial<CatalogItemSummary & { requiresBaseItem: boolean }> = {}) {
  return { id, name: id, nameEn: `${id} en`, category: 'Weapon', subcategory: 'Arma marcial corpo a corpo', rarity: null, requiresAttunement: false, magicBonus: 0, ...extra }
}
function itemPage(items: unknown[], total = items.length, page = 1): CatalogPage<CatalogItemSummary> {
  return { items: items as CatalogItemSummary[], total, page, pageSize: 20, attribution: ATTRIBUTION }
}
function spellPage(items: CatalogSpellSummary[], total = items.length): CatalogPage<CatalogSpellSummary> {
  return { items, total, page: 1, pageSize: 20, attribution: null }
}
function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((r) => { resolve = r })
  return { promise, resolve }
}

beforeEach(() => {
  vi.useFakeTimers()
  setActivePinia(createPinia())
  useCampaignStore().session = { campaignId: 'c1', playerCode: '1234', masterCode: null, participantId: 'p1', displayName: 'Loren', role: 'Player', participantToken: 'tok' } as never
  useCharacterStore().apply(makeCharacter(), { resetDraft: true })
  for (const fn of [...Object.values(api), request]) fn.mockReset()
})
afterEach(() => { vi.useRealTimers() })

const lastQuery = (fn: ReturnType<typeof vi.fn>) => fn.mock.calls.at(-1)![1]

describe('Catálogo de itens', () => {
  it('busca só depois da pausa de 300 ms e volta para a página 1', async () => {
    api.listCatalogItems.mockResolvedValue(itemPage([summary('espada')]))
    const wrapper = mount(CatalogPicker, { props: { mode: 'items' } })
    await flushPromises()
    expect(api.listCatalogItems).toHaveBeenCalledTimes(1)

    const input = wrapper.get('[data-catalog-search]')
    for (const text of ['e', 'es', 'esp']) {
      await input.setValue(text)
      await vi.advanceTimersByTimeAsync(100)
    }
    expect(api.listCatalogItems).toHaveBeenCalledTimes(1)
    await vi.advanceTimersByTimeAsync(300)
    await flushPromises()
    expect(api.listCatalogItems).toHaveBeenCalledTimes(2)
    expect(lastQuery(api.listCatalogItems)).toMatchObject({ q: 'esp', page: 1, pageSize: 20 })
  })

  it('manda categoria e raridade na consulta', async () => {
    api.listCatalogItems.mockResolvedValue(itemPage([summary('espada')]))
    const wrapper = mount(CatalogPicker, { props: { mode: 'items' } })
    await flushPromises()
    await wrapper.get('[data-filter="category"]').setValue('Armor')
    await flushPromises()
    expect(lastQuery(api.listCatalogItems)).toMatchObject({ category: 'Armor', page: 1 })
    await wrapper.get('[data-filter="rarity"]').setValue('Muito Raro')
    await flushPromises()
    expect(lastQuery(api.listCatalogItems)).toMatchObject({ category: 'Armor', rarity: 'Muito Raro' })
    expect(wrapper.findAll('[data-filter="rarity"] option').map(x => x.text()).slice(1))
      .toEqual(['Comum', 'Incomum', 'Raro', 'Muito Raro', 'Lendário', 'Artefato', 'Varia'])
  })

  it('pagina de 20 em 20', async () => {
    api.listCatalogItems.mockResolvedValue(itemPage(Array.from({ length: 20 }, (_, i) => summary(`item${i}`)), 45))
    const wrapper = mount(CatalogPicker, { props: { mode: 'items' } })
    await flushPromises()
    expect(wrapper.get('[data-page-label]').text()).toBe('Página 1 de 3')
    expect(wrapper.get('[data-page-prev]').attributes('disabled')).toBeDefined()
    await wrapper.get('[data-page-next]').trigger('click')
    await flushPromises()
    expect(lastQuery(api.listCatalogItems)).toMatchObject({ page: 2 })
    expect(wrapper.get('[data-page-label]').text()).toBe('Página 2 de 3')
  })

  it('adiciona um item comum direto na mochila', async () => {
    api.listCatalogItems.mockResolvedValue(itemPage([summary('Espada Longa')]))
    api.addItemFromCatalog.mockResolvedValue(makeCharacter())
    const wrapper = mount(CatalogPicker, { props: { mode: 'items' } })
    await flushPromises()
    await wrapper.get('[data-add-catalog]').trigger('click')
    await flushPromises()
    expect(api.addItemFromCatalog).toHaveBeenCalledWith(expect.anything(), 'Espada Longa', 1)
    expect(wrapper.get('[data-catalog-status]').text()).toContain('Espada Longa foi para a mochila.')
  })

  it('"Arma +1" pede a arma base e envia baseCatalogItemId', async () => {
    api.listCatalogItems.mockImplementation((_session, params) => Promise.resolve(params.mundane
      ? itemPage([summary('Espada Curta'), summary('Machado de Batalha', { nameEn: 'Battleaxe' })])
      : itemPage([summary('Arma +1', { id: 'magic', name: 'Arma +1', rarity: 'Incomum', magicBonus: 1, requiresBaseItem: true })])))
    request.mockResolvedValue(makeCharacter())
    const wrapper = mount(CatalogPicker, { props: { mode: 'items' } })
    await flushPromises()

    await wrapper.get('[data-add-catalog]').trigger('click')
    await flushPromises()
    expect(api.addItemFromCatalog).not.toHaveBeenCalled()
    expect(wrapper.get('[data-base-step]').text()).toContain('Escolha a arma base')
    expect(lastQuery(api.listCatalogItems)).toMatchObject({ mundane: true, category: 'Weapon' })

    await wrapper.get('[data-base-id="Machado de Batalha"] [data-choose-base]').trigger('click')
    await flushPromises()
    expect(request).toHaveBeenCalledTimes(1)
    const [path, init, token] = request.mock.calls[0]!
    expect(path).toBe('/campaigns/c1/participants/me/character/items/from-catalog')
    expect(init.method).toBe('POST')
    expect(JSON.parse(init.body)).toEqual({ catalogItemId: 'magic', quantity: 1, baseCatalogItemId: 'Machado de Batalha' })
    expect(token).toBe('tok')
    expect(wrapper.find('[data-base-step]').exists()).toBe(false)
    expect(wrapper.get('[data-catalog-status]').text()).toContain('Arma +1 (Machado de Batalha)')
  })

  it('armadura genérica pede a armadura base', async () => {
    api.listCatalogItems.mockImplementation((_session, params) => Promise.resolve(params.mundane
      ? itemPage([summary('Cota de Malha', { category: 'Armor' })])
      : itemPage([summary('Armadura +1', { category: 'Armor', requiresBaseItem: true })])))
    const wrapper = mount(CatalogPicker, { props: { mode: 'items' } })
    await flushPromises()
    await wrapper.get('[data-add-catalog]').trigger('click')
    await flushPromises()
    expect(wrapper.get('[data-base-step]').text()).toContain('Escolha a armadura base')
    expect(lastQuery(api.listCatalogItems)).toMatchObject({ mundane: true, category: 'Armor' })
    await wrapper.get('[data-base-back]').trigger('click')
    expect(wrapper.find('[data-catalog-search]').exists()).toBe(true)
  })

  it('mostra o crédito do SRD só no catálogo de itens', async () => {
    api.listCatalogItems.mockResolvedValue(itemPage([summary('espada')]))
    api.listCatalogSpells.mockResolvedValue({ ...spellPage([{ id: 's1', name: 'Bola de Fogo', level: 3, school: 'evocação', ritual: false, concentration: false, classes: ['mago'] }]), attribution: 'qualquer crédito' })
    const items = mount(CatalogPicker, { props: { mode: 'items' } })
    const spells = mount(CatalogPicker, { props: { mode: 'spells' } })
    await flushPromises()
    expect(items.get('[data-attribution]').text()).toBe(ATTRIBUTION)
    expect(spells.find('[data-attribution]').exists()).toBe(false)
    expect(spells.text()).not.toContain('crédito')
  })

  it('uma resposta lenta de uma busca antiga não substitui a mais nova', async () => {
    const slow = deferred<CatalogPage<CatalogItemSummary>>()
    const fast = deferred<CatalogPage<CatalogItemSummary>>()
    api.listCatalogItems
      .mockResolvedValueOnce(itemPage([]))
      .mockReturnValueOnce(slow.promise)
      .mockReturnValueOnce(fast.promise)
    const wrapper = mount(CatalogPicker, { props: { mode: 'items' } })
    await flushPromises()

    await wrapper.get('[data-catalog-search]').setValue('arco')
    await vi.advanceTimersByTimeAsync(300)
    await wrapper.get('[data-catalog-search]').setValue('arco longo')
    await vi.advanceTimersByTimeAsync(300)

    fast.resolve(itemPage([summary('Arco Longo')]))
    await flushPromises()
    slow.resolve(itemPage([summary('Arco Curto'), summary('Arco Longo')]))
    await flushPromises()

    expect(wrapper.findAll('[data-catalog-id]').map(x => x.attributes('data-catalog-id'))).toEqual(['Arco Longo'])
  })

  it('mostra carregando, vazio e erro', async () => {
    const pending = deferred<CatalogPage<CatalogItemSummary>>()
    api.listCatalogItems.mockReturnValueOnce(pending.promise)
    const wrapper = mount(CatalogPicker, { props: { mode: 'items' } })
    await flushPromises()
    expect(wrapper.find('[data-catalog-loading]').exists()).toBe(true)
    pending.resolve(itemPage([]))
    await flushPromises()
    expect(wrapper.get('[data-catalog-empty]').text()).toContain('Nada encontrado')

    api.listCatalogItems.mockRejectedValueOnce(new Error('Sem conexão.'))
    await wrapper.get('[data-filter="category"]').setValue('Potion')
    await flushPromises()
    expect(wrapper.get('[data-catalog-load-error]').text()).toContain('Sem conexão.')
  })

  it('Escape fecha o seletor', async () => {
    api.listCatalogItems.mockResolvedValue(itemPage([]))
    const wrapper = mount(CatalogPicker, { props: { mode: 'items' } })
    await flushPromises()
    await wrapper.get('.sheet-modal-overlay').trigger('keydown', { key: 'Escape' })
    expect(wrapper.emitted('close')).toHaveLength(1)
  })

  it('T078: o passo da base recebe o foco, Voltar e Esc devolvem ao passo 1 com o foco na busca', async () => {
    api.listCatalogItems.mockImplementation((_session, params) => Promise.resolve(params.mundane
      ? itemPage([summary('Espada Curta')])
      : itemPage([summary('Arma +1', { id: 'magic', name: 'Arma +1', requiresBaseItem: true })])))
    const wrapper = mount(CatalogPicker, { props: { mode: 'items' }, attachTo: document.body })
    await flushPromises()

    await wrapper.get('[data-add-catalog]').trigger('click')
    await flushPromises()
    expect(document.activeElement).toBe(wrapper.get('[data-base-search]').element)

    await wrapper.get('[data-base-back]').trigger('click')
    await flushPromises()
    expect(document.activeElement).toBe(wrapper.get('[data-catalog-search]').element)

    await wrapper.get('[data-add-catalog]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-base-search]').trigger('keydown', { key: 'Escape' })
    await flushPromises()
    expect(wrapper.emitted('close')).toBeUndefined()
    expect(wrapper.find('[data-base-step]').exists()).toBe(false)
    expect(document.activeElement).toBe(wrapper.get('[data-catalog-search]').element)
    wrapper.unmount()
  })

  it('T078: "Adicionar" e "Escolher" dizem o nome do item ao leitor de tela', async () => {
    api.listCatalogItems.mockImplementation((_session, params) => Promise.resolve(params.mundane
      ? itemPage([summary('Espada Curta')])
      : itemPage([summary('Espada Longa'), summary('Arma +1', { id: 'magic', name: 'Arma +1', requiresBaseItem: true })])))
    const wrapper = mount(CatalogPicker, { props: { mode: 'items' } })
    await flushPromises()
    expect(wrapper.findAll('[data-add-catalog]').map(button => button.attributes('aria-label'))).toEqual(['Adicionar Espada Longa', 'Adicionar Arma +1'])
    await wrapper.get('[data-catalog-id="magic"] [data-add-catalog]').trigger('click')
    await flushPromises()
    expect(wrapper.get('[data-choose-base]').attributes('aria-label')).toBe('Escolher Espada Curta')
  })
})

describe('Catálogo de magias', () => {
  const fireball: CatalogSpellSummary = { id: 's1', name: 'Bola de Fogo', level: 3, school: 'evocação', ritual: false, concentration: false, classes: ['mago'] }

  it('manda círculo, classe, ritual e concentração na consulta', async () => {
    api.listCatalogSpells.mockResolvedValue(spellPage([fireball]))
    const wrapper = mount(CatalogPicker, { props: { mode: 'spells' } })
    await flushPromises()
    expect(wrapper.findAll('[data-filter="level"] option').map(x => x.text()).slice(1, 3)).toEqual(['Truque', '1º círculo'])
    await wrapper.get('[data-filter="level"]').setValue('0')
    await wrapper.get('[data-filter="class"]').setValue('clérigo')
    await wrapper.get('[data-filter="ritual"]').trigger('click')
    await wrapper.get('[data-filter="concentration"]').trigger('click')
    await flushPromises()
    expect(lastQuery(api.listCatalogSpells)).toMatchObject({ level: 0, class: 'clérigo', ritual: true, concentration: true, page: 1 })
    await wrapper.get('[data-filter="ritual"]').trigger('click')
    await flushPromises()
    expect(lastQuery(api.listCatalogSpells).ritual).toBeNull()
  })

  it('adiciona a magia e mostra a mensagem do servidor quando ela já está na ficha', async () => {
    api.listCatalogSpells.mockResolvedValue(spellPage([fireball]))
    api.addSpellFromCatalog
      .mockResolvedValueOnce(makeCharacter())
      .mockRejectedValueOnce(Object.assign(new Error('Bola de Fogo já está na sua ficha.'), { status: 409 }))
    const wrapper = mount(CatalogPicker, { props: { mode: 'spells' } })
    await flushPromises()
    expect(wrapper.get('[data-catalog-id="s1"]').text()).toContain('3º círculo, evocação')

    await wrapper.get('[data-add-catalog]').trigger('click')
    await flushPromises()
    expect(api.addSpellFromCatalog).toHaveBeenCalledWith(expect.anything(), 's1')
    expect(wrapper.get('[data-catalog-status]').text()).toContain('Bola de Fogo entrou na sua lista de magias.')

    await wrapper.get('[data-add-catalog]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-catalog-status]').exists()).toBe(false)
    expect(wrapper.get('[data-catalog-error]').text()).toBe('Bola de Fogo já está na sua ficha.')
  })
})
