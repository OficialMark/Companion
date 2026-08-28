import { mount } from '@vue/test-utils'
import CampaignAccess from '~/components/CampaignAccess.vue'
const setSession = vi.fn(); const createCampaign = vi.fn(); const joinCampaign = vi.fn()
vi.mock('#app', () => ({ useRuntimeConfig: () => ({ public: { apiBase: '/api' } }) }))
vi.stubGlobal('useRuntimeConfig', () => ({ public: { apiBase: '/api' } }))
vi.stubGlobal('useCampaignStore', () => ({ setSession }))
vi.stubGlobal('useCompanionApi', () => ({ createCampaign, joinCampaign }))
describe('CampaignAccess', () => {
  it('shows create fields and lets the user switch to join mode', async () => { const wrapper = mount(CampaignAccess); expect(wrapper.text()).toContain('Nome da campanha'); await wrapper.get('[role="tablist"] button:nth-child(2)').trigger('click'); expect(wrapper.text()).toContain('Código da campanha'); const code = wrapper.get('input.code-input'); expect(code.attributes('maxlength')).toBe('4'); expect(code.attributes('pattern')).toBe('[0-9]{4}') })
  it('preserves the player role and hidden master code returned by join', async () => { const session = { campaignId: 'c1', playerCode: '0123', masterCode: null, participantId: 'p1', displayName: 'Lia', role: 'Player', participantToken: 'token' }; joinCampaign.mockResolvedValue(session); const wrapper = mount(CampaignAccess); await wrapper.get('[role="tablist"] button:nth-child(2)').trigger('click'); const inputs = wrapper.findAll('input'); await inputs[0]!.setValue('0123'); await inputs[1]!.setValue('Lia'); await wrapper.get('form').trigger('submit'); await Promise.resolve(); await Promise.resolve(); expect(joinCampaign).toHaveBeenCalledWith('0123', 'Lia'); expect(setSession).toHaveBeenCalledWith(session) })
})