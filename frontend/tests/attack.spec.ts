import { mount } from '@vue/test-utils'
import CombatAttack from '~/components/CombatAttack.vue'
const resolveAttack = vi.fn()
const encounter = { id: 'e1', status: 'Active', round: 1, currentTurnIndex: 0, combatants: [
  { id: 'c1', participantId: 'p1', name: 'Lia', initiativeModifier: 2, initiative: 15, creationOrder: 0, isCurrent: true, currentHitPoints: 20 },
  { id: 'c2', participantId: null, name: 'Goblin', initiativeModifier: 1, initiative: 12, creationOrder: 1, isCurrent: false, currentHitPoints: null }
] }
const store = { isMaster: false, session: { participantId: 'p1' }, connection: 'online', snapshot: { activeEncounter: encounter } }
vi.stubGlobal('useCampaignStore', () => store)
vi.stubGlobal('useCompanionApi', () => ({ resolveAttack }))
describe('CombatAttack', () => {
  beforeEach(() => resolveAttack.mockReset())
  it('rolls before selecting multiple targets and applies on confirmation', async () => {
    resolveAttack.mockResolvedValue({ attack: { id: 'a1', damage: 7, targets: [] }, encounter })
    const wrapper = mount(CombatAttack, { global: { stubs: { DiceRoller: { template: '<button data-testid="finish-roll" @click="$emit(\'rolled\', { id: \'r1\', total: 7, results: [{ total: 7 }] })">Finalizar rolagem</button>' } } } })
    await wrapper.get('button.attack-open').trigger('click'); expect(wrapper.findAll('input[type="checkbox"]')).toHaveLength(0)
    await wrapper.get('[data-testid="finish-roll"]').trigger('click'); expect(wrapper.findAll('input[type="checkbox"]')).toHaveLength(2)
    await wrapper.findAll('input[type="checkbox"]')[0]!.setValue(true); await wrapper.findAll('input[type="checkbox"]')[1]!.setValue(true)
    await wrapper.get('button.attack-confirm').trigger('click'); await Promise.resolve()
    expect(resolveAttack).toHaveBeenCalledWith(store.session, 'e1', 'r1', ['c1', 'c2'], expect.any(String))
  })
  it('cancels without applying damage', async () => { const wrapper = mount(CombatAttack, { global: { stubs: { DiceRoller: true } } }); await wrapper.get('button.attack-open').trigger('click'); await wrapper.get('button.attack-cancel').trigger('click'); expect(resolveAttack).not.toHaveBeenCalled() })
})