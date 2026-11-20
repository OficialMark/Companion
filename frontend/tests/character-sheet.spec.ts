import { enableAutoUnmount, mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useCampaignStore } from '~/stores/campaign'
import { AUTOSAVE_DELAY_MS, RELOAD_DISCARDS_EDITS, useCharacterStore } from '~/stores/character'
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
// The sheet listens on window ('online', 'beforeunload'): a view left mounted by one test would answer
// the events of the next.
enableAutoUnmount(afterEach)

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
    // The edit was never saved: reloading discards it, so the player confirms first (T077).
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
    await wrapper.get('[data-reload-sheet]').trigger('click')
    await flushPromises()
    expect(confirm).toHaveBeenCalledWith(RELOAD_DISCARDS_EDITS)
    expect(api.getCharacter).toHaveBeenCalledTimes(1)
    expect(useCharacterStore().draft!.strength).toBe(14)

    confirm.mockReturnValue(true)
    await wrapper.get('[data-reload-sheet]').trigger('click')
    await flushPromises()
    expect(api.getCharacter).toHaveBeenCalledTimes(2)
    expect(useCharacterStore().draft!.strength).toBe(10)
    confirm.mockRestore()
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

// Phase 12 (T077): findings of the cold UX review of the sheet.
describe('Tela Ficha: achados da revisão (T077)', () => {
  async function typeInto(wrapper: ReturnType<typeof mount>, field: string, value: string) {
    const input = wrapper.get(`[data-field="${field}"]`)
    ;(input.element as HTMLInputElement).value = value
    await input.trigger('input')
    return input
  }

  it('salvaguarda e perícia mostram a composição ao toque, separada do botão de proficiência', async () => {
    const character = makeCharacter()
    character.stats.savingThrows.str = { ...character.stats.savingThrows.str, bonus: 3, parts: [
      { label: 'FOR', value: 0, source: 'Força', itemId: null },
      { label: 'prof', value: 2, source: 'Proficiência', itemId: null },
      { label: 'Anel', value: 1, source: 'Anel de Proteção', itemId: 'i9' }
    ] }
    character.stats.skills.atletismo = { ...character.stats.skills.atletismo!, bonus: 3, parts: [
      { label: 'prof', value: 2, source: 'Proficiência', itemId: null },
      { label: 'Manto', value: 1, source: 'Manto do Atleta', itemId: 'i8' }
    ] }
    api.getCharacter.mockResolvedValue(character)
    const wrapper = mount(CharacterSheetView, { global: { stubs }, attachTo: document.body })
    await flushPromises()

    await wrapper.get('[data-derived="save-str"] button').trigger('click')
    const save = wrapper.get('[data-derived="save-str"] [data-breakdown]').text()
    expect(save).toContain('Anel de Proteção')
    expect(save).toContain('+1')
    // Opening the breakdown does not toggle the proficiency: that is its own button.
    expect(useCharacterStore().draft!.savingThrowProficiencies).toEqual([])
    expect(wrapper.get('[data-derived="save-str"] [data-value]').text()).toBe('+3')

    await wrapper.get('[data-derived="skill-atletismo"] button').trigger('click')
    expect(wrapper.get('[data-derived="skill-atletismo"] [data-breakdown]').text()).toContain('Manto do Atleta')
    wrapper.unmount()
  })

  it('cada derivado repetido tem um nome acessível próprio', async () => {
    const wrapper = mount(CharacterSheetView, { global: { stubs } })
    await flushPromises()
    expect(wrapper.get('[data-derived="ability-str"] button').attributes('aria-label')).toBe('Modificador de Força: +0. Ver composição')
    expect(wrapper.get('[data-derived="save-dex"] button').attributes('aria-label')).toBe('Salvaguarda de Destreza: +0. Ver composição')
    expect(wrapper.get('[data-save="dex"]').attributes('aria-label')).toBe('Proficiência em salvaguarda de Destreza')
    expect(wrapper.get('[data-derived="skill-furtividade"] button').attributes('aria-label')).toBe('Bônus de Furtividade: +0. Ver composição')
    const names = wrapper.findAll('.derived-stat-button').map(button => button.attributes('aria-label'))
    expect(new Set(names).size).toBe(names.length)
  })

  it('o alerta de erro some quando a gravação seguinte passa', async () => {
    api.saveSheet.mockRejectedValueOnce(new Error('Falha de rede')).mockResolvedValue(makeCharacter())
    const wrapper = mount(CharacterSheetView, { global: { stubs } })
    await flushPromises()
    await typeInto(wrapper, 'strength', '14')
    await vi.advanceTimersByTimeAsync(AUTOSAVE_DELAY_MS)
    await flushPromises()
    expect(wrapper.get('.form-error').text()).toBe('Falha de rede')

    await wrapper.get('[data-retry-save]').trigger('click')
    await flushPromises()
    expect(wrapper.get('[data-save-state]').attributes('data-save-state')).toBe('saved')
    expect(wrapper.find('.form-error').exists()).toBe(false)
    expect(useCharacterStore().error).toBe('')
  })

  it('recarregar sem edição pendente não pergunta nada', async () => {
    const wrapper = mount(CharacterSheetView, { global: { stubs } })
    await flushPromises()
    const store = useCharacterStore()
    store.saveState = 'conflict'
    await flushPromises()
    const confirm = vi.spyOn(window, 'confirm')
    await wrapper.get('[data-reload-sheet]').trigger('click')
    await flushPromises()
    expect(confirm).not.toHaveBeenCalled()
    expect(api.getCharacter).toHaveBeenCalledTimes(2)
    confirm.mockRestore()
  })

  it('grava de novo sozinha quando a conexão volta', async () => {
    api.saveSheet.mockRejectedValueOnce(new Error('Falha de rede')).mockResolvedValue(makeCharacter())
    const wrapper = mount(CharacterSheetView, { global: { stubs } })
    await flushPromises()
    await typeInto(wrapper, 'strength', '14')
    await vi.advanceTimersByTimeAsync(AUTOSAVE_DELAY_MS)
    await flushPromises()
    expect(useCharacterStore().saveState).toBe('error')
    expect(api.saveSheet).toHaveBeenCalledTimes(1)

    window.dispatchEvent(new Event('online'))
    await flushPromises()
    expect(api.saveSheet).toHaveBeenCalledTimes(2)
    expect(useCharacterStore().saveState).toBe('saved')

    // Without pending edits, coming back online saves nothing.
    window.dispatchEvent(new Event('online'))
    await flushPromises()
    expect(api.saveSheet).toHaveBeenCalledTimes(2)
    wrapper.unmount()
    // Unmounted: the listener is gone.
    useCharacterStore().editSequence++
    window.dispatchEvent(new Event('online'))
    await flushPromises()
    expect(api.saveSheet).toHaveBeenCalledTimes(2)
  })

  it('sair do campo grava na hora, sem esperar a pausa', async () => {
    api.saveSheet.mockResolvedValue(makeCharacter())
    const wrapper = mount(CharacterSheetView, { global: { stubs } })
    await flushPromises()
    const input = await typeInto(wrapper, 'characterName', 'Loren')
    expect(api.saveSheet).not.toHaveBeenCalled()
    await input.trigger('focusout')
    await flushPromises()
    expect(api.saveSheet).toHaveBeenCalledTimes(1)
    // The pending autosave timer was cancelled: no second save after the pause.
    await vi.advanceTimersByTimeAsync(AUTOSAVE_DELAY_MS)
    await flushPromises()
    expect(api.saveSheet).toHaveBeenCalledTimes(1)
  })

  it('avisa ao fechar a aba só enquanto há mudança sem salvar, e para de escutar ao desmontar', async () => {
    let resolveSave: (value: unknown) => void = () => {}
    api.saveSheet.mockImplementation(() => new Promise(resolve => { resolveSave = resolve }))
    const add = vi.spyOn(window, 'addEventListener')
    const remove = vi.spyOn(window, 'removeEventListener')
    const wrapper = mount(CharacterSheetView, { global: { stubs } })
    await flushPromises()
    const unload = () => { const event = new Event('beforeunload', { cancelable: true }); window.dispatchEvent(event); return event.defaultPrevented }
    expect(unload()).toBe(false)
    expect(add.mock.calls.some(([type]) => type === 'beforeunload')).toBe(false)

    await typeInto(wrapper, 'notes', 'Um parágrafo inteiro de anotações')
    await flushPromises()
    expect(unload()).toBe(true)

    await vi.advanceTimersByTimeAsync(AUTOSAVE_DELAY_MS)
    resolveSave(makeCharacter())
    await flushPromises()
    expect(useCharacterStore().stale).toBe(false)
    expect(unload()).toBe(false)
    expect(remove.mock.calls.some(([type]) => type === 'beforeunload')).toBe(true)

    await typeInto(wrapper, 'notes', 'outra')
    await flushPromises()
    wrapper.unmount()
    expect(unload()).toBe(false)
    // The unmount flush is waiting on the server: answer it, so the write queue is free for the next test.
    resolveSave(makeCharacter())
    await flushPromises()
    add.mockRestore()
    remove.mockRestore()
  })

  it('abas seguem o padrão ARIA: ids, controles e setas com tabindex itinerante', async () => {
    const wrapper = mount(CharacterSheetView, { global: { stubs }, attachTo: document.body })
    await flushPromises()
    const tabs = () => wrapper.findAll('[role="tab"]')
    const panel = wrapper.get('[role="tabpanel"]')
    expect(tabs().map(tab => tab.attributes('aria-controls'))).toEqual(Array(4).fill(panel.attributes('id')))
    expect(panel.attributes('aria-labelledby')).toBe(tabs()[0]!.attributes('id'))
    expect(tabs().map(tab => tab.attributes('tabindex'))).toEqual(['0', '-1', '-1', '-1'])

    await tabs()[0]!.trigger('keydown', { key: 'ArrowRight' })
    expect(tabs()[1]!.attributes('aria-selected')).toBe('true')
    expect(document.activeElement).toBe(tabs()[1]!.element)
    expect(wrapper.get('[role="tabpanel"]').attributes('aria-labelledby')).toBe('character-tab-mochila')
    expect(tabs().map(tab => tab.attributes('tabindex'))).toEqual(['-1', '0', '-1', '-1'])

    await tabs()[1]!.trigger('keydown', { key: 'End' })
    expect(tabs()[3]!.attributes('aria-selected')).toBe('true')
    await tabs()[3]!.trigger('keydown', { key: 'ArrowRight' })
    expect(tabs()[0]!.attributes('aria-selected')).toBe('true')
    await tabs()[0]!.trigger('keydown', { key: 'ArrowLeft' })
    expect(tabs()[3]!.attributes('aria-selected')).toBe('true')
    await tabs()[3]!.trigger('keydown', { key: 'Home' })
    expect(tabs()[0]!.attributes('aria-selected')).toBe('true')
    expect(document.activeElement).toBe(tabs()[0]!.element)
    wrapper.unmount()
  })
})
