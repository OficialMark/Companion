import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useCampaignStore } from '~/stores/campaign'
import { useCharacterStore } from '~/stores/character'
import PlayerRollPanel from '~/components/character/PlayerRollPanel.vue'
import type { DiceRoll } from '~/types/campaign'
import type { Character } from '~/types/character'
import { RESULT_HOLD_MS } from '~/utils/creatureRoll'
import { makeFighter, savedRoll } from './player-roll-fixture'

// Spec 011, US4/US5/US6 in the Modo Jogo panel: live expression, armor rule, limits before sending,
// roll through the existing dice route (publish=false, then reveal) with a descriptive label.
const api = { rollDice: vi.fn(), revealDiceRoll: vi.fn() }
const characterApi = { addRoll: vi.fn(), saveRoll: vi.fn() }
vi.stubGlobal('useCompanionApi', () => api)
vi.stubGlobal('useCharacterApi', () => characterApi)
vi.stubGlobal('useCampaignStore', () => useCampaignStore())
vi.stubGlobal('useCharacterStore', () => useCharacterStore())

function diceRoll(id: string, expression: string, label: string | null, total = 15): DiceRoll {
  return {
    id, participantId: 'p1', displayName: 'Loren', expression, values: [12], modifier: 3, total, label, createdAt: '2026-11-19T10:00:00Z',
    results: [{ sequence: 1, terms: [{ expression: 'd20', values: [12], subtotal: 12 }], modifier: 3, total }]
  }
}

function setup(character: Character = makeFighter()) {
  setActivePinia(createPinia())
  const campaign = useCampaignStore()
  campaign.session = { campaignId: 'c1', playerCode: '1234', masterCode: null, participantId: 'p1', displayName: 'Loren', role: 'Player', participantToken: 't' } as never
  campaign.connection = 'online'
  campaign.snapshot = { diceRolls: [] } as never
  useCharacterStore().character = character
  return mount(PlayerRollPanel, { attachTo: document.body, global: { stubs: { DiceRollAnimation: true } } })
}

const expression = (wrapper: VueWrapper) => wrapper.get('[data-expression]').text()
async function pickSource(wrapper: VueWrapper, key: string) {
  await wrapper.get('[data-source-select]').setValue(key)
}

beforeEach(() => {
  vi.useFakeTimers()
  api.rollDice.mockReset().mockImplementation(async (_s, expr: string, _publish: boolean, label: string | null) => diceRoll('r1', expr, label))
  api.revealDiceRoll.mockReset().mockImplementation(async () => diceRoll('r1', 'd20 + (FOR) 3 + (prof) 3 + (arma) 1', 'Ataque: Espada Longa'))
  characterApi.addRoll.mockReset()
})
afterEach(() => { vi.useRealTimers(); document.body.innerHTML = '' })

async function rollAndSettle(wrapper: VueWrapper, selector = '[data-roll-go]') {
  await wrapper.get(selector).trigger('click')
  await flushPromises()
  await vi.advanceTimersByTimeAsync(1000)
  await flushPromises()
}

