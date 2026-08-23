import { mount } from '@vue/test-utils'
import EncounterSetup from '~/components/EncounterSetup.vue'
import InitiativeTracker from '~/components/InitiativeTracker.vue'

const rollInitiative = vi.fn()
const store = { isMaster: true, session: {} as Record<string, unknown>, snapshot: { activeEncounter: null } as { activeEncounter: null | Record<string, unknown> } }
vi.stubGlobal('useCampaignStore', () => store)
vi.stubGlobal('useCompanionApi', () => ({ createEncounter: vi.fn(), rollInitiative, nextTurn: vi.fn(), endEncounter: vi.fn() }))

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
      combatants: [{ id: 'combatant-1', name: 'Goblin', initiativeModifier: 2, initiative: 14, creationOrder: 0, isCurrent: true }]
    }
    store.snapshot.activeEncounter = encounter
    rollInitiative.mockResolvedValue(encounter)

    const wrapper = mount(InitiativeTracker)
    await wrapper.get('[aria-label="Rolar iniciativa novamente para Goblin"]').trigger('click')

    expect(rollInitiative).toHaveBeenCalledWith(store.session, 'encounter-1', 'combatant-1')
  })
})
