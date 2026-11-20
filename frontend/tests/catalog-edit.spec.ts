import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useCampaignStore } from '~/stores/campaign'
import { useCharacterStore } from '~/stores/character'
import CatalogPicker from '~/components/character/CatalogPicker.vue'
import type { CatalogItem, CatalogPage, CatalogSpell, CatalogSpellSummary, CharacterSpellData, InventoryItemData } from '~/types/character'

// Spec 013, US1: the master creates and edits catalog entries in the Compêndio. Editing a compendium entry
// sends the global id (the server creates the table's version); deleting an edited one restores the original.
const api = {
  listCatalogItems: vi.fn(), listCatalogSpells: vi.fn(), getCatalogItem: vi.fn(), getCatalogSpell: vi.fn(),
  createCatalogItem: vi.fn(), saveCatalogItem: vi.fn(), deleteCatalogItem: vi.fn(),
  createCatalogSpell: vi.fn(), saveCatalogSpell: vi.fn(), deleteCatalogSpell: vi.fn()
}
vi.stubGlobal('useCharacterApi', () => api)
vi.stubGlobal('useCompanionApi', () => ({ request: vi.fn() }))
vi.stubGlobal('useCampaignStore', () => useCampaignStore())
vi.stubGlobal('useCharacterStore', () => useCharacterStore())

const spellTemplate = (dice = '8d6'): CharacterSpellData => ({
  name: 'Bola de Fogo', level: 3, school: 'evocação', castingTime: '1 ação', range: '45 metros', components: 'V, S, M',
  duration: 'instantânea', ritual: false, concentration: false, description: 'Explode.', attackKind: 'Save', saveAbility: 'Dex',
  damageDice: dice, damageType: 'fogo', addAbilityToDamage: false
})
const spell = (id: string, origin: CatalogSpell['origin'], dice = '8d6', baseId: string | null = null): CatalogSpell =>
  ({ id, name: 'Bola de Fogo', nameEn: 'Fireball', level: 3, classes: ['mago'], higherLevels: null, template: spellTemplate(dice), origin, baseId })
const spellSummary = (id: string, origin: CatalogSpellSummary['origin']): CatalogSpellSummary =>
  ({ id, name: 'Bola de Fogo', nameEn: 'Fireball', level: 3, school: 'evocação', ritual: false, concentration: false, classes: ['mago'], origin })
const page = <T>(items: T[]): CatalogPage<T> => ({ items, total: items.length, page: 1, pageSize: 20, attribution: null })

const plate: InventoryItemData = {
  name: 'Corpo Fênix', category: 'Armor', quantity: 1, weight: 32.5, description: null, notes: null, equipped: false, proficient: false,
  magicBonus: 1, weapon: null, armor: { baseAc: 18, type: 'Heavy', strengthMin: 15, stealthDisadvantage: true }, shield: null, effects: []
}

beforeEach(() => {
  setActivePinia(createPinia())
  useCampaignStore().session = { campaignId: 'c1', playerCode: '1234', masterCode: '9999', participantId: 'm1', displayName: 'Mestre', role: 'Master', participantToken: 'tok' } as never
  for (const fn of Object.values(api)) fn.mockReset()
})

async function openDetail(wrapper: ReturnType<typeof mount>, id: string) {
  await wrapper.get(`[data-catalog-id="${id}"] [data-catalog-detail-toggle]`).trigger('click')
  await flushPromises()
}