describe('PlayerRollPanel', () => {
  it('lists equipped weapons, spells, abilities, skills and saves as sources', () => {
    const wrapper = setup()
    const sources = wrapper.findAll('option[data-source]').map(option => option.attributes('data-source'))
    expect(sources).toContain('weapon:sword')
    expect(sources).not.toContain('weapon:bow') // not equipped
    expect(sources).toEqual(expect.arrayContaining(['spell:firebolt', 'spell:fireball', 'ability:Str', 'skill:furtividade', 'save:Dex', 'none']))
  })

  it('updates the expression live as the player builds the roll', async () => {
    const wrapper = setup()
    expect(expression(wrapper)).toBe('')
    expect(wrapper.get('[data-roll-go]').attributes('disabled')).toBeDefined()

    await pickSource(wrapper, 'weapon:sword')
    expect(wrapper.get('[data-kind="attack"]').attributes('aria-checked')).toBe('true')
    expect(wrapper.get('[data-kind="check"]').attributes('disabled')).toBeDefined()
    expect(expression(wrapper)).toBe('d20 + (FOR) 3 + (prof) 3 + (arma) 1')

    await wrapper.get('[data-die="d4"]').trigger('click')
    expect(expression(wrapper)).toBe('d20 + d4 + (FOR) 3 + (prof) 3 + (arma) 1')

    await wrapper.get('[data-add-extra]').trigger('click')
    await wrapper.get('[data-extra] [data-extra-label]').setValue('Bênção')
    await wrapper.get('[data-extra] [data-extra-value]').setValue('2')
    expect(expression(wrapper)).toBe('d20 + d4 + (FOR) 3 + (prof) 3 + (arma) 1 + (Bênção) 2')

    await wrapper.get('[data-kind="damage"]').trigger('click')
    expect(expression(wrapper)).toBe('d8 + d4 + (FOR) 3 + (arma) 1 + (Bênção) 2')
    await wrapper.get('[data-versatile]').setValue(true)
    expect(expression(wrapper)).toBe('d10 + d4 + (FOR) 3 + (arma) 1 + (Bênção) 2')
  })

  it('US5: advantage, disadvantage and repetitions produce the canonical forms', async () => {
    const wrapper = setup()
    await wrapper.get('[data-die="d20"]').trigger('click')
    await wrapper.get('[data-die="d6"]').trigger('click')
    await wrapper.get('[data-die="d6"]').trigger('click')
    await wrapper.get('[data-add-extra]').trigger('click')
    await wrapper.get('[data-extra-value]').setValue('5')
    expect(expression(wrapper)).toBe('1d20+2d6+5')

    await wrapper.get('[data-advantage="advantage"]').trigger('click')
    expect(expression(wrapper)).toBe('2#(d20 + 2d6 + 5) maior')
    // Exclusive: picking disadvantage unselects advantage.
    await wrapper.get('[data-advantage="disadvantage"]').trigger('click')
    expect(wrapper.get('[data-advantage="advantage"]').attributes('aria-checked')).toBe('false')
    expect(expression(wrapper)).toBe('2#(d20 + 2d6 + 5) menor')

    await wrapper.get('[data-advantage="none"]').trigger('click')
    await wrapper.get('[data-repetitions]').setValue('3')
    expect(expression(wrapper)).toBe('3#[d20 + 2d6 + 5]')
    await wrapper.get('[data-advantage="advantage"]').trigger('click')
    expect(expression(wrapper)).toBe('3#[2#(d20 + 2d6 + 5) maior]')

    // Repetitions are clamped to 1..20.
    await wrapper.get('[data-repetitions]').setValue('99')
    expect((wrapper.get('[data-repetitions]').element as HTMLInputElement).value).toBe('20')
  })

  it('US5 scenario 6: the limit error shows before sending and disables the roll', async () => {
    const wrapper = setup()
    await wrapper.get('[data-die="d20"]').trigger('click')
    await wrapper.get('[data-die="d6"]').trigger('click')
    await wrapper.get('[data-die="d6"]').trigger('click')
    await wrapper.get('[data-advantage="advantage"]').trigger('click')
    await wrapper.get('[data-repetitions]').setValue('20')
    expect(wrapper.get('[data-roll-error]').text()).toContain('100 dados')
    expect(wrapper.get('[data-roll-go]').attributes('disabled')).toBeDefined()
    await wrapper.get('[data-roll-go]').trigger('click')
    expect(api.rollDice).not.toHaveBeenCalled()

    await wrapper.get('[data-repetitions]').setValue('16')
    expect(wrapper.find('[data-roll-error]').exists()).toBe(false)
    expect(wrapper.get('[data-roll-go]').attributes('disabled')).toBeUndefined()
  })

  it('preselects disadvantage with the reason for FOR and DES rolls when the armor rule applies, and the player can change it', async () => {
    const character = makeFighter()
    character.stats.rollDefaults = { disadvantageOnStrDex: true, reason: 'Armadura ou escudo sem proficiência: desvantagem em FOR e DES.' }
    const wrapper = setup(character)

    await pickSource(wrapper, 'skill:furtividade')
    expect(wrapper.get('[data-advantage="disadvantage"]').attributes('aria-checked')).toBe('true')
    expect(wrapper.get('[data-disadvantage-reason]').text()).toContain('Armadura ou escudo sem proficiência')
    expect(expression(wrapper)).toBe('2#(d20 + (DES) 2 + (prof) 3) menor')

    await wrapper.get('[data-advantage="none"]').trigger('click')
    expect(expression(wrapper)).toBe('d20 + (DES) 2 + (prof) 3')
    // The reason stays visible; the choice stays the player's.
    expect(wrapper.find('[data-disadvantage-reason]').exists()).toBe(true)

    // A roll without FOR or DES drops the automatic disadvantage and the reason.
    await pickSource(wrapper, 'skill:arcanismo')
    expect(wrapper.get('[data-advantage="none"]').attributes('aria-checked')).toBe('true')
    expect(wrapper.find('[data-disadvantage-reason]').exists()).toBe(false)
    await pickSource(wrapper, 'weapon:sword')
    expect(wrapper.get('[data-advantage="disadvantage"]').attributes('aria-checked')).toBe('true')
    await pickSource(wrapper, 'skill:arcanismo')
    expect(wrapper.get('[data-advantage="none"]').attributes('aria-checked')).toBe('true')
  })

  it('does not preselect disadvantage without the armor rule', async () => {
    const wrapper = setup()
    await pickSource(wrapper, 'skill:furtividade')
    expect(wrapper.get('[data-advantage="none"]').attributes('aria-checked')).toBe('true')
    expect(wrapper.find('[data-disadvantage-reason]').exists()).toBe(false)
  })

  it('rolls with publish=false and a descriptive label, then reveals and adds to the history', async () => {
    const wrapper = setup()
    await pickSource(wrapper, 'weapon:sword')
    await wrapper.get('[data-roll-go]').trigger('click')
    await flushPromises()
    expect(api.rollDice).toHaveBeenCalledWith(expect.objectContaining({ campaignId: 'c1' }), 'd20 + (FOR) 3 + (prof) 3 + (arma) 1', false, 'Ataque: Espada Longa')
    expect(api.revealDiceRoll).not.toHaveBeenCalled()
    expect(wrapper.find('[data-roll-stage]').exists()).toBe(true)

    await vi.advanceTimersByTimeAsync(1000)
    await flushPromises()
    expect(api.revealDiceRoll).toHaveBeenCalledWith(expect.anything(), 'r1')
    expect(useCampaignStore().snapshot!.diceRolls.map(roll => roll.id)).toEqual(['r1'])
    const attack = wrapper.emitted('attack-rolled')!
    expect(attack).toHaveLength(1)
    expect(attack[0]![1]).toBe('d8 + (FOR) 3 + (arma) 1')

    // The result stays for RESULT_HOLD_MS, then the last result line takes its place.
    await vi.advanceTimersByTimeAsync(RESULT_HOLD_MS)
    expect(wrapper.find('[data-roll-stage]').exists()).toBe(false)
    expect(wrapper.get('[data-roll-last]').text()).toContain('Ataque: Espada Longa')
  })

  it('only weapon attacks ask the page for "Acertou?"', async () => {
    const wrapper = setup()
    await pickSource(wrapper, 'skill:furtividade')
    await rollAndSettle(wrapper)
    expect(api.rollDice).toHaveBeenCalledWith(expect.anything(), 'd20 + (DES) 2 + (prof) 3', false, 'Teste: Furtividade')
    expect(wrapper.emitted('rolled')).toHaveLength(1)
    expect(wrapper.emitted('attack-rolled')).toBeUndefined()
  })

  it('a saved roll loads its configuration into the panel and rolls with its name', async () => {
    const character = makeFighter({ savedRolls: [savedRoll('s2', { name: 'Furtivo', icon: 'lucide:footprints', kind: 'Check', inventoryItemId: null, skill: 'furtividade' }, false, 1), savedRoll('s1', { advantage: 'Advantage' }, false, 0)] })
    const wrapper = setup(character)
    const chips = wrapper.findAll('[data-saved-roll]')
    expect(chips.map(chip => chip.attributes('data-saved-roll'))).toEqual(['s1', 's2'])
    expect(chips[0]!.text()).toContain('Ataque com espada')
    expect(chips[0]!.find('[data-icon="lucide:sword"]').exists()).toBe(true)

    await rollAndSettle(wrapper, '[data-saved-roll="s1"]')
    expect(api.rollDice).toHaveBeenCalledWith(expect.anything(), '2#(d20 + (FOR) 3 + (prof) 3 + (arma) 1) maior', false, 'Ataque com espada')
    expect(expression(wrapper)).toBe('2#(d20 + (FOR) 3 + (prof) 3 + (arma) 1) maior')
    expect(wrapper.get('[data-advantage="advantage"]').attributes('aria-checked')).toBe('true')
  })

  it('an incomplete saved roll is disabled with the reason and never rolls', async () => {
    const character = makeFighter({ savedRolls: [savedRoll('s1', { inventoryItemId: 'gone' }, true)] })
    const wrapper = setup(character)
    const chip = wrapper.get('[data-saved-roll="s1"]')
    expect(chip.attributes('data-incomplete')).toBeDefined()
    expect(chip.attributes('disabled')).toBeDefined()
    expect(chip.attributes('title')).toBe('Arma ou magia ausente: edite a rolagem')
    await chip.trigger('click')
    expect(api.rollDice).not.toHaveBeenCalled()
  })

  it('"Salvar como rolagem" opens the editor prefilled with the panel configuration', async () => {
    const wrapper = setup()
    await pickSource(wrapper, 'weapon:sword')
    await wrapper.get('[data-advantage="advantage"]').trigger('click')
    await wrapper.get('[data-save-roll]').trigger('click')
    const editor = wrapper.get('[data-saved-roll-editor]')
    expect(editor.get('[data-expression]').text()).toBe('2#(d20 + (FOR) 3 + (prof) 3 + (arma) 1) maior')
  })

  it('"Mochila" asks the page to open the bag', async () => {
    const wrapper = setup()
    await wrapper.get('[data-open-inventory]').trigger('click')
    expect(wrapper.emitted('open-inventory')).toHaveLength(1)
  })

  it('a server error is shown and the panel is free again', async () => {
    api.rollDice.mockRejectedValueOnce(new Error('Expressão inválida.'))
    const wrapper = setup()
    await pickSource(wrapper, 'ability:Str')
    await wrapper.get('[data-roll-go]').trigger('click')
    await flushPromises()
    expect(wrapper.get('[data-roll-send-error]').text()).toContain('Expressão inválida.')
    expect(wrapper.find('[data-roll-stage]').exists()).toBe(false)
    expect(wrapper.get('[data-roll-go]').attributes('disabled')).toBeUndefined()
  })
})
