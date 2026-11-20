import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useCampaignStore } from '~/stores/campaign'
import { useCharacterStore } from '~/stores/character'
import PlayerRollPanel from '~/components/character/PlayerRollPanel.vue'
import SavedRollEditor from '~/components/character/SavedRollEditor.vue'
import SavedRollList from '~/components/character/SavedRollList.vue'
import type { Character, SavedRollData } from '~/types/character'
import { SAVED_ROLL_ICONS } from '~/utils/savedRollIcons'
import { makeFighter, savedRoll } from './player-roll-fixture'

// Spec 011, US6 and FR-036: the editor reuses the panel controls, so the same configuration shows the
// same expression; it saves references only, in the SavedRollData shape of the backend.
const characterApi = { addRoll: vi.fn(), saveRoll: vi.fn(), duplicateRoll: vi.fn(), removeRoll: vi.fn() }
vi.stubGlobal('useCompanionApi', () => ({ rollDice: vi.fn(), revealDiceRoll: vi.fn() }))
vi.stubGlobal('useCharacterApi', () => characterApi)
vi.stubGlobal('useCampaignStore', () => useCampaignStore())
vi.stubGlobal('useCharacterStore', () => useCharacterStore())

function setup(character: Character = makeFighter()) {
  setActivePinia(createPinia())
  const campaign = useCampaignStore()
  campaign.session = { campaignId: 'c1', playerCode: '1234', masterCode: null, participantId: 'p1', displayName: 'Loren', role: 'Player', participantToken: 't' } as never
  campaign.connection = 'online'
  campaign.snapshot = { diceRolls: [] } as never
  useCharacterStore().character = character
}

/** Applies the same configuration through the shared controls of either component. */
async function configure(wrapper: VueWrapper) {
  await wrapper.get('[data-source-select]').setValue('skill:furtividade')
  await wrapper.get('[data-die="d4"]').trigger('click')
  await wrapper.get('[data-add-extra]').trigger('click')
  await wrapper.get('[data-extra-label]').setValue('Guia')
  await wrapper.get('[data-extra-value]').setValue('1')
  await wrapper.get('[data-advantage="advantage"]').trigger('click')
  await wrapper.get('[data-repetitions]').setValue('2')
}

beforeEach(() => {
  Object.values(characterApi).forEach(mock => mock.mockReset())
})
afterEach(() => { document.body.innerHTML = '' })

