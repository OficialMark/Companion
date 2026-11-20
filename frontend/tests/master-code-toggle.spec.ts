import { mount } from '@vue/test-utils'
import MasterCodeToggle from '~/components/MasterCodeToggle.vue'

const store = { isMaster: true, session: { masterCode: 'M-4F2KQZ' } as { masterCode?: string } | null }
vi.stubGlobal('useCampaignStore', () => store)

beforeEach(() => { store.isMaster = true; store.session = { masterCode: 'M-4F2KQZ' } })

describe('MasterCodeToggle (spec 012, FR-009)', () => {
  it('starts masked and the eye shows and hides the code', async () => {
    const wrapper = mount(MasterCodeToggle)
    const value = () => wrapper.get('[data-master-code-value]').text()
    const eye = wrapper.get('[data-master-code-eye]')

    expect(value()).not.toContain('M-4F2KQZ')
    expect(eye.attributes('aria-pressed')).toBe('false')
    expect(eye.attributes('aria-label')).toBe('Mostrar código do mestre')

    await eye.trigger('click')
    expect(value()).toBe('M-4F2KQZ')
    expect(eye.attributes('aria-pressed')).toBe('true')
    // One fixed label; the state is aria-pressed (critic 012: a changing label plus aria-pressed contradicts itself).
    expect(eye.attributes('aria-label')).toBe('Mostrar código do mestre')

    await eye.trigger('click')
    expect(value()).not.toContain('M-4F2KQZ')
  })

  it('renders nothing for a player, even if a code were present', () => {
    store.isMaster = false
    expect(mount(MasterCodeToggle).find('[data-master-code]').exists()).toBe(false)
  })
})
