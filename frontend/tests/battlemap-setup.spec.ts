import { mount } from '@vue/test-utils'
import BattlemapSetup from '~/components/battlemap/BattlemapSetup.vue'

const configureBattlemap = vi.fn()
vi.stubGlobal('useCampaignStore', () => ({
  session: { campaignId: 'campaign-1', participantToken: 'token' },
  connection: 'online'
}))
vi.stubGlobal('useCompanionApi', () => ({ configureBattlemap }))

describe('BattlemapSetup', () => {
  it('shows a grid preview and validates dimensions', async () => {
    const wrapper = mount(BattlemapSetup, { props: { encounterId: 'encounter-1', battlemap: null } })
    expect(wrapper.text()).toContain('Configurar battlemap')
    await wrapper.get('[aria-label="Colunas do grid"]').setValue('101')
    await wrapper.get('form').trigger('submit')
    expect(wrapper.text()).toContain('entre 1 e 100')
    expect(configureBattlemap).not.toHaveBeenCalled()
  })
})
