import { mount } from '@vue/test-utils'
import InitiativeModifier from '~/components/InitiativeModifier.vue'

const updateCombatProfile = vi.fn()
const participant = { id: 'p1', displayName: 'Lia', role: 'Player', isOnline: true, initiativeModifier: 2, currentHitPoints: 18, maxHitPoints: 20 }
const store = {
  isMaster: false,
  session: { participantId: 'p1', role: 'Player' },
  connection: 'online',
  snapshot: { currentParticipant: { ...participant }, participants: [{ ...participant }] }
}
vi.stubGlobal('useCampaignStore', () => store)
vi.stubGlobal('useCompanionApi', () => ({ updateCombatProfile }))

describe('InitiativeModifier', () => {
  it('loads and persists current player initiative and hit points', async () => {
    updateCombatProfile.mockResolvedValue({ ...participant, initiativeModifier: 5, currentHitPoints: 24, maxHitPoints: 30 })
    const wrapper = mount(InitiativeModifier)
    expect((wrapper.get('#own-initiative-modifier').element as HTMLInputElement).value).toBe('2')
    expect((wrapper.get('#own-hit-points').element as HTMLInputElement).value).toBe('18')
    expect((wrapper.get('#own-max-hit-points').element as HTMLInputElement).value).toBe('20')
    await wrapper.get('#own-initiative-modifier').setValue('5')
    await wrapper.get('#own-hit-points').setValue('24')
    await wrapper.get('#own-max-hit-points').setValue('30')
    await wrapper.get('form').trigger('submit')
    await Promise.resolve()

    expect(updateCombatProfile).toHaveBeenCalledWith(store.session, 5, 24, 30)
    expect(wrapper.text()).toContain('Perfil de combate salvo.')
  })

  it('is not rendered for a master', () => {
    store.isMaster = true
    const wrapper = mount(InitiativeModifier)
    expect(wrapper.find('section').exists()).toBe(false)
    store.isMaster = false
  })
})