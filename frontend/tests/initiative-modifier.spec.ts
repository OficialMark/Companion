import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { AUTOSAVE_DELAY_MS, useCharacterStore } from '~/stores/character'
import InitiativeModifier from '~/components/InitiativeModifier.vue'
import { makeCharacter } from './character-fixture'

const updateCombatProfile = vi.fn()
const characterApi = { getCharacter: vi.fn(), saveSheet: vi.fn() }
const go = vi.fn()
const participant = { id: 'p1', displayName: 'Lia', role: 'Player', isOnline: true, initiativeModifier: 2, currentHitPoints: 18, maxHitPoints: 20 }
const store = {
  isMaster: false,
  session: { participantId: 'p1', role: 'Player', participantToken: 't' },
  connection: 'online',
  snapshot: { currentParticipant: { ...participant }, participants: [{ ...participant }] }
}
vi.stubGlobal('useCampaignStore', () => store)
vi.stubGlobal('useCompanionApi', () => ({ updateCombatProfile }))
vi.stubGlobal('useCharacterApi', () => characterApi)
vi.stubGlobal('useCharacterStore', () => useCharacterStore())
vi.stubGlobal('useAppView', () => ({ view: ref('mesa'), go }))

const notFound = () => Object.assign(new Error('Ficha não encontrada.'), { status: 404 })

function sheetCharacter() {
  const character = makeCharacter()
  character.sheet.currentHitPoints = 31
  character.stats.initiative = { value: 3, parts: [] }
  character.stats.maxHitPoints = { value: 42, parts: [] }
  return character
}

beforeEach(() => {
  setActivePinia(createPinia())
  updateCombatProfile.mockReset()
  characterApi.getCharacter.mockReset().mockRejectedValue(notFound())
  characterApi.saveSheet.mockReset()
  go.mockReset()
})

describe('InitiativeModifier sem ficha (como antes da spec 011)', () => {
  it('loads and persists current player initiative and hit points', async () => {
    updateCombatProfile.mockResolvedValue({ ...participant, initiativeModifier: 5, currentHitPoints: 24, maxHitPoints: 30 })
    const wrapper = mount(InitiativeModifier)
    await flushPromises()
    expect(wrapper.attributes('data-combat-profile')).toBe('manual')
    expect((wrapper.get('#own-initiative-modifier').element as HTMLInputElement).value).toBe('2')
    expect((wrapper.get('#own-hit-points').element as HTMLInputElement).value).toBe('18')
    expect((wrapper.get('#own-max-hit-points').element as HTMLInputElement).value).toBe('20')
    await wrapper.get('#own-initiative-modifier').setValue('5')
    await wrapper.get('#own-hit-points').setValue('24')
    await wrapper.get('#own-max-hit-points').setValue('30')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(updateCombatProfile).toHaveBeenCalledWith(store.session, 5, 24, 30)
    expect(wrapper.text()).toContain('Perfil de combate salvo.')
  })

  it('asks for the sheet with ifExists, so showing the panel never creates one', async () => {
    mount(InitiativeModifier)
    await flushPromises()
    expect(characterApi.getCharacter).toHaveBeenCalledTimes(1)
    expect(characterApi.getCharacter).toHaveBeenCalledWith(store.session, { ifExists: true })
    expect(useCharacterStore().character).toBeNull()
  })

  it('is not rendered for a master, and asks nothing', async () => {
    store.isMaster = true
    const wrapper = mount(InitiativeModifier)
    await flushPromises()
    expect(wrapper.find('section').exists()).toBe(false)
    expect(characterApi.getCharacter).not.toHaveBeenCalled()
    store.isMaster = false
  })
})

