import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useCampaignStore } from '~/stores/campaign'
import { useCharacterStore } from '~/stores/character'
import InventoryPanel from '~/components/character/InventoryPanel.vue'
import ItemEditorModal from '~/components/character/ItemEditorModal.vue'
import type { Character, InventoryItem, InventoryItemData } from '~/types/character'
import { makeCharacter } from './character-fixture'

// Spec 011, US3: the bag lists items by category, toggles equipment and proficiency through the
// server, flags repeated effects and edits items with category blocks and an effects editor.
const api = {
  getCharacter: vi.fn(), equipItem: vi.fn(), saveItem: vi.fn(), addItem: vi.fn(), removeItem: vi.fn(),
  listCatalogItems: vi.fn(), addItemFromCatalog: vi.fn()
}
vi.stubGlobal('useCharacterApi', () => api)
vi.stubGlobal('useCompanionApi', () => ({ request: vi.fn() }))
vi.stubGlobal('useCampaignStore', () => useCampaignStore())
vi.stubGlobal('useCharacterStore', () => useCharacterStore())

function item(id: string, data: Partial<InventoryItemData>, sortOrder = 0): InventoryItem {
  return {
    id, sortOrder, catalogItemId: null, version: `v-${id}`,
    data: {
      name: id, category: 'Gear', quantity: 1, weight: null, description: null, notes: null, equipped: false, proficient: false,
      magicBonus: 0, weapon: null, armor: null, shield: null, effects: [], ...data
    }
  }
}

const longsword = item('sword', {
  name: 'Espada Longa', category: 'Weapon', proficient: true, magicBonus: 1, weight: 1.5,
  weapon: { damageDice: '1d8', damageType: 'cortante', versatileDice: '1d10', kind: 'Melee', class: 'Martial', properties: ['versatile'], rangeNormal: null, rangeLong: null, abilityOverride: null, mastery: null }
})
const chain = item('chain', { name: 'Cota de Malha', category: 'Armor', equipped: true, armor: { baseAc: 16, type: 'Heavy', strengthMin: 13, stealthDisadvantage: true } })
const ring1 = item('ring1', { name: 'Anel de Proteção', category: 'Misc', effects: [{ type: 'ArmorClass', target: null, op: 'Add', value: 1 }] }, 1)
const ring2 = item('ring2', { name: 'Anel de Proteção', category: 'Misc', effects: [{ type: 'ArmorClass', target: null, op: 'Add', value: 1 }] }, 2)
const potion = item('potion', { name: 'Poção de Cura', category: 'Potion', quantity: 3 })

function loaded(character: Character = makeCharacter({ items: [longsword, chain, ring1, ring2, potion] })) {
  setActivePinia(createPinia())
  useCampaignStore().session = { campaignId: 'c1', playerCode: '1234', masterCode: null, participantId: 'p1', displayName: 'Loren', role: 'Player', participantToken: 't' } as never
  useCharacterStore().apply(character, { resetDraft: true })
  return character
}

beforeEach(() => {
  for (const fn of Object.values(api)) fn.mockReset()
  api.listCatalogItems.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 20, attribution: null })
})

const mountPanel = () => mount(InventoryPanel, { attachTo: document.body })

