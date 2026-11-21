import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { defineComponent, reactive } from 'vue'
import GameModeToggle from '~/components/GameModeToggle.vue'
import { useGameMode } from '~/composables/useGameMode'
import { useCharacterStore } from '~/stores/character'
import IndexPage from '~/pages/index.vue'
import { makeFighter } from './player-roll-fixture'

describe('useGameMode', () => {
  afterEach(() => { useGameMode().exit() })

  it('starts inactive and toggles/enters/exits correctly', () => {
    const gameMode = useGameMode()
    expect(gameMode.active.value).toBe(false)
    gameMode.enter()
    expect(gameMode.active.value).toBe(true)
    gameMode.toggle()
    expect(gameMode.active.value).toBe(false)
    gameMode.toggle()
    expect(gameMode.active.value).toBe(true)
    gameMode.exit()
    expect(gameMode.active.value).toBe(false)
  })

  it('is shared across every consumer (module-level singleton)', () => {
    useGameMode().enter()
    expect(useGameMode().active.value).toBe(true)
  })
})

describe('GameModeToggle', () => {
  afterEach(() => { useGameMode().exit() })

  it('shows "Modo Jogo" when inactive and switches the game mode state on click', async () => {
    const wrapper = mount(GameModeToggle)
    expect(wrapper.text()).toContain('Modo Jogo')
    expect(useGameMode().active.value).toBe(false)

    await wrapper.get('button').trigger('click')

    expect(useGameMode().active.value).toBe(true)
    expect(wrapper.text()).toContain('Voltar à visão completa')
    wrapper.unmount()
  })

  it('reflects an already-active game mode and switches it back off on click', async () => {
    useGameMode().enter()
    const wrapper = mount(GameModeToggle)
    expect(wrapper.text()).toContain('Voltar à visão completa')

    await wrapper.get('button').trigger('click')

    expect(useGameMode().active.value).toBe(false)
    expect(wrapper.text()).toContain('Modo Jogo')
    wrapper.unmount()
  })
})

// Spec 011, T045: in the Modo Jogo a player with a character gets the roll panel instead of the quick
// roller; a player without one keeps the quick roller; the master is unchanged.
describe('Modo Jogo: painel de rolagem do jogador (index.vue)', () => {
  const characterApi = { getCharacter: vi.fn() }

  const PanelStub = defineComponent({
    name: 'PlayerRollPanel',
    emits: ['attack-rolled', 'open-inventory'],
    template: `<div data-stub="player-roll-panel">
      <button data-fake-attack @click="$emit('attack-rolled', { id: 'r9', total: 18 }, 'd8 + (FOR) 3 + (arma) 1')" />
      <button data-fake-typed-attack @click="$emit('attack-rolled', { id: 'r9', total: 18 }, '2d6 + 2d8 + (FOR) 3', 'contundente + radiante', { baseType: 'contundente', baseTermCount: 1, components: [{ label: 'Smite', damageType: 'radiante', termCount: 1 }] })" />
      <button data-fake-inventory data-open-inventory @click="$emit('open-inventory')" />
    </div>`
  })
  const QuickStub = defineComponent({
    name: 'QuickDiceRoller',
    emits: ['rolled-d20'],
    template: `<div data-stub="quick-dice-roller"><button data-fake-d20 @click="$emit('rolled-d20', { id: 'q1', total: 12 })" /></div>`
  })
  const ConfirmStub = defineComponent({
    name: 'AttackConfirmModal',
    props: { roll: { type: Object, required: true } },
    emits: ['hit', 'dismissed'],
    template: `<div data-stub="attack-confirm" :data-total="roll.total"><button data-fake-hit @click="$emit('hit', roll)" /><button data-fake-miss @click="$emit('dismissed')" /></div>`
  })
  const DamageStub = defineComponent({
    name: 'DamageRollModal',
    props: { expression: { type: String, default: undefined } },
    emits: ['resolved', 'cancelled'],
    template: `<div data-stub="damage-modal" :data-expression="expression ?? 'none'"><button data-fake-done @click="$emit('resolved')" /></div>`
  })
  // A bag with a nested editor open: the editor stops its own Esc, like ItemEditorModal and CatalogPicker.
  const nestedEscapes = vi.fn()
  const InventoryStub = defineComponent({
    name: 'InventoryPanel',
    props: { compact: Boolean, hideTitle: Boolean },
    setup: () => ({ nestedEscapes }),
    template: `<div data-stub="inventory" :data-compact="compact" :data-hide-title="hideTitle">
      <button data-stub-first>Novo item</button>
      <div role="dialog" aria-modal="true" data-stub-nested @keydown.esc.stop="nestedEscapes()"><input data-nested-field></div>
      <button data-stub-last>Remover</button>
    </div>`
  })

  const stubs = {
    MasterRollPanel: {
      props: ['gameMode'], emits: ['rolled-d20'],
      template: `<div data-stub="master-roll-panel" :data-game-mode="String(gameMode)"><button data-fake-d20 @click="$emit('rolled-d20', { id: 'q1', total: 12 })" /></div>`
    },
    PlayerRollPanel: PanelStub, QuickDiceRoller: QuickStub, AttackConfirmModal: ConfirmStub, DamageRollModal: DamageStub, InventoryPanel: InventoryStub,
    DiceRoller: { template: '<div data-stub="dice-roller" />' },
    CampaignAccess: true, ViewNav: true, GameModeToggle: true, ConnectionStatus: true, UserMenu: true, CharacterSheetView: true,
    CreatureLibrary: true, CreatureSheetModal: true, BattlemapBoard: true, BattlemapSetup: true, EncounterSetup: true,
    InitiativeTracker: true, CombatAttack: true, ParticipantsPanel: true, RollHistory: true, SheetSummaryBar: true
  }

  function setup({ role = 'Player' as 'Player' | 'Master', combatant = true } = {}) {
    setActivePinia(createPinia())
    const campaign = reactive({
      session: { campaignId: 'c1', playerCode: '1234', masterCode: role === 'Master' ? '9999' : null, participantId: 'p1', displayName: 'Loren', role, participantToken: 't' },
      isMaster: role === 'Master',
      connection: 'online',
      error: '',
      snapshot: {
        campaign: { name: 'Mesa', playerCode: '1234' },
        participants: [],
        diceRolls: [],
        activeEncounter: { id: 'e1', status: 'Active', combatants: combatant ? [{ id: 'k1', participantId: 'p1', isDefeated: false }] : [] }
      },
      battlemap: null,
      battlemapLoading: false,
      battlemapImageSource: null,
      avatarUrls: {},
      loadSession: vi.fn(),
      refresh: vi.fn().mockResolvedValue(undefined)
    })
    vi.stubGlobal('useCampaignStore', () => campaign)
    vi.stubGlobal('useCharacterStore', () => useCharacterStore())
    vi.stubGlobal('useCharacterApi', () => characterApi)
    vi.stubGlobal('useCampaignRealtime', () => ({ connect: vi.fn().mockResolvedValue(undefined), disconnect: vi.fn().mockResolvedValue(undefined) }))
    vi.stubGlobal('useAppView', () => ({ view: ref('mesa'), go: vi.fn() }))
    vi.stubGlobal('useCreatureStore', () => ({ close: vi.fn(), open: vi.fn(), sheet: null }))
    vi.stubGlobal('useImageCacheStore', () => ({ ensure: vi.fn(), urls: {} }))
  }

  async function mountPage(): Promise<VueWrapper> {
    const wrapper = mount(IndexPage, { attachTo: document.body, global: { stubs } })
    await flushPromises()
    return wrapper
  }

  beforeEach(() => {
    characterApi.getCharacter.mockReset().mockResolvedValue(makeFighter())
    useGameMode().enter()
  })
  afterEach(() => { useGameMode().exit(); vi.unstubAllGlobals(); document.body.innerHTML = '' })

  it('a player with a character sees the roll panel instead of the quick roller (loaded on entering the Modo Jogo)', async () => {
    setup()
    const wrapper = await mountPage()
    expect(characterApi.getCharacter).toHaveBeenCalledTimes(1)
    expect(wrapper.find('[data-stub="player-roll-panel"]').exists()).toBe(true)
    expect(wrapper.find('[data-stub="quick-dice-roller"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('a character already loaded is not fetched again', async () => {
    setup()
    useCharacterStore().character = makeFighter()
    const wrapper = await mountPage()
    expect(characterApi.getCharacter).not.toHaveBeenCalled()
    expect(wrapper.find('[data-stub="player-roll-panel"]').exists()).toBe(true)
    wrapper.unmount()
  })

  it('a player without a character keeps the quick roller', async () => {
    setup()
    characterApi.getCharacter.mockRejectedValue(new Error('Sem ficha.'))
    const wrapper = await mountPage()
    expect(wrapper.find('[data-stub="quick-dice-roller"]').exists()).toBe(true)
    expect(wrapper.find('[data-stub="player-roll-panel"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('the master gets the creature roll panel (spec 012) and no character is loaded', async () => {
    setup({ role: 'Master' })
    const wrapper = await mountPage()
    expect(characterApi.getCharacter).not.toHaveBeenCalled()
    expect(wrapper.get('[data-stub="master-roll-panel"]').attributes('data-game-mode')).toBe('true')
    expect(wrapper.find('[data-stub="player-roll-panel"]').exists()).toBe(false)
    // Quick d20: "Acertou?" then the damage roller without a prefilled expression, as before.
    await wrapper.get('[data-fake-d20]').trigger('click')
    expect(wrapper.get('[data-stub="attack-confirm"]').attributes('data-total')).toBe('12')
    await wrapper.get('[data-fake-hit]').trigger('click')
    expect(wrapper.get('[data-stub="damage-modal"]').attributes('data-expression')).toBe('none')
    wrapper.unmount()
  })

  it('spec 012: outside the Modo Jogo the player also rolls from the sheet', async () => {
    setup()
    useGameMode().exit()
    const wrapper = await mountPage()
    expect(characterApi.getCharacter).toHaveBeenCalledTimes(1)
    expect(wrapper.find('[data-stub="player-roll-panel"]').exists()).toBe(true)
    expect(wrapper.find('[data-stub="dice-roller"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('spec 012: the master outside the Modo Jogo gets the creature roll panel in full-view mode', async () => {
    setup({ role: 'Master' })
    useGameMode().exit()
    const wrapper = await mountPage()
    expect(wrapper.get('[data-stub="master-roll-panel"]').attributes('data-game-mode')).toBe('false')
    wrapper.unmount()
  })

  it('a weapon attack of a combatant asks "Acertou?" and a hit opens the damage roll of the same weapon', async () => {
    setup()
    const wrapper = await mountPage()
    await wrapper.get('[data-fake-attack]').trigger('click')
    expect(wrapper.get('[data-stub="attack-confirm"]').attributes('data-total')).toBe('18')
    await wrapper.get('[data-fake-hit]').trigger('click')
    expect(wrapper.find('[data-stub="attack-confirm"]').exists()).toBe(false)
    expect(wrapper.get('[data-stub="damage-modal"]').attributes('data-expression')).toBe('d8 + (FOR) 3 + (arma) 1')
    await wrapper.get('[data-fake-done]').trigger('click')
    expect(wrapper.find('[data-stub="damage-modal"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('a miss closes the question without opening the damage roll', async () => {
    setup()
    const wrapper = await mountPage()
    await wrapper.get('[data-fake-attack]').trigger('click')
    await wrapper.get('[data-fake-miss]').trigger('click')
    expect(wrapper.find('[data-stub="attack-confirm"]').exists()).toBe(false)
    expect(wrapper.find('[data-stub="damage-modal"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('a player who is not a combatant of the active encounter is not asked', async () => {
    setup({ combatant: false })
    const wrapper = await mountPage()
    await wrapper.get('[data-fake-attack]').trigger('click')
    expect(wrapper.find('[data-stub="attack-confirm"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('offers configured damage after a hit even when the player is outside combat', async () => {
    setup({ combatant: false })
    const wrapper = await mountPage()
    await wrapper.get('[data-fake-typed-attack]').trigger('click')
    await wrapper.get('[data-fake-hit]').trigger('click')
    expect(wrapper.get('[data-stub="damage-modal"]').attributes('data-expression')).toBe('2d6 + 2d8 + (FOR) 3')
    wrapper.unmount()
  })

  it('"Mochila" opens the bag in a modal without leaving the Modo Jogo', async () => {
    setup()
    const wrapper = await mountPage()
    expect(wrapper.find('[data-game-inventory]').exists()).toBe(false)
    await wrapper.get('[data-fake-inventory]').trigger('click')
    expect(wrapper.get('[data-game-inventory] [data-stub="inventory"]').attributes('data-compact')).toBe('true')
    expect(useGameMode().active.value).toBe(true)
    await wrapper.get('[data-close-inventory]').trigger('click')
    expect(wrapper.find('[data-game-inventory]').exists()).toBe(false)
    await wrapper.get('[data-fake-inventory]').trigger('click')
    await wrapper.get('[data-game-inventory] [data-stub-first]').trigger('keydown', { key: 'Escape' })
    await flushPromises()
    expect(wrapper.find('[data-game-inventory]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('T078: one "Mochila" title; focus enters on open, Tab stays inside, focus returns on close', async () => {
    setup()
    const wrapper = await mountPage()
    await wrapper.get('[data-fake-inventory]').trigger('click')
    await flushPromises()
    const dialog = wrapper.get('[data-game-inventory]')
    expect(dialog.findAll('h2').map(h => h.text())).toEqual(['Mochila'])
    expect(dialog.get('[data-stub="inventory"]').attributes('data-hide-title')).toBe('true')
    expect(dialog.element.contains(document.activeElement)).toBe(true)
    expect(document.activeElement).toBe(dialog.get('#game-inventory-title').element)

    // Tab from the last control wraps to the first; Shift+Tab from the first wraps to the last.
    const last = dialog.get('[data-stub-last]')
    ;(last.element as HTMLElement).focus()
    await last.trigger('keydown', { key: 'Tab' })
    expect(document.activeElement).toBe(dialog.get('[data-close-inventory]').element)
    await dialog.get('[data-close-inventory]').trigger('keydown', { key: 'Tab', shiftKey: true })
    expect(document.activeElement).toBe(last.element)

    await dialog.get('[data-close-inventory]').trigger('click')
    await flushPromises()
    expect(document.activeElement).toBe(wrapper.get('[data-open-inventory]').element)
    wrapper.unmount()
  })

  it('T078: Esc inside a nested editor closes the editor only; Esc elsewhere on the window does nothing', async () => {
    setup()
    const wrapper = await mountPage()
    await wrapper.get('[data-fake-inventory]').trigger('click')
    nestedEscapes.mockReset()
    await wrapper.get('[data-nested-field]').trigger('keydown', { key: 'Escape' })
    await flushPromises()
    expect(nestedEscapes).toHaveBeenCalledTimes(1)
    expect(wrapper.find('[data-game-inventory]').exists()).toBe(true)
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    await flushPromises()
    expect(wrapper.find('[data-game-inventory]').exists()).toBe(true)
    wrapper.unmount()
  })
})