describe('SavedRollEditor', () => {
  it('shows the same expression as the panel for the same configuration (FR-036)', async () => {
    setup()
    const panel = mount(PlayerRollPanel, { attachTo: document.body, global: { stubs: { DiceRollAnimation: true } } })
    const editor = mount(SavedRollEditor, { attachTo: document.body })
    await configure(panel)
    await configure(editor)
    const expected = '2#[2#(d20 + d4 + (DES) 2 + (prof) 3 + (Guia) 1) maior]'
    expect(panel.get('[data-expression]').text()).toBe(expected)
    expect(editor.get('[data-expression]').text()).toBe(expected)
    // Same options: the same sources, kinds and advantage buttons.
    const options = (w: VueWrapper) => w.findAll('[data-source], [data-kind], [data-advantage], [data-die]').map(el => el.attributes('data-source') ?? el.attributes('data-kind') ?? el.attributes('data-advantage') ?? el.attributes('data-die'))
    expect(options(editor)).toEqual(options(panel))
  })

  it('offers exactly the 16 icons of the backend list, in order', () => {
    setup()
    const editor = mount(SavedRollEditor, { attachTo: document.body })
    expect(editor.findAll('[data-icon][role="radio"]').map(button => button.attributes('data-icon'))).toEqual([...SAVED_ROLL_ICONS])
    expect(editor.get('[data-icon="lucide:dices"][role="radio"]').attributes('aria-checked')).toBe('true')
  })

  it('saves a new roll with the SavedRollData shape (references, not numbers)', async () => {
    setup()
    characterApi.addRoll.mockResolvedValue(makeFighter({ savedRolls: [savedRoll('new')] }))
    const editor = mount(SavedRollEditor, { attachTo: document.body })
    expect(editor.get('[data-save-saved-roll]').attributes('disabled')).toBeDefined()
    await editor.get('[data-roll-name]').setValue('  Ataque com espada  ')
    await editor.get('[data-icon="lucide:swords"][role="radio"]').trigger('click')
    await editor.get('[data-source-select]').setValue('weapon:sword')
    await editor.get('[data-add-extra]').trigger('click')
    await editor.get('[data-extra-value]').setValue('2')
    await editor.get('[data-advantage="disadvantage"]').trigger('click')
    await editor.get('form').trigger('submit')
    await flushPromises()

    const expected: SavedRollData = {
      name: 'Ataque com espada', icon: 'lucide:swords', kind: 'Attack', dice: {}, ability: null, skill: null, savingThrow: null,
      inventoryItemId: 'sword', characterSpellId: null, versatile: false, extraModifiers: [{ label: null, value: 2 }], advantage: 'Disadvantage', repetitions: 1
    }
    expect(characterApi.addRoll).toHaveBeenCalledWith(expect.objectContaining({ campaignId: 'c1' }), expected)
    expect(Object.keys(characterApi.addRoll.mock.calls[0]![1]).sort()).toEqual(Object.keys(expected).sort())
    expect(editor.emitted('saved')).toHaveLength(1)
    expect(useCharacterStore().character!.savedRolls.map(roll => roll.id)).toEqual(['new'])
  })

  it('edits an existing roll with PUT and shows server errors', async () => {
    const roll = savedRoll('s1', { name: 'Furtivo', icon: 'lucide:footprints', kind: 'Check', inventoryItemId: null, skill: 'furtividade' })
    setup(makeFighter({ savedRolls: [roll] }))
    characterApi.saveRoll.mockRejectedValueOnce(new Error('Escolha um ícone da lista.'))
    const editor = mount(SavedRollEditor, { props: { roll }, attachTo: document.body })
    expect((editor.get('[data-roll-name]').element as HTMLInputElement).value).toBe('Furtivo')
    expect(editor.get('[data-icon="lucide:footprints"][role="radio"]').attributes('aria-checked')).toBe('true')
    expect(editor.get('[data-expression]').text()).toBe('d20 + (DES) 2 + (prof) 3')
    await editor.get('[data-repetitions]').setValue('3')
    await editor.get('form').trigger('submit')
    await flushPromises()
    expect(characterApi.saveRoll).toHaveBeenCalledWith(expect.anything(), 's1', expect.objectContaining({ name: 'Furtivo', skill: 'furtividade', repetitions: 3 }))
    expect(editor.get('[data-editor-error]').text()).toContain('Escolha um ícone da lista.')
    expect(editor.emitted('saved')).toBeUndefined()
  })

  it('keeps a removed weapon visible as missing and blocks saving until it is changed', async () => {
    const roll = savedRoll('s1', { inventoryItemId: 'gone' }, true)
    setup(makeFighter({ savedRolls: [roll] }))
    const editor = mount(SavedRollEditor, { props: { roll }, attachTo: document.body })
    expect(editor.get('option[data-source="weapon:gone"]').text()).toBe('Arma ausente')
    expect(editor.get('[data-roll-error]').text()).toContain('Arma ausente')
    expect(editor.get('[data-save-saved-roll]').attributes('disabled')).toBeDefined()
    await editor.get('[data-source-select]').setValue('weapon:sword')
    expect(editor.get('[data-save-saved-roll]').attributes('disabled')).toBeUndefined()
  })
})