describe('Mochila', () => {
  it('agrupa os itens por categoria, na ordem fixa, com números curtos', () => {
    loaded()
    const wrapper = mountPanel()
    expect(wrapper.find('[data-inventory]').exists()).toBe(true)
    expect(wrapper.findAll('[data-category]').map(x => x.attributes('data-category'))).toEqual(['Weapon', 'Armor', 'Misc', 'Potion'])
    const sword = wrapper.get('[data-item-id="sword"]').text()
    expect(sword).toContain('1d8 cortante')
    expect(sword).toContain('+1')
    expect(wrapper.get('[data-item-id="chain"]').text()).toContain('CA 16, armadura pesada')
    expect(wrapper.get('[data-item-id="potion"]').text()).toContain('×3')
    // Potions and misc items are neither equipped nor proficient.
    expect(wrapper.find('[data-item-id="potion"] [data-equip]').exists()).toBe(false)
    expect(wrapper.find('[data-item-id="ring1"] [data-proficient]').exists()).toBe(false)
    expect(wrapper.get('[data-item-id="chain"] [data-equip]').attributes('aria-pressed')).toBe('true')
    wrapper.unmount()
  })

  it('mostra o estado vazio', () => {
    loaded(makeCharacter())
    const wrapper = mountPanel()
    expect(wrapper.get('[data-inventory-empty]').text()).toContain('A mochila está vazia.')
    wrapper.unmount()
  })

  it('equipar chama o servidor e mostra o aviso de quem saiu', async () => {
    const before = loaded()
    const plate = item('plate', { name: 'Placas', category: 'Armor', armor: { baseAc: 18, type: 'Heavy', strengthMin: 15, stealthDisadvantage: true } })
    before.items.push(plate)
    useCharacterStore().apply(before)
    const after = makeCharacter({
      items: [longsword, { ...chain, data: { ...chain.data, equipped: false } }, { ...plate, data: { ...plate.data, equipped: true } }],
      notices: ['Cota de Malha foi desequipado.']
    })
    api.equipItem.mockResolvedValue(after)
    const wrapper = mountPanel()

    await wrapper.get('[data-item-id="plate"] [data-equip]').trigger('click')
    await flushPromises()

    expect(api.equipItem).toHaveBeenCalledWith(expect.anything(), 'plate', true)
    expect(wrapper.get('[data-item-id="plate"] [data-equip]').attributes('aria-pressed')).toBe('true')
    expect(wrapper.get('[data-item-id="chain"] [data-equip]').attributes('aria-pressed')).toBe('false')
    expect(wrapper.get('[data-inventory-notices]').text()).toContain('Cota de Malha foi desequipado.')
    wrapper.unmount()
  })

  it('dentro da tela Ficha não repete os avisos que a tela já mostra', async () => {
    loaded(makeCharacter({ items: [chain], notices: ['Cota de Malha foi desequipado.'] }))
    const host = document.createElement('div')
    host.setAttribute('data-character-view', '')
    document.body.appendChild(host)
    const wrapper = mount(InventoryPanel, { attachTo: host })
    await flushPromises()
    expect(wrapper.find('[data-inventory-notices]').exists()).toBe(false)
    wrapper.unmount()
    host.remove()
  })

  it('alternar proficiência grava o item inteiro com a versão dele', async () => {
    loaded()
    api.saveItem.mockResolvedValue(makeCharacter({ items: [{ ...longsword, data: { ...longsword.data, proficient: false } }] }))
    const wrapper = mountPanel()
    const toggle = wrapper.get('[data-item-id="sword"] [data-proficient]')
    expect(toggle.attributes('aria-pressed')).toBe('true')

    await toggle.trigger('click')
    await flushPromises()

    expect(api.saveItem).toHaveBeenCalledTimes(1)
    const [, id, data, version] = api.saveItem.mock.calls[0]!
    expect(id).toBe('sword')
    expect(version).toBe('v-sword')
    expect(data.proficient).toBe(false)
    expect(data.weapon.damageDice).toBe('1d8')
    expect(wrapper.get('[data-item-id="sword"] [data-proficient]').attributes('aria-pressed')).toBe('false')
    wrapper.unmount()
  })

  it('marca o efeito repetido que não acumula', () => {
    const character = loaded()
    character.stats.suppressedEffects = [{ itemId: 'ring2', itemName: 'Anel de Proteção', effectKey: 'k', keptItemId: 'ring1', keptItemName: 'Anel de Proteção' }]
    useCharacterStore().apply(character)
    const wrapper = mountPanel()
    expect(wrapper.find('[data-item-id="ring1"] [data-suppressed]').exists()).toBe(false)
    expect(wrapper.get('[data-item-id="ring2"] [data-suppressed]').text()).toContain('Efeito repetido: não acumula')
    wrapper.unmount()
  })

  it('remover pede confirmação antes de chamar o servidor', async () => {
    loaded()
    api.removeItem.mockResolvedValue(makeCharacter({ items: [longsword] }))
    const wrapper = mountPanel()

    await wrapper.get('[data-item-id="potion"] [data-remove-item]').trigger('click')
    expect(api.removeItem).not.toHaveBeenCalled()
    expect(wrapper.get('[data-item-id="potion"]').text()).toContain('Remover?')

    await wrapper.get('[data-item-id="potion"] [data-cancel-remove]').trigger('click')
    expect(wrapper.find('[data-item-id="potion"] [data-confirm-remove]').exists()).toBe(false)

    await wrapper.get('[data-item-id="potion"] [data-remove-item]').trigger('click')
    await wrapper.get('[data-item-id="potion"] [data-confirm-remove]').trigger('click')
    await flushPromises()
    expect(api.removeItem).toHaveBeenCalledWith(expect.anything(), 'potion')
    expect(wrapper.find('[data-item-id="potion"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('"Do catálogo" abre o seletor de itens', async () => {
    loaded()
    const wrapper = mountPanel()
    await wrapper.get('[data-open-catalog]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-catalog-picker="items"]').exists()).toBe(true)
    wrapper.unmount()
  })
})

describe('Editor de item', () => {
  async function newWeapon(wrapper: ReturnType<typeof mount>) {
    await wrapper.get('[data-new-item]').trigger('click')
    const editor = wrapper.getComponent(ItemEditorModal)
    await editor.get('[data-item-field="name"]').setValue('Machado')
    await editor.get('[data-item-field="category"]').setValue('Weapon')
    return editor
  }

  it('abre com foco no nome, como diálogo, e fecha com Escape', async () => {
    loaded()
    const wrapper = mountPanel()
    await wrapper.get('[data-new-item]').trigger('click')
    await flushPromises()
    const dialog = wrapper.get('[data-item-editor]')
    expect(dialog.attributes('role')).toBe('dialog')
    expect(dialog.attributes('aria-modal')).toBe('true')
    expect(document.activeElement).toBe(wrapper.get('[data-item-field="name"]').element)
    await wrapper.get('.sheet-modal-overlay').trigger('keydown', { key: 'Escape' })
    expect(wrapper.find('[data-item-editor]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('exige o dado de dano da arma antes de chamar o servidor', async () => {
    loaded()
    const wrapper = mountPanel()
    const editor = await newWeapon(wrapper)
    await editor.get('[data-item-editor]').trigger('submit')
    expect(api.addItem).not.toHaveBeenCalled()
    expect(editor.get('[data-item-error]').text()).toContain('1d8')

    await editor.get('[data-item-field="damageDice"]').setValue('2d7')
    await editor.get('[data-item-editor]').trigger('submit')
    expect(api.addItem).not.toHaveBeenCalled()

    api.addItem.mockResolvedValue(makeCharacter())
    await editor.get('[data-item-field="damageDice"]').setValue(' 1D12 ')
    await editor.get('[data-item-field="damageType"]').setValue('cortante')
    await editor.get('[data-property="heavy"]').trigger('click')
    await editor.get('[data-item-editor]').trigger('submit')
    await flushPromises()
    expect(api.addItem).toHaveBeenCalledTimes(1)
    const data = api.addItem.mock.calls[0]![1] as InventoryItemData
    expect(data.name).toBe('Machado')
    expect(data.weapon).toMatchObject({ damageDice: '1d12', damageType: 'cortante', properties: ['heavy'] })
    expect(data.armor).toBeNull()
    expect(data.shield).toBeNull()
    expect(wrapper.find('[data-item-editor]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('arma nova já vem proficiente conforme as proficiências gerais da ficha', async () => {
    const character = makeCharacter()
    character.sheet.generalProficiencies = { armor: ['Light'], shields: true, simpleWeapons: true, martialWeapons: false, other: null }
    loaded(character)
    const wrapper = mountPanel()
    const editor = await newWeapon(wrapper)
    const proficient = () => (editor.get('[data-item-field="proficient"]').element as HTMLInputElement).checked

    expect(proficient()).toBe(true)
    await editor.get('[data-item-field="weaponClass"]').setValue('Martial')
    expect(proficient()).toBe(false)
    await editor.get('[data-item-field="category"]').setValue('Shield')
    expect(proficient()).toBe(true)
    expect((editor.get('[data-item-field="acBonus"]').element as HTMLInputElement).value).toBe('2')
    await editor.get('[data-item-field="category"]').setValue('Armor')
    await editor.get('[data-item-field="armorType"]').setValue('Heavy')
    expect(proficient()).toBe(false)
    wrapper.unmount()
  })

  it('a escolha manual de proficiência não é desfeita ao trocar de classe', async () => {
    loaded()
    const wrapper = mountPanel()
    const editor = await newWeapon(wrapper)
    await editor.get('[data-item-field="proficient"]').setValue(true)
    await editor.get('[data-item-field="weaponClass"]').setValue('Martial')
    expect((editor.get('[data-item-field="proficient"]').element as HTMLInputElement).checked).toBe(true)
    wrapper.unmount()
  })

  it('cada tipo de efeito mostra os alvos dele e esconde o valor nos textuais', async () => {
    loaded()
    const wrapper = mountPanel()
    await wrapper.get('[data-new-item]').trigger('click')
    await wrapper.get('[data-add-effect]').trigger('click')
    const row = () => wrapper.get('[data-effect-row="0"]')
    const options = (selector: string) => row().findAll(`${selector} option`).map(x => x.text())

    // Classe de Armadura: no target, both operations, a value.
    expect(row().find('[data-effect-target]').exists()).toBe(false)
    expect(options('[data-effect-op]')).toEqual(['Soma', 'Pelo menos'])
    expect(row().find('[data-effect-value]').exists()).toBe(true)

    await row().get('[data-effect-type]').setValue('Ability')
    expect(options('[data-effect-target]')).toEqual(['FOR', 'DES', 'CON', 'INT', 'SAB', 'CAR'])

    await row().get('[data-effect-type]').setValue('Speed')
    expect(options('[data-effect-target]')).toEqual(['andar', 'voo', 'natação', 'escalada'])

    await row().get('[data-effect-type]').setValue('Vision')
    expect(options('[data-effect-target]')).toEqual(['visão no escuro', 'percepção às cegas', 'sentido sísmico', 'visão verdadeira'])

    await row().get('[data-effect-type]').setValue('AttackBonus')
    expect(options('[data-effect-target]')).toEqual(['todos', 'corpo a corpo', 'à distância', 'magia'])
    expect(row().find('[data-effect-op]').exists()).toBe(false)

    await row().get('[data-effect-type]').setValue('SkillBonus')
    expect(options('[data-effect-target]')[0]).toBe('todas')
    expect(options('[data-effect-target]')).toContain('Furtividade')

    await row().get('[data-effect-type]').setValue('Resistance')
    expect(row().get('[data-effect-target]').element.tagName).toBe('INPUT')
    expect(row().find('[data-effect-value]').exists()).toBe(false)
    expect(row().find('[data-effect-op]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('grava efeitos no formato do servidor: textual sem valor, alvo só quando existe', async () => {
    loaded()
    api.addItem.mockResolvedValue(makeCharacter())
    const wrapper = mountPanel()
    await wrapper.get('[data-new-item]').trigger('click')
    await wrapper.get('[data-item-field="name"]').setValue('Manto')
    await wrapper.get('[data-add-effect]').trigger('click')
    await wrapper.get('[data-add-effect]').trigger('click')
    await wrapper.get('[data-effect-row="0"] [data-effect-value]').setValue('1')
    await wrapper.get('[data-effect-row="1"] [data-effect-type]').setValue('Resistance')
    await wrapper.get('[data-item-editor]').trigger('submit')
    expect(api.addItem).not.toHaveBeenCalled()
    expect(wrapper.get('[data-item-error]').text()).toContain('Resistência')

    await wrapper.get('[data-effect-row="1"] [data-effect-target]').setValue('fogo')
    await wrapper.get('[data-item-editor]').trigger('submit')
    await flushPromises()
    expect(api.addItem.mock.calls[0]![1].effects).toEqual([
      { type: 'ArmorClass', target: null, op: 'Add', value: 1 },
      { type: 'Resistance', target: 'fogo', op: 'Add', value: null }
    ])
    wrapper.unmount()
  })

  it('edita com a versão do item e mostra o erro de validação do servidor', async () => {
    loaded()
    api.saveItem.mockRejectedValue(Object.assign(new Error('O peso deve ficar entre 0 e 99999 kg.'), { status: 400 }))
    const wrapper = mountPanel()
    await wrapper.get('[data-item-id="chain"] [data-edit-item]').trigger('click')
    expect((wrapper.get('[data-item-field="baseAc"]').element as HTMLInputElement).value).toBe('16')
    await wrapper.get('[data-item-editor]').trigger('submit')
    await flushPromises()
    expect(api.saveItem.mock.calls[0]![1]).toBe('chain')
    expect(api.saveItem.mock.calls[0]![3]).toBe('v-chain')
    expect(wrapper.get('[data-item-error]').text()).toBe('O peso deve ficar entre 0 e 99999 kg.')
    expect(wrapper.find('[data-item-editor]').exists()).toBe(true)
    wrapper.unmount()
  })

  it('limita a 20 efeitos', async () => {
    loaded()
    const wrapper = mountPanel()
    await wrapper.get('[data-new-item]').trigger('click')
    for (let i = 0; i < 22; i++) await wrapper.get('[data-add-effect]').trigger('click')
    expect(wrapper.findAll('[data-effect-row]')).toHaveLength(20)
    expect(wrapper.get('[data-add-effect]').attributes('disabled')).toBeDefined()
    wrapper.unmount()
  })
})
