import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useCampaignStore } from '~/stores/campaign'
import { useCharacterStore } from '~/stores/character'
import SpellList from '~/components/character/SpellList.vue'
import type { Character, CharacterSpell, CharacterSpellData } from '~/types/character'
import { makeCharacter } from './character-fixture'

// Spec 011, US7: the "Magias" tab groups spells by level, shows DC and spell attack from the
// server's stats, renders descriptions as text, and edits or removes spells through the store.
const api = { addSpell: vi.fn(), saveSpell: vi.fn(), removeSpell: vi.fn(), listCatalogSpells: vi.fn(), addSpellFromCatalog: vi.fn() }
vi.stubGlobal('useCharacterApi', () => api)
vi.stubGlobal('useCompanionApi', () => ({ request: vi.fn() }))
vi.stubGlobal('useCampaignStore', () => useCampaignStore())
vi.stubGlobal('useCharacterStore', () => useCharacterStore())

function spell(id: string, level: number, data: Partial<CharacterSpellData> = {}, sortOrder = 0): CharacterSpell {
  return {
    id, sortOrder, catalogSpellId: null,
    data: {
      name: id, level, school: null, castingTime: '1 ação', range: '18 metros', components: 'V, S', duration: 'instantânea',
      ritual: false, concentration: false, description: null, attackKind: 'None', saveAbility: null, damageDice: null, damageType: null,
      addAbilityToDamage: false, ...data
    }
  }
}

function loaded(character: Character) {
  setActivePinia(createPinia())
  useCampaignStore().session = { campaignId: 'c1', playerCode: '1234', masterCode: null, participantId: 'p1', displayName: 'Loren', role: 'Player', participantToken: 't' } as never
  useCharacterStore().apply(character, { resetDraft: true })
}

beforeEach(() => {
  for (const fn of Object.values(api)) fn.mockReset()
})

const book = () => makeCharacter({
  spells: [
    spell('Bola de Fogo', 3, { school: 'evocação', damageDice: '8d6', damageType: 'fogo', attackKind: 'Save', saveAbility: 'Dex' }),
    spell('Raio de Fogo', 0, { school: 'evocação', damageDice: '1d10', damageType: 'fogo', attackKind: 'Attack' }),
    spell('Escudo Arcano', 1, { school: 'abjuração', castingTime: '1 reação' }),
    spell('Detectar Magia', 1, { school: 'adivinhação', ritual: true, concentration: true })
  ]
})

describe('Aba Magias', () => {
  it('agrupa por círculo, com truques primeiro', () => {
    loaded(book())
    const wrapper = mount(SpellList)
    expect(wrapper.findAll('[data-spell-level] h3').map(x => x.text().replace(/\s+\d+$/, ''))).toEqual(['Truques', '1º círculo', '3º círculo'])
    expect(wrapper.findAll('[data-spell-level="1"] [data-spell-id]').map(x => x.attributes('data-spell-id'))).toEqual(['Detectar Magia', 'Escudo Arcano'])
    const fireball = wrapper.get('[data-spell-id="Bola de Fogo"]')
    expect(fireball.get('[data-spell-roll]').text()).toBe('salvaguarda de DES, 8d6 fogo')
    expect(fireball.text()).toContain('evocação')
    const detect = wrapper.get('[data-spell-id="Detectar Magia"]')
    expect(detect.find('[data-badge="ritual"]').exists()).toBe(true)
    expect(detect.find('[data-badge="concentration"]').exists()).toBe(true)
  })

  it('CD e ataque mágico vêm do stats do servidor', () => {
    const character = book()
    character.stats.spellSaveDc = { value: 15, parts: [{ label: 'base', value: 8, source: 'base', itemId: null }] }
    character.stats.spellAttackBonus = { value: 7, parts: [] }
    loaded(character)
    const wrapper = mount(SpellList)
    expect(wrapper.get('[data-spell-dc]').text()).toContain('15')
    expect(wrapper.get('[data-spell-attack]').text()).toContain('+7')
    expect(wrapper.find('[data-spell-casting-hint]').exists()).toBe(false)
  })

  it('sem atributo de conjuração, explica onde escolher', () => {
    loaded(book())
    const wrapper = mount(SpellList)
    expect(wrapper.find('[data-spell-dc]').exists()).toBe(false)
    expect(wrapper.get('[data-spell-casting-hint]').text()).toContain('Escolha o atributo de conjuração na aba Ficha')
  })

  it('a descrição é texto: tags aparecem como texto, quebras de linha ficam', async () => {
    loaded(makeCharacter({ spells: [spell('Luz', 0, { description: 'Primeira linha.\n<b>x</b><img src=x onerror=alert(1)>' })] }))
    const wrapper = mount(SpellList)
    expect(wrapper.find('[data-spell-description]').exists()).toBe(false)
    await wrapper.get('[data-spell-expand]').trigger('click')
    const description = wrapper.get('[data-spell-description]')
    expect(description.text()).toContain('<b>x</b>')
    expect(description.find('b').exists()).toBe(false)
    expect(description.find('img').exists()).toBe(false)
    expect(description.element.textContent).toContain('Primeira linha.\n<b>x</b>')
    expect(wrapper.get('[data-spell-expand]').attributes('aria-expanded')).toBe('true')
  })

  it('remover pede confirmação', async () => {
    loaded(book())
    api.removeSpell.mockResolvedValue(makeCharacter())
    const wrapper = mount(SpellList)
    await wrapper.get('[data-spell-id="Escudo Arcano"] [data-remove-spell]').trigger('click')
    expect(api.removeSpell).not.toHaveBeenCalled()
    await wrapper.get('[data-spell-id="Escudo Arcano"] [data-confirm-remove]').trigger('click')
    await flushPromises()
    expect(api.removeSpell).toHaveBeenCalledWith(expect.anything(), 'Escudo Arcano')
  })

  it('nova magia: salvaguarda exige o atributo e grava no formato do servidor', async () => {
    loaded(makeCharacter())
    api.addSpell.mockResolvedValue(makeCharacter())
    const wrapper = mount(SpellList, { attachTo: document.body })
    await wrapper.get('[data-new-spell]').trigger('click')
    await flushPromises()
    expect(document.activeElement).toBe(wrapper.get('[data-spell-field="name"]').element)
    await wrapper.get('[data-spell-field="name"]').setValue('Explosão Sombria')
    await wrapper.get('[data-spell-field="level"]').setValue('2')
    await wrapper.get('[data-spell-field="attackKind"]').setValue('Save')
    await wrapper.get('[data-spell-field="damageDice"]').setValue('3d8 + 1d6')
    await wrapper.get('[data-spell-editor]').trigger('submit')
    expect(api.addSpell).not.toHaveBeenCalled()
    expect(wrapper.get('[data-spell-error]').text()).toContain('salvaguarda')

    await wrapper.get('[data-spell-field="saveAbility"]').setValue('Con')
    await wrapper.get('[data-spell-field="damageType"]').setValue('necrótico')
    await wrapper.get('[data-spell-field="concentration"]').trigger('click')
    await wrapper.get('[data-spell-editor]').trigger('submit')
    await flushPromises()
    expect(api.addSpell.mock.calls[0]![1]).toMatchObject({
      name: 'Explosão Sombria', level: 2, attackKind: 'Save', saveAbility: 'Con', damageDice: '3d8+1d6', damageType: 'necrótico', concentration: true
    })
    expect(wrapper.find('[data-spell-editor]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('editar envia o id da magia', async () => {
    loaded(book())
    api.saveSpell.mockResolvedValue(book())
    const wrapper = mount(SpellList)
    await wrapper.get('[data-spell-id="Escudo Arcano"] [data-edit-spell]').trigger('click')
    expect((wrapper.get('[data-spell-field="castingTime"]').element as HTMLInputElement).value).toBe('1 reação')
    await wrapper.get('[data-spell-editor]').trigger('submit')
    await flushPromises()
    expect(api.saveSpell.mock.calls[0]![1]).toBe('Escudo Arcano')
  })

  it('"Do catálogo" abre o seletor de magias e mostra o aviso de magia repetida (409)', async () => {
    loaded(book())
    api.listCatalogSpells.mockResolvedValue({ items: [{ id: 'fb', name: 'Bola de Fogo', level: 3, school: 'evocação', ritual: false, concentration: false, classes: ['mago'] }], total: 1, page: 1, pageSize: 20, attribution: null })
    api.addSpellFromCatalog.mockRejectedValue(Object.assign(new Error('Bola de Fogo já está na sua ficha.'), { status: 409 }))
    const wrapper = mount(SpellList)
    await wrapper.get('[data-open-catalog]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-catalog-picker="spells"]').exists()).toBe(true)
    await wrapper.get('[data-add-catalog]').trigger('click')
    await flushPromises()
    expect(wrapper.get('[data-catalog-error]').text()).toBe('Bola de Fogo já está na sua ficha.')
  })
})
