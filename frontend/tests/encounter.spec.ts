import { mount } from '@vue/test-utils'
import EncounterSetup from '~/components/EncounterSetup.vue'
import InitiativeTracker from '~/components/InitiativeTracker.vue'

const rollInitiative = vi.fn()
const updateHitPoints = vi.fn()
const store = { isMaster: true, session: {} as Record<string, unknown>, snapshot: { activeEncounter: null } as { activeEncounter: null | Record<string, unknown> } }
vi.stubGlobal('useCampaignStore', () => store)
vi.stubGlobal('useCompanionApi', () => ({
  createEncounter: vi.fn(), rollInitiative, nextTurn: vi.fn(), endEncounter: vi.fn(),
  updateCombatant: vi.fn(), updateHitPoints, removeCombatant: vi.fn()
}))

describe('EncounterSetup', () => {
  it('lets the master start encounter preparation', () => {
    store.snapshot.activeEncounter = null
    const wrapper = mount(EncounterSetup)
    expect(wrapper.text()).toContain('Iniciativa')
    expect(wrapper.get('button').text()).toContain('Iniciar preparação')
  })

  it('lets the master reroll one combatant during combat', async () => {
    const encounter = {
      id: 'encounter-1', status: 'Active', round: 1, currentTurnIndex: 0,
      combatants: [{ id: 'combatant-1', participantId: null, name: 'Goblin', initiativeModifier: 2, initiative: 14, creationOrder: 0, isCurrent: true, currentHitPoints: 12 }]
    }
    store.snapshot.activeEncounter = encounter
    rollInitiative.mockResolvedValue(encounter)

    const wrapper = mount(InitiativeTracker)
    await wrapper.get('[aria-label="Rolar iniciativa novamente para Goblin"]').trigger('click')

    expect(rollInitiative).toHaveBeenCalledWith(store.session, 'encounter-1', 'combatant-1')
  })

  it('marks a combatant with zero hit points as defeated', () => { store.isMaster = true; store.snapshot.activeEncounter = { id: 'encounter-1', status: 'Active', round: 1, currentTurnIndex: 0, combatants: [{ id: 'combatant-1', participantId: null, name: 'Goblin', initiativeModifier: 2, initiative: 14, creationOrder: 0, isCurrent: true, currentHitPoints: 0, isDefeated: true }] }; const wrapper = mount(InitiativeTracker); expect(wrapper.get('.initiative-list li').classes()).toContain('defeated') })
  it('blocks encounter start while hit points are missing', () => {
    store.snapshot.activeEncounter = {
      id: 'encounter-1', status: 'Preparing', round: 0, currentTurnIndex: null,
      combatants: [{ id: 'combatant-1', participantId: null, name: 'Goblin', initiativeModifier: 2, initiative: 14, creationOrder: 0, isCurrent: false, currentHitPoints: null }]
    }
    const wrapper = mount(EncounterSetup)
    expect(wrapper.get('#new-combatant-hit-points').exists()).toBe(true)
    expect(wrapper.get('button.button.primary').attributes('disabled')).toBeDefined()
  })
  it('lets a linked player update only own hit points', async () => {
    store.isMaster = false
    store.session = { participantId: 'p1' }
    const active = { id: 'encounter-1', status: 'Active', round: 1, currentTurnIndex: 0, combatants: [{ id: 'combatant-1', participantId: 'p1', name: 'Lia', initiativeModifier: 2, initiative: 14, creationOrder: 0, isCurrent: true, currentHitPoints: 20 }] }
    store.snapshot.activeEncounter = active
    const api = useCompanionApi()
    api.updateHitPoints.mockResolvedValue(active)
    const wrapper = mount(InitiativeTracker)
    await wrapper.get('#own-combat-hit-points-combatant-1').setValue('17')
    await wrapper.get('[aria-label="Salvar vida de Lia"]').trigger('click')
    expect(updateHitPoints).toHaveBeenCalledWith(store.session, 'encounter-1', 'combatant-1', 17)
    store.isMaster = true
  })})
