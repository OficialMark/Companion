import { mount } from '@vue/test-utils'
import AdminPage from '~/pages/admin.vue'

const listAdminCampaigns = vi.fn()
const deleteAdminCampaign = vi.fn()
vi.stubGlobal('useCompanionApi', () => ({ listAdminCampaigns, deleteAdminCampaign }))

describe('AdminPage', () => {
  it('reveals no campaign before a valid secret and renders returned codes', async () => {
    listAdminCampaigns.mockResolvedValue([{
      id: 'c1', name: 'Mesa secreta', createdAt: new Date().toISOString(), participantCount: 2,
      onlineCount: 1, playerCode: '0123', masterCode: '9876'
    }])
    const wrapper = mount(AdminPage)
    expect(wrapper.text()).not.toContain('Mesa secreta')
    await wrapper.get('input[type="password"]').setValue('secret')
    await wrapper.get('form').trigger('submit')
    await Promise.resolve()

    expect(wrapper.text()).toContain('Mesa secreta')
    expect(wrapper.text()).toContain('0123')
    expect(wrapper.text()).toContain('9876')
  })

  it('requires explicit confirmation before deleting', async () => {
    listAdminCampaigns.mockResolvedValue([{
      id: 'c1', name: 'Mesa descartável', createdAt: new Date().toISOString(), participantCount: 1,
      onlineCount: 0, playerCode: '1111', masterCode: '2222'
    }])
    vi.spyOn(window, 'confirm').mockReturnValue(false)
    const wrapper = mount(AdminPage)
    await wrapper.get('input[type="password"]').setValue('secret')
    await wrapper.get('form').trigger('submit')
    await Promise.resolve()
    await wrapper.get('.admin-table .danger').trigger('click')

    expect(deleteAdminCampaign).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Mesa descartável')
  })
})