describe('Compêndio editável pelo mestre', () => {
  it('sem editable não mostra Novo, filtro de origem nem ações', async () => {
    api.listCatalogSpells.mockResolvedValue(page([spellSummary('g1', 'Compendium')]))
    api.getCatalogSpell.mockResolvedValue(spell('g1', 'Compendium'))
    const wrapper = mount(CatalogPicker, { props: { mode: 'spells', browse: true } })
    await flushPromises()
    expect(wrapper.find('[data-catalog-new]').exists()).toBe(false)
    expect(wrapper.find('[data-filter="origin"]').exists()).toBe(false)
    await openDetail(wrapper, 'g1')
    expect(wrapper.find('[data-catalog-master-actions]').exists()).toBe(false)
  })

  it('mostra o selo de origem e manda o filtro de origem', async () => {
    api.listCatalogSpells.mockResolvedValue(page([spellSummary('e1', 'Edited'), spellSummary('c1', 'Custom'), spellSummary('g1', 'Compendium')]))
    const wrapper = mount(CatalogPicker, { props: { mode: 'spells', browse: true, editable: true } })
    await flushPromises()
    expect(wrapper.get('[data-catalog-id="e1"] .origin-badge').text()).toBe('Editada')
    expect(wrapper.get('[data-catalog-id="c1"] .origin-badge').text()).toBe('Personalizada')
    expect(wrapper.find('[data-catalog-id="g1"] .origin-badge').exists()).toBe(false)
    await wrapper.get('[data-filter="origin"]').setValue('Edited')
    await flushPromises()
    expect(api.listCatalogSpells.mock.calls.at(-1)![1]).toMatchObject({ origin: 'Edited', page: 1 })
  })

  it('editar uma magia do compêndio manda o id global e abre a versão da mesa', async () => {
    api.listCatalogSpells.mockResolvedValue(page([spellSummary('g1', 'Compendium')]))
    api.getCatalogSpell.mockResolvedValue(spell('g1', 'Compendium'))
    api.saveCatalogSpell.mockResolvedValue(spell('copy1', 'Edited', '10d6', 'g1'))
    const wrapper = mount(CatalogPicker, { props: { mode: 'spells', browse: true, editable: true }, attachTo: document.body })
    await flushPromises()
    await openDetail(wrapper, 'g1')
    expect(wrapper.get('[data-catalog-master-actions]').text()).not.toContain('Excluir')

    await wrapper.get('[data-catalog-edit]').trigger('click')
    const editor = wrapper.get('[data-spell-editor]')
    expect(editor.find('[data-catalog-copy-hint]').exists()).toBe(true)
    expect(editor.get('#spell-editor-title').text()).toBe('Editar magia do compêndio')
    await editor.get('[data-spell-field="damageDice"]').setValue('10d6')
    api.listCatalogSpells.mockResolvedValue(page([spellSummary('copy1', 'Edited')]))
    await editor.trigger('submit')
    await flushPromises()

    expect(api.saveCatalogSpell).toHaveBeenCalledWith(expect.anything(), 'g1', expect.objectContaining({
      template: expect.objectContaining({ damageDice: '10d6' }), classes: ['mago'], nameEn: 'Fireball'
    }))
    expect(wrapper.find('[data-spell-editor]').exists()).toBe(false)
    expect(wrapper.get('[data-catalog-status]').text()).toContain('Salvo na versão da mesa')
    // The copy's row is open with the saved details, offering to restore the original.
    expect(wrapper.get('[data-catalog-id="copy1"] [data-catalog-delete]').text()).toContain('Restaurar original')
    // The "Editar" that opened the editor was on the global row, now gone: focus lands on the copy's "Editar".
    expect(document.activeElement).toBe(wrapper.get('[data-catalog-id="copy1"] [data-catalog-edit]').element)
    wrapper.unmount()
  })

  it('restaurar o original pede confirmação e reabre a original', async () => {
    api.listCatalogSpells.mockResolvedValue(page([spellSummary('copy1', 'Edited')]))
    api.getCatalogSpell.mockResolvedValue(spell('copy1', 'Edited', '10d6', 'g1'))
    api.deleteCatalogSpell.mockResolvedValue(spell('g1', 'Compendium'))
    const wrapper = mount(CatalogPicker, { props: { mode: 'spells', browse: true, editable: true } })
    await flushPromises()
    await openDetail(wrapper, 'copy1')

    await wrapper.get('[data-catalog-delete]').trigger('click')
    expect(api.deleteCatalogSpell).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Voltar ao original do compêndio?')
    api.listCatalogSpells.mockResolvedValue(page([spellSummary('g1', 'Compendium')]))
    await wrapper.get('[data-catalog-delete]').trigger('click')
    await flushPromises()
    expect(api.deleteCatalogSpell).toHaveBeenCalledWith(expect.anything(), 'copy1')
    expect(wrapper.get('[data-catalog-status]').text()).toContain('voltou ao original')
    expect(wrapper.find('[data-catalog-id="g1"] [data-catalog-detail]').exists()).toBe(true)
  })

  it('editada cujo original saiu do compêndio oferece Excluir, não Restaurar (achado do crítico)', async () => {
    api.listCatalogSpells.mockResolvedValue(page([spellSummary('copy1', 'Edited')]))
    api.getCatalogSpell.mockResolvedValue({ ...spell('copy1', 'Edited', '10d6', 'g1'), baseRemoved: true })
    const wrapper = mount(CatalogPicker, { props: { mode: 'spells', browse: true, editable: true } })
    await flushPromises()
    await openDetail(wrapper, 'copy1')
    expect(wrapper.get('[data-catalog-delete]').text()).toBe('Excluir')
    await wrapper.get('[data-catalog-delete]').trigger('click')
    expect(wrapper.text()).toContain('Excluir do compêndio da mesa?')
    expect(wrapper.text()).not.toContain('Voltar ao original')
  })

  it('cria um item com os campos do compêndio e item genérico sem bloco de arma', async () => {
    api.listCatalogItems.mockResolvedValue({ ...page([]), attribution: null })
    api.createCatalogItem.mockResolvedValue({
      id: 'n1', name: 'Corpo Fênix', nameEn: 'Phoenix Plate', category: 'Armor', subcategory: null, rarity: 'Raro',
      requiresAttunement: true, cost: '5000 PO', requiresBaseItem: false, template: plate, origin: 'Custom', baseId: null
    } satisfies CatalogItem)
    const wrapper = mount(CatalogPicker, { props: { mode: 'items', browse: true, editable: true }, attachTo: document.body })
    await flushPromises()
    await wrapper.get('[data-catalog-new]').trigger('click')
    const editor = wrapper.get('[data-item-editor]')
    expect(editor.get('#item-editor-title').text()).toBe('Novo item do compêndio')
    await editor.get('[data-item-field="name"]').setValue('Corpo Fênix')
    await editor.get('[data-item-field="category"]').setValue('Armor')
    await editor.get('[data-item-field="baseAc"]').setValue('18')
    await editor.get('[data-item-field="armorType"]').setValue('Heavy')
    await editor.get('[data-item-field="nameEn"]').setValue('Phoenix Plate')
    await editor.get('[data-item-field="rarity"]').setValue('Raro')
    await editor.get('[data-item-field="cost"]').setValue('5000 PO')
    await editor.get('[data-item-field="requiresAttunement"]').setValue(true)
    // The sheet's own proficiency box does not belong to a catalog entry.
    expect(editor.find('[data-item-field="proficient"]').exists()).toBe(false)
    await editor.trigger('submit')
    await flushPromises()
    expect(api.createCatalogItem).toHaveBeenCalledWith(expect.anything(), expect.objectContaining({
      nameEn: 'Phoenix Plate', rarity: 'Raro', cost: '5000 PO', requiresAttunement: true, subcategory: null,
      template: expect.objectContaining({ name: 'Corpo Fênix', category: 'Armor', armor: expect.objectContaining({ baseAc: 18, type: 'Heavy' }), proficient: false, equipped: false })
    }))
    expect(wrapper.get('[data-catalog-status]').text()).toContain('Criado no compêndio da mesa: Corpo Fênix')
    wrapper.unmount()
  })

  it('item genérico manda arma nula e não exige dano', async () => {
    api.listCatalogItems.mockResolvedValue({ ...page([]), attribution: null })
    api.createCatalogItem.mockResolvedValue({
      id: 'n2', name: 'Arma da Mesa +2', nameEn: null, category: 'Weapon', subcategory: null, rarity: 'Raro', requiresAttunement: false,
      cost: null, requiresBaseItem: true, template: { ...plate, category: 'Weapon', armor: null, magicBonus: 2 }, origin: 'Custom', baseId: null
    } satisfies CatalogItem)
    const wrapper = mount(CatalogPicker, { props: { mode: 'items', browse: true, editable: true }, attachTo: document.body })
    await flushPromises()
    await wrapper.get('[data-catalog-new]').trigger('click')
    const editor = wrapper.get('[data-item-editor]')
    await editor.get('[data-item-field="name"]').setValue('Arma da Mesa +2')
    await editor.get('[data-item-field="category"]').setValue('Weapon')
    await editor.get('[data-item-field="generic"]').setValue(true)
    expect(editor.find('[data-block="weapon"]').exists()).toBe(false)
    await editor.trigger('submit')
    await flushPromises()
    expect(api.createCatalogItem).toHaveBeenCalledWith(expect.anything(), expect.objectContaining({
      template: expect.objectContaining({ category: 'Weapon', weapon: null })
    }))
    wrapper.unmount()
  })

  it('erro do servidor fica no editor e nada fecha', async () => {
    api.listCatalogSpells.mockResolvedValue(page([]))
    api.createCatalogSpell.mockRejectedValue(new Error('O nome da magia deve ter entre 1 e 80 caracteres.'))
    const wrapper = mount(CatalogPicker, { props: { mode: 'spells', browse: true, editable: true }, attachTo: document.body })
    await flushPromises()
    await wrapper.get('[data-catalog-new]').trigger('click')
    const editor = wrapper.get('[data-spell-editor]')
    await editor.get('[data-spell-field="name"]').setValue('Nova')
    await editor.get('[data-spell-class="paladino"]').trigger('click')
    await editor.trigger('submit')
    await flushPromises()
    expect(api.createCatalogSpell).toHaveBeenCalledWith(expect.anything(), expect.objectContaining({ classes: ['paladino'] }))
    expect(wrapper.get('[data-spell-error]').text()).toContain('nome da magia')
    expect(wrapper.find('[data-spell-editor]').exists()).toBe(true)
    wrapper.unmount()
  })
})
