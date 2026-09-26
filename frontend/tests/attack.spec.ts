import { mount } from '@vue/test-utils'
import CombatAttack from '~/components/CombatAttack.vue'
import AttackConfirmModal from '~/components/AttackConfirmModal.vue'
import DamageRollModal from '~/components/DamageRollModal.vue'
const resolveAttack = vi.fn()
const encounter = { id: 'e1', status: 'Active', round: 1, currentTurnIndex: 0, combatants: [
  { id: 'c1', participantId: 'p1', name: 'Lia', initiativeModifier: 2, initiative: 15, creationOrder: 0, isCurrent: true, currentHitPoints: 20 },
  { id: 'c2', participantId: null, name: 'Goblin', initiativeModifier: 1, initiative: 12, creationOrder: 1, isCurrent: false, currentHitPoints: null }
] }
const updateActiveEncounter = vi.fn()
const store = { isMaster: false, session: { participantId: 'p1' }, connection: 'online', snapshot: { activeEncounter: encounter }, updateActiveEncounter }
vi.stubGlobal('useCampaignStore', () => store)
vi.stubGlobal('useCompanionApi', () => ({ resolveAttack }))
describe('CombatAttack', () => {
  beforeEach(() => { resolveAttack.mockReset(); updateActiveEncounter.mockReset() })
  it('rolls before selecting multiple targets and applies on confirmation', async () => {
    resolveAttack.mockResolvedValue({ attack: { id: 'a1', damage: 7, targets: [] }, encounter })
    const wrapper = mount(CombatAttack, { global: { stubs: { DiceRoller: { template: '<button data-testid="finish-roll" @click="$emit(\'rolled\', { id: \'r1\', total: 7, results: [{ total: 7 }] })">Finalizar rolagem</button>' } } } })
    await wrapper.get('button.attack-open').trigger('click'); expect(wrapper.findAll('input[type="checkbox"]')).toHaveLength(0)
    await wrapper.get('[data-testid="finish-roll"]').trigger('click'); expect(wrapper.findAll('input[type="checkbox"]')).toHaveLength(2)
    await wrapper.findAll('input[type="checkbox"]')[0]!.setValue(true); await wrapper.findAll('input[type="checkbox"]')[1]!.setValue(true)
    await wrapper.get('button.attack-confirm').trigger('click'); await Promise.resolve()
    expect(resolveAttack).toHaveBeenCalledWith(store.session, 'e1', 'r1', ['c1', 'c2'], expect.any(String))
    expect(updateActiveEncounter).toHaveBeenCalledWith(encounter)
  })
  it('cancels without applying damage', async () => { const wrapper = mount(CombatAttack, { global: { stubs: { DiceRoller: true } } }); await wrapper.get('button.attack-open').trigger('click'); await wrapper.get('button.attack-cancel').trigger('click'); expect(resolveAttack).not.toHaveBeenCalled() })
})

describe('AttackConfirmModal', () => {
  const roll = { id: 'r1', total: 18, results: [{ total: 18 }] }

  it('emits hit when the user confirms the attack landed', async () => {
    const wrapper = mount(AttackConfirmModal, { props: { roll } })
    expect(wrapper.text()).toContain('18')
    await wrapper.get('button.attack-hit-confirm').trigger('click')
    expect(wrapper.emitted('hit')?.[0]).toEqual([roll])
    wrapper.unmount()
  })

  it('emits dismissed when the user says the attack missed', async () => {
    const wrapper = mount(AttackConfirmModal, { props: { roll } })
    await wrapper.get('button.attack-hit-deny').trigger('click')
    expect(wrapper.emitted('dismissed')).toHaveLength(1)
    expect(wrapper.emitted('hit')).toBeUndefined()
    wrapper.unmount()
  })

  it('auto-dismisses after 10s without a response, never emitting hit', async () => {
    vi.useFakeTimers()
    const wrapper = mount(AttackConfirmModal, { props: { roll } })
    try {
      await vi.advanceTimersByTimeAsync(9999)
      expect(wrapper.emitted('dismissed')).toBeUndefined()
      await vi.advanceTimersByTimeAsync(1)
      expect(wrapper.emitted('dismissed')).toHaveLength(1)
      expect(wrapper.emitted('hit')).toBeUndefined()
    } finally { wrapper.unmount(); vi.useRealTimers() }
  })
})

describe('DamageRollModal', () => {
  beforeEach(() => { resolveAttack.mockReset(); updateActiveEncounter.mockReset() })

  it('rolls damage, lets the user pick a target, and applies it via resolveAttack', async () => {
    resolveAttack.mockResolvedValue({ attack: { id: 'a1', damage: 9, targets: [] }, encounter })
    const wrapper = mount(DamageRollModal, { global: { stubs: { DiceRoller: { template: '<button data-testid="finish-roll" @click="$emit(\'rolled\', { id: \'r2\', total: 9, results: [{ total: 9 }] })">Finalizar rolagem</button>' } } } })
    await wrapper.get('[data-testid="finish-roll"]').trigger('click')
    const targets = wrapper.findAll('input[type="checkbox"]')
    expect(targets).toHaveLength(2)
    await targets[0]!.setValue(true)
    await wrapper.get('button.attack-confirm').trigger('click'); await Promise.resolve()
    expect(resolveAttack).toHaveBeenCalledWith(store.session, 'e1', 'r2', ['c1'], expect.any(String))
    expect(wrapper.emitted('resolved')).toHaveLength(1)
    wrapper.unmount()
  })

  it('cancelling emits cancelled without applying damage', async () => {
    const wrapper = mount(DamageRollModal, { global: { stubs: { DiceRoller: true } } })
    await wrapper.get('button.attack-cancel').trigger('click')
    expect(resolveAttack).not.toHaveBeenCalled()
    expect(wrapper.emitted('cancelled')).toHaveLength(1)
    wrapper.unmount()
  })
})