import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useCampaignStore } from '~/stores/campaign'
import { AUTOSAVE_DELAY_MS, useCharacterStore } from '~/stores/character'
import CharacterSheetView from '~/components/character/CharacterSheetView.vue'
import SheetCoreTab from '~/components/character/SheetCoreTab.vue'
import DerivedStat from '~/components/character/DerivedStat.vue'
import { makeCharacter } from './character-fixture'

// Spec 011, US1 and FR-018: the sheet edits base values, saves them by itself, and shows only
// numbers calculated by the server.
const api = { getCharacter: vi.fn(), saveSheet: vi.fn() }
vi.stubGlobal('useCharacterApi', () => api)
vi.stubGlobal('useCampaignStore', () => useCampaignStore())
vi.stubGlobal('useCharacterStore', () => useCharacterStore())

const stubs = { SheetCoreTab, DerivedStat, InventoryPanel: true, SpellList: true, SavedRollList: true }

function session() {
  setActivePinia(createPinia())
  useCampaignStore().session = { campaignId: 'c1', playerCode: '1234', masterCode: null, participantId: 'p1', displayName: 'Loren', role: 'Player', participantToken: 't' } as never
}

beforeEach(() => {
  vi.useFakeTimers()
  session()
  api.getCharacter.mockReset().mockResolvedValue(makeCharacter())
  api.saveSheet.mockReset()
})
afterEach(() => { vi.useRealTimers() })

describe('Tela Ficha', () => {
  it('carrega a própria ficha e mostra as quatro abas', async () => {
    const wrapper = mount(CharacterSheetView, { global: { stubs } })
    await flushPromises()
    expect(api.getCharacter).toHaveBeenCalledTimes(1)
    expect(wrapper.findAll('[data-tab]').map(x => x.attributes('data-tab'))).toEqual(['ficha', 'mochila', 'magias', 'rolagens'])
    expect(wrapper.get('h1').text()).toBe('Loren')
  })

  it('editar um atributo grava sozinho depois da pausa e mostra o derivado que o servidor devolveu', async () => {
    const saved = makeCharacter()
    saved.sheet.dexterity = 16
    saved.stats.abilities.dex = { ...saved.stats.abilities.dex, base: 16, final: 16, modifier: 3 }
    saved.stats.initiative = { value: 3, parts: [] }
    api.saveSheet.mockResolvedValue(saved)
    const wrapper = mount(CharacterSheetView, { global: { stubs } })
    await flushPromises()

    const input = wrapper.get('[data-field="dexterity"]')
    ;(input.element as HTMLInputElement).value = '16'
    await input.trigger('input')

    // Before the server answers, the derived value is NOT calculated in the browser: still 0, marked stale.
    expect(wrapper.get('[data-derived="initiative"] [data-value]').text()).toBe('+0')
    expect(wrapper.find('[data-derived="initiative"][data-stale]').exists()).toBe(true)
    expect(wrapper.get('[data-save-state]').attributes('data-save-state')).toBe('saving')
    expect(api.saveSheet).not.toHaveBeenCalled()

    await vi.advanceTimersByTimeAsync(AUTOSAVE_DELAY_MS)
    await flushPromises()

    expect(api.saveSheet).toHaveBeenCalledTimes(1)
    expect(api.saveSheet.mock.calls[0]![1].dexterity).toBe(16)
    expect(api.saveSheet.mock.calls[0]![2]).toBe('AAAAAAAAB9E=')
    expect(wrapper.get('[data-derived="initiative"] [data-value]').text()).toBe('+3')
    expect(wrapper.find('[data-derived="initiative"][data-stale]').exists()).toBe(false)
    expect(wrapper.get('[data-save-state]').attributes('data-save-state')).toBe('saved')
  })

  it('várias teclas seguidas viram uma gravação só', async () => {
    api.saveSheet.mockResolvedValue(makeCharacter())
    const wrapper = mount(CharacterSheetView, { global: { stubs } })
    await flushPromises()
    const input = wrapper.get('[data-field="characterName"]')
    for (const text of ['L', 'Lo', 'Lor', 'Lore', 'Loren']) {
      ;(input.element as HTMLInputElement).value = text
      await input.trigger('input')
      await vi.advanceTimersByTimeAsync(100)
    }
    await vi.advanceTimersByTimeAsync(AUTOSAVE_DELAY_MS)
    await flushPromises()
    expect(api.saveSheet).toHaveBeenCalledTimes(1)
    expect(api.saveSheet.mock.calls[0]![1].characterName).toBe('Loren')
  })

  it('conflito de versão pede para recarregar, sem fingir que salvou', async () => {
    api.saveSheet.mockRejectedValue(Object.assign(new Error('A ficha mudou.'), { status: 409 }))
    const wrapper = mount(CharacterSheetView, { global: { stubs } })
    await flushPromises()
    const input = wrapper.get('[data-field="strength"]')
    ;(input.element as HTMLInputElement).value = '14'
    await input.trigger('input')
    await vi.advanceTimersByTimeAsync(AUTOSAVE_DELAY_MS)
    await flushPromises()

    expect(wrapper.get('[data-save-state]').attributes('data-save-state')).toBe('conflict')
    expect(wrapper.find('[data-reload-sheet]').exists()).toBe(true)
    await wrapper.get('[data-reload-sheet]').trigger('click')
    await flushPromises()
    expect(api.getCharacter).toHaveBeenCalledTimes(2)
  })

  it('o popover de um derivado mostra de onde vem cada número', async () => {
    const character = makeCharacter()
    character.stats.armorClass = { value: 20, parts: [
      { label: 'armadura', value: 16, source: 'Cota de Malha', itemId: 'i1' },
      { label: 'escudo', value: 2, source: 'Escudo', itemId: 'i2' },
      { label: 'Anel de Proteção', value: 1, source: 'Anel de Proteção', itemId: 'i3' },
      { label: 'Escudo +1', value: 1, source: 'Escudo +1 (bônus mágico)', itemId: 'i2' }
    ] }
    api.getCharacter.mockResolvedValue(character)
    const wrapper = mount(CharacterSheetView, { global: { stubs } })
    await flushPromises()
    await wrapper.get('[data-derived="armorClass"] button').trigger('click')
    const breakdown = wrapper.get('[data-derived="armorClass"] [data-breakdown]').text()
    expect(breakdown).toContain('Cota de Malha')
    expect(breakdown).toContain('Anel de Proteção')
    expect(breakdown).toContain('+16')
  })

  it('perícia alterna entre sem proficiência, proficiente e especialista', async () => {
    api.saveSheet.mockResolvedValue(makeCharacter())
    const wrapper = mount(CharacterSheetView, { global: { stubs } })
    await flushPromises()
    const button = () => wrapper.get('[data-skill="furtividade"] .skill-training')
    await button().trigger('click')
    expect(useCharacterStore().draft!.skills.furtividade).toBe('Proficient')
    await button().trigger('click')
    expect(useCharacterStore().draft!.skills.furtividade).toBe('Expertise')
    await button().trigger('click')
    expect(useCharacterStore().draft!.skills.furtividade).toBeUndefined()
  })

  it('outro aparelho da mesma conta avisa e a ficha recarrega; de outra conta, não', async () => {
    mount(CharacterSheetView, { global: { stubs } })
    await flushPromises()
    await useCharacterStore().onRemoteChange('outra-conta')
    expect(api.getCharacter).toHaveBeenCalledTimes(1)
    await useCharacterStore().onRemoteChange('p1')
    expect(api.getCharacter).toHaveBeenCalledTimes(2)
  })
})