describe('SavedRollList (aba Rolagens)', () => {
  it('shows icon, name, live expression and the incomplete badge', () => {
    setup(makeFighter({ savedRolls: [savedRoll('s1'), savedRoll('s2', { name: 'Arco', inventoryItemId: 'gone' }, true, 1)] }))
    const list = mount(SavedRollList, { attachTo: document.body })
    const items = list.findAll('[data-saved-roll-item]')
    expect(items).toHaveLength(2)
    expect(items[0]!.find('[data-icon="lucide:sword"]').exists()).toBe(true)
    expect(items[0]!.text()).toContain('Ataque com espada')
    expect(items[0]!.get('[data-expression]').text()).toBe('d20 + (FOR) 3 + (prof) 3 + (arma) 1')
    expect(items[0]!.attributes('data-incomplete')).toBeUndefined()
    expect(items[1]!.attributes('data-incomplete')).toBeDefined()
    expect(items[1]!.text()).toContain('Arma ou magia ausente: edite a rolagem')
  })

  it('duplicates, and removes only after confirming', async () => {
    setup(makeFighter({ savedRolls: [savedRoll('s1')] }))
    characterApi.duplicateRoll.mockResolvedValue(makeFighter({ savedRolls: [savedRoll('s1'), savedRoll('s1b', {}, false, 1)] }))
    characterApi.removeRoll.mockResolvedValue(makeFighter({ savedRolls: [savedRoll('s1b')] }))
    const list = mount(SavedRollList, { attachTo: document.body })

    await list.get('[data-duplicate-roll]').trigger('click')
    await flushPromises()
    expect(characterApi.duplicateRoll).toHaveBeenCalledWith(expect.anything(), 's1')
    expect(list.findAll('[data-saved-roll-item]')).toHaveLength(2)

    await list.get('[data-saved-roll-item="s1"] [data-remove-roll]').trigger('click')
    expect(characterApi.removeRoll).not.toHaveBeenCalled()
    await list.get('[data-cancel-remove]').trigger('click')
    expect(list.find('[data-confirm-remove]').exists()).toBe(false)
    await list.get('[data-saved-roll-item="s1"] [data-remove-roll]').trigger('click')
    await list.get('[data-confirm-remove]').trigger('click')
    await flushPromises()
    expect(characterApi.removeRoll).toHaveBeenCalledWith(expect.anything(), 's1')
    expect(list.findAll('[data-saved-roll-item]').map(item => item.attributes('data-saved-roll-item'))).toEqual(['s1b'])
  })

  it('T078: focus moves to "Remover" on ask, back to the trash can on "Manter", onto the list after removal', async () => {
    setup(makeFighter({ savedRolls: [savedRoll('s1'), savedRoll('s2', {}, false, 1)] }))
    characterApi.removeRoll.mockResolvedValue(makeFighter({ savedRolls: [savedRoll('s2')] }))
    const list = mount(SavedRollList, { attachTo: document.body })
    const row = '[data-saved-roll-item="s1"]'
    await list.get(`${row} [data-remove-roll]`).trigger('click')
    await flushPromises()
    expect(document.activeElement).toBe(list.get(`${row} [data-confirm-remove]`).element)
    await list.get(`${row} [data-cancel-remove]`).trigger('click')
    await flushPromises()
    expect(document.activeElement).toBe(list.get(`${row} [data-remove-roll]`).element)
    await list.get(`${row} [data-remove-roll]`).trigger('click')
    await list.get(`${row} [data-confirm-remove]`).trigger('click')
    await flushPromises()
    expect(document.activeElement).toBe(list.get('[data-saved-roll-list]').element)
    list.unmount()
  })

  it('opens the editor to create and to edit', async () => {
    setup(makeFighter({ savedRolls: [savedRoll('s1')] }))
    const list = mount(SavedRollList, { attachTo: document.body })
    await list.get('[data-new-roll]').trigger('click')
    expect(list.get('[data-saved-roll-editor]').text()).toContain('Nova rolagem')
    await list.get('[data-saved-roll-editor] [data-cancel-editor]').trigger('click')
    expect(list.find('[data-saved-roll-editor]').exists()).toBe(false)
    await list.get('[data-edit-roll]').trigger('click')
    expect(list.get('[data-saved-roll-editor]').text()).toContain('Editar rolagem')
    expect((list.get('[data-roll-name]').element as HTMLInputElement).value).toBe('Ataque com espada')
  })
})