describe('InitiativeModifier com ficha (FR-010)', () => {
  it('shows initiative and max HP from the sheet, read-only, with a way to the Ficha', async () => {
    characterApi.getCharacter.mockResolvedValue(sheetCharacter())
    const wrapper = mount(InitiativeModifier)
    await flushPromises()
    expect(wrapper.attributes('data-combat-profile')).toBe('sheet')
    expect(wrapper.get('[data-from-sheet-hint]').text()).toContain('Vêm da sua ficha')
    expect(wrapper.get('[data-sheet-initiative]').text()).toBe('+3')
    expect(wrapper.get('[data-sheet-max-hp]').text()).toBe('42')
    expect(wrapper.find('#own-initiative-modifier').exists()).toBe(false)
    expect(wrapper.find('#own-max-hit-points').exists()).toBe(false)
    expect(wrapper.find('form').exists()).toBe(false)
    await wrapper.get('[data-open-sheet]').trigger('click')
    expect(go).toHaveBeenCalledWith('ficha')
  })

  it('current HP is edited through the sheet and saved by the character store', async () => {
    vi.useFakeTimers()
    try {
      characterApi.getCharacter.mockResolvedValue(sheetCharacter())
      const saved = sheetCharacter()
      saved.sheet.currentHitPoints = 25
      characterApi.saveSheet.mockResolvedValue(saved)
      const wrapper = mount(InitiativeModifier)
      await flushPromises()
      const input = wrapper.get('#own-hit-points')
      expect((input.element as HTMLInputElement).value).toBe('31')
      ;(input.element as HTMLInputElement).value = '25'
      await input.trigger('input')
      expect(useCharacterStore().draft!.currentHitPoints).toBe(25)
      await vi.advanceTimersByTimeAsync(AUTOSAVE_DELAY_MS)
      await flushPromises()
      expect(characterApi.saveSheet).toHaveBeenCalledTimes(1)
      expect(characterApi.saveSheet.mock.calls[0]![1].currentHitPoints).toBe(25)
      expect(updateCombatProfile).not.toHaveBeenCalled()
      expect(wrapper.get('[data-sheet-save-state]').text()).toContain('Salvo')
    } finally {
      vi.useRealTimers()
    }
  })

  it('a conflict on the current HP offers "Recarregar ficha", confirming before it discards the edit', async () => {
    characterApi.getCharacter.mockResolvedValue(sheetCharacter())
    characterApi.saveSheet.mockRejectedValue(Object.assign(new Error('A ficha mudou.'), { status: 409 }))
    const wrapper = mount(InitiativeModifier)
    await flushPromises()
    const input = wrapper.get('#own-hit-points')
    ;(input.element as HTMLInputElement).value = '5'
    await input.trigger('input')
    await input.trigger('change')
    await flushPromises()
    expect(wrapper.get('[data-sheet-save-state]').attributes('data-sheet-save-state')).toBe('conflict')
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
    await wrapper.get('[data-reload-sheet]').trigger('click')
    expect(characterApi.getCharacter).toHaveBeenCalledTimes(1)
    confirm.mockReturnValue(true)
    await wrapper.get('[data-reload-sheet]').trigger('click')
    await flushPromises()
    expect(characterApi.getCharacter).toHaveBeenCalledTimes(2)
    expect(useCharacterStore().draft!.currentHitPoints).toBe(31)
    confirm.mockRestore()
    wrapper.unmount()
  })

  it('a sheet loaded elsewhere (Ficha, Modo Jogo) switches the panel without another request', async () => {
    const wrapper = mount(InitiativeModifier)
    await flushPromises()
    expect(wrapper.attributes('data-combat-profile')).toBe('manual')
    useCharacterStore().apply(sheetCharacter())
    await flushPromises()
    expect(wrapper.attributes('data-combat-profile')).toBe('sheet')
  })

  it('409 from combat-profile (sheet created meanwhile) switches to the read-only values', async () => {
    updateCombatProfile.mockRejectedValue(Object.assign(new Error('Iniciativa e vida vêm da sua ficha. Edite pela aba Ficha.'), { status: 409 }))
    const wrapper = mount(InitiativeModifier)
    await flushPromises()
    characterApi.getCharacter.mockReset().mockResolvedValue(sheetCharacter())
    await wrapper.get('#own-initiative-modifier').setValue('9')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(characterApi.getCharacter).toHaveBeenCalledWith(store.session, { ifExists: true })
    expect(wrapper.attributes('data-combat-profile')).toBe('sheet')
    expect(wrapper.get('[data-sheet-initiative]').text()).toBe('+3')
  })
})
