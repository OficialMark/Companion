import { mount } from '@vue/test-utils'
import InitiativeModifier from '~/components/InitiativeModifier.vue'

const updateInitiativeModifier = vi.fn()
const participant = { id: 'p1', displayName: 'Lia', role: 'Player', isOnline: true, initiativeModifier: 2 }
const store = {
  session: { participantId: 'p1' },
  connection: 'online',
  snapshot: { currentParticipant: { ...participant }, participants: [{ ...participant }] }
}
vi.stubGlobal('useCampaignStore', () => store)
vi.stubGlobal('useCompanionApi', () => ({ updateInitiativeModifier }))

describe('InitiativeModifier', () => {
  it('loads and persists only the current participant modifier', async () => {
    updateInitiativeModifier.mockResolvedValue({ ...participant, initiativeModifier: 5 })
    const wrapper = mount(InitiativeModifier)
    const input = wrapper.get('#own-initiative-modifier')
    expect((input.element as HTMLInputElement).value).toBe('2')
    await input.setValue('5')
    await wrapper.get('form').trigger('submit')
    await Promise.resolve()

    expect(updateInitiativeModifier).toHaveBeenCalledWith(store.session, 5)
    expect(wrapper.text()).toContain('Modificador salvo.')
  })
})
