import { flushPromises, mount } from '@vue/test-utils'
import MasterRollPanel from '~/components/MasterRollPanel.vue'

const goblin = {
  id: 'cr-goblin', name: 'Goblin', strength: 8, dexterity: 14, constitution: 10, intelligence: 10, wisdom: 8, charisma: 8,
  actions: [{ name: 'Cimitarra', description: 'Ataque com Arma Corpo a Corpo: +4 para acertar. 5 (1d6 + 2) de dano cortante.' }],
  bonusActions: [], reactions: [], legendaryActions: []
}
const combatants = [
  { id: 'cb-1', name: 'Goblin 1', creatureId: 'cr-goblin' },
  { id: 'cb-2', name: 'Lia', creatureId: null, participantId: 'p1' },
  { id: 'cb-3', name: 'Bandido', creatureId: null }
]
const store = reactive({
  session: { campaignId: 'c', participantToken: 't' },
  connection: 'online',
  snapshot: { activeEncounter: { status: 'Active', combatants: [...combatants] }, diceRolls: [] } as { activeEncounter: { status: string; combatants: typeof combatants } | null; diceRolls: unknown[] }
})
const getCreature = vi.fn()
const open = vi.fn()
vi.stubGlobal('useCampaignStore', () => store)
vi.stubGlobal('useCompanionApi', () => ({ getCreature }))
vi.stubGlobal('useCreatureStore', () => ({ open }))

const stubs = {
  CreatureRollPanel: { name: 'CreatureRollPanel', props: ['creature', 'preset', 'label'], emits: ['rolled'], template: '<div data-creature-roll :data-label="label" :data-preset="JSON.stringify(preset)" />' },
  DiceRoller: { template: '<div data-free-roller="full" />' },
  QuickDiceRoller: { template: '<div data-free-roller="game" />' }
}

beforeEach(() => {
  getCreature.mockReset().mockResolvedValue(goblin)
  open.mockReset()
  store.snapshot.activeEncounter = { status: 'Active', combatants: [...combatants] }
})

describe('MasterRollPanel (spec 012, US1)', () => {
  it('lists only library creatures on the table and keeps the free roller by default', () => {
    const wrapper = mount(MasterRollPanel, { global: { stubs } })
    const options = wrapper.findAll('[data-master-roll-source] option').map(option => option.text())
    expect(options).toEqual(['Rolagem livre', 'Goblin 1'])
    expect(wrapper.find('[data-free-roller="full"]').exists()).toBe(true)
    expect(mount(MasterRollPanel, { props: { gameMode: true }, global: { stubs } }).find('[data-free-roller="game"]').exists()).toBe(true)
  })

  it('choosing a creature loads its sheet once and rolls with its name', async () => {
    const wrapper = mount(MasterRollPanel, { global: { stubs } })
    await wrapper.get('[data-master-roll-source]').setValue('cb-1')
    await flushPromises()

    expect(getCreature).toHaveBeenCalledWith(store.session, 'cr-goblin')
    expect(wrapper.get('[data-creature-roll]').attributes('data-label')).toBe('Goblin 1')
    expect(wrapper.find('[data-free-roller]').exists()).toBe(false)

    // Action shortcut sets the preset of the creature roll panel.
    const damage = wrapper.findAll('[data-shortcut="damage"]')[0]!
    await damage.trigger('click')
    expect(JSON.parse(wrapper.get('[data-creature-roll]').attributes('data-preset')!)).toEqual({ count: 1, die: 6, ability: null, bonus: 2 })

    // Back and forth: the sheet comes from the cache.
    await wrapper.get('[data-master-roll-source]').setValue('free')
    await wrapper.get('[data-master-roll-source]').setValue('cb-1')
    await flushPromises()
    expect(getCreature).toHaveBeenCalledTimes(1)

    await wrapper.get('[data-open-creature-sheet]').trigger('click')
    expect(open).toHaveBeenCalledWith('cr-goblin', { id: 'cb-1', name: 'Goblin 1' })
  })

  it('critic 012 #2: a table update (new snapshot objects) keeps the roll being set up and does not refetch', async () => {
    const wrapper = mount(MasterRollPanel, { global: { stubs } })
    await wrapper.get('[data-master-roll-source]').setValue('cb-1')
    await flushPromises()
    const preset = wrapper.findComponent({ name: 'CreatureRollPanel' }).props('preset')
    store.snapshot.activeEncounter = { status: 'Active', combatants: combatants.map(c => ({ ...c })) }
    await flushPromises()
    // Same preset object: the creature panel's watch would reset its form on a new one.
    expect(wrapper.findComponent({ name: 'CreatureRollPanel' }).props('preset')).toBe(preset)
    expect(getCreature).toHaveBeenCalledTimes(1)
  })

  it('critic 012 #3: in the Modo Jogo a creature d20 asks "Acertou?", a damage roll does not', async () => {
    const wrapper = mount(MasterRollPanel, { props: { gameMode: true }, global: { stubs } })
    await wrapper.get('[data-master-roll-source]').setValue('cb-1')
    await flushPromises()
    const panel = wrapper.findComponent({ name: 'CreatureRollPanel' })
    panel.vm.$emit('rolled', { id: 'r1', total: 15 }, { count: 1, die: 20, ability: 'dex', bonus: 0 })
    panel.vm.$emit('rolled', { id: 'r2', total: 5 }, { count: 1, die: 6, ability: null, bonus: 2 })
    expect(wrapper.emitted('rolled-d20')).toEqual([[{ id: 'r1', total: 15 }]])
  })

  it('a creature that leaves the table falls back to the free roll with a notice', async () => {
    const wrapper = mount(MasterRollPanel, { global: { stubs } })
    await wrapper.get('[data-master-roll-source]').setValue('cb-1')
    await flushPromises()

    store.snapshot.activeEncounter = null
    await flushPromises()

    expect((wrapper.get('[data-master-roll-source]').element as HTMLSelectElement).value).toBe('free')
    expect(wrapper.get('[data-master-roll-notice]').text()).toContain('Goblin 1 saiu da mesa')
    expect(wrapper.find('[data-free-roller]').exists()).toBe(true)
  })

  it('shows the error when the sheet cannot be loaded', async () => {
    getCreature.mockRejectedValue(new Error('Criatura não encontrada.'))
    const wrapper = mount(MasterRollPanel, { global: { stubs } })
    await wrapper.get('[data-master-roll-source]').setValue('cb-1')
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain('Criatura não encontrada.')
  })
})
