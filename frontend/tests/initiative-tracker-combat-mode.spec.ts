import { mount } from '@vue/test-utils'
import InitiativeTracker from '~/components/InitiativeTracker.vue'
import { useGameMode } from '~/composables/useGameMode'

const encounter = {
  id: 'e1', status: 'Active', round: 1, currentTurnIndex: 0,
  combatants: [
    { id: 'c1', participantId: 'p1', name: 'Lia', initiativeModifier: 2, initiative: 15, creationOrder: 0, isCurrent: true, currentHitPoints: 20 },
    { id: 'c2', participantId: null, name: 'Goblin', initiativeModifier: 1, initiative: 12, creationOrder: 1, isCurrent: false, currentHitPoints: 8 }
  ]
}

function buildStore(isMaster: boolean) {
  return {
    isMaster,
    session: { participantId: 'p1' },
    connection: 'online',
    snapshot: { activeEncounter: encounter },
    damageEffects: {},
    clearDamageEffect: vi.fn()
  }
}

describe('InitiativeTracker respects game mode for HP/initiative editing', () => {
  afterEach(() => { useGameMode().exit() })

  it('shows the master editor (name/modifier/HP) and the player own-HP editor in setup mode', () => {
    const store = buildStore(true)
    vi.stubGlobal('useCampaignStore', () => store)
    vi.stubGlobal('useCompanionApi', () => ({}))
    const wrapper = mount(InitiativeTracker)

    expect(wrapper.find('.combatant-editor').exists()).toBe(true)
    wrapper.unmount()
  })

  it('hides the editable HP/initiative fields once game mode is active, showing read-only values instead', () => {
    useGameMode().enter()
    const store = buildStore(true)
    vi.stubGlobal('useCampaignStore', () => store)
    vi.stubGlobal('useCompanionApi', () => ({}))
    const wrapper = mount(InitiativeTracker)

    expect(wrapper.find('.combatant-editor').exists()).toBe(false)
    expect(wrapper.find('.combatant-readonly').exists()).toBe(true)
    expect(wrapper.text()).toContain('Vida 8')
    wrapper.unmount()
  })

  it('hides the player own-HP editable input during game mode, showing read-only HP instead', () => {
    useGameMode().enter()
    const store = buildStore(false)
    vi.stubGlobal('useCampaignStore', () => store)
    vi.stubGlobal('useCompanionApi', () => ({}))
    const wrapper = mount(InitiativeTracker)

    expect(wrapper.find('.own-hit-points').exists()).toBe(false)
    expect(wrapper.find('.own-hit-points-readonly').exists()).toBe(true)
    expect(wrapper.text()).toContain('Vida 20')
    wrapper.unmount()
  })
})
