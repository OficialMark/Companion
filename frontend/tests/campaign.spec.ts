import { mount } from '@vue/test-utils'
import CampaignAccess from '~/components/CampaignAccess.vue'

vi.mock('#app', () => ({ useRuntimeConfig: () => ({ public: { apiBase: '/api' } }) }))
vi.stubGlobal('useRuntimeConfig', () => ({ public: { apiBase: '/api' } }))
vi.stubGlobal('useCampaignStore', () => ({ setSession: vi.fn() }))
vi.stubGlobal('useCompanionApi', () => ({ createCampaign: vi.fn(), joinCampaign: vi.fn() }))

describe('CampaignAccess', () => {
  it('shows create fields and lets the user switch to join mode', async () => {
    const wrapper = mount(CampaignAccess)
    expect(wrapper.text()).toContain('Nome da campanha')
    await wrapper.get('[role="tablist"] button:nth-child(2)').trigger('click')
    expect(wrapper.text()).toContain('Código da campanha')
    const code = wrapper.get('input.code-input')
    expect(code.attributes('maxlength')).toBe('4')
    expect(code.attributes('pattern')).toBe('[0-9]{4}')
  })
})
