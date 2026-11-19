import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useCampaignStore } from '~/stores/campaign'
import UserMenu from '~/components/UserMenu.vue'
import BattlemapToken from '~/components/battlemap/BattlemapToken.vue'
import type { Participant, Session } from '~/types/campaign'

vi.mock('~/utils/avatarImage', () => ({
  AVATAR_ACCEPT: 'image/png,image/jpeg,image/webp',
  prepareAvatar: vi.fn(async () => ({ blob: new Blob(['x'], { type: 'image/webp' }), fileName: 'avatar.webp' }))
}))

const session: Session = { campaignId: 'c', playerCode: '1234', masterCode: null, participantId: 'p1', displayName: 'Lia', role: 'Player', participantToken: 'token' }
const player = (overrides: Partial<Participant> = {}): Participant => ({ id: 'p1', displayName: 'Lia', role: 'Player', isOnline: true, initiativeModifier: 2, currentHitPoints: 20, avatarRevision: null, ...overrides })

let objectUrls = 0
const participantAvatar = vi.fn(async () => new Blob(['img'], { type: 'image/webp' }))
const uploadAvatar = vi.fn()
const removeAvatar = vi.fn()

function setupStore(current = player()) {
  setActivePinia(createPinia())
  const store = useCampaignStore()
  store.session = session
  store.connection = 'online'
  store.snapshot = { campaign: { id: 'c', playerCode: '1234', name: 'Mesa' }, currentParticipant: current, participants: [{ ...current, currentHitPoints: null }], diceRolls: [], activeEncounter: null }
  return store
}

beforeEach(() => {
  objectUrls = 0
  participantAvatar.mockClear(); uploadAvatar.mockReset(); removeAvatar.mockReset()
  vi.stubGlobal('useCompanionApi', () => ({ participantAvatar, uploadAvatar, removeAvatar }))
  vi.stubGlobal('useCampaignStore', useCampaignStore)
  URL.createObjectURL = vi.fn(() => `blob:avatar-${++objectUrls}`)
  URL.revokeObjectURL = vi.fn()
})

describe('avatar store sync', () => {
  it('loads photos by revision, reuses unchanged ones and drops removed ones', async () => {
    const store = setupStore(player({ avatarRevision: 'r1' }))
    await store.syncAvatars()
    expect(participantAvatar).toHaveBeenCalledWith(session, 'p1', 'r1')
    expect(store.avatarUrls).toEqual({ p1: 'blob:avatar-1' })

    await store.syncAvatars()
    expect(participantAvatar).toHaveBeenCalledTimes(1)

    store.snapshot!.participants[0]!.avatarRevision = null
    store.snapshot!.currentParticipant.avatarRevision = null
    await store.syncAvatars()
    expect(store.avatarUrls).toEqual({})
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:avatar-1')
  })
})

describe('UserMenu', () => {
  it('opens the menu, uploads a cropped photo and shows it in the trigger', async () => {
    const store = setupStore()
    uploadAvatar.mockResolvedValue(player({ avatarRevision: 'r2' }))
    const wrapper = mount(UserMenu, { attachTo: document.body })
    expect(wrapper.find('[role="menu"]').exists()).toBe(false)

    await wrapper.get('.user-menu-trigger').trigger('click')
    expect(wrapper.get('.user-menu-trigger').attributes('aria-expanded')).toBe('true')
    expect(wrapper.text()).toContain('Enviar foto')
    expect(wrapper.text()).not.toContain('Remover foto')

    const input = wrapper.get('input[type="file"]').element as HTMLInputElement
    Object.defineProperty(input, 'files', { value: [new File(['x'], 'eu.png', { type: 'image/png' })], configurable: true })
    await input.dispatchEvent(new Event('change'))
    await vi.waitFor(() => expect(store.avatarUrls.p1).toBe('blob:avatar-1'))

    expect(uploadAvatar).toHaveBeenCalledWith(session, expect.any(Blob), 'avatar.webp')
    expect(store.snapshot!.currentParticipant.avatarRevision).toBe('r2')
    expect(store.snapshot!.participants[0]!.avatarRevision).toBe('r2')
    await nextTick()
    expect(wrapper.get('.user-menu-trigger img').attributes('src')).toBe('blob:avatar-1')
    wrapper.unmount()
  })

  it('removes an existing photo and emits leave from the menu', async () => {
    const store = setupStore(player({ avatarRevision: 'r1' }))
    removeAvatar.mockResolvedValue(player({ avatarRevision: null }))
    const wrapper = mount(UserMenu, { attachTo: document.body })
    await wrapper.get('.user-menu-trigger').trigger('click')
    expect(wrapper.text()).toContain('Trocar foto')

    const remove = wrapper.findAll('.user-menu-item').find(item => item.text().includes('Remover foto'))!
    await remove.trigger('click')
    await vi.waitFor(() => expect(removeAvatar).toHaveBeenCalledWith(session))
    expect(store.snapshot!.currentParticipant.avatarRevision).toBeNull()

    await wrapper.get('.user-menu-trigger').trigger('click')
    await wrapper.get('.user-menu-item.danger').trigger('click')
    expect(wrapper.emitted('leave')).toHaveLength(1)
    wrapper.unmount()
  })

  it('closes with Escape and disables photo changes while offline', async () => {
    const store = setupStore()
    store.connection = 'offline'
    const wrapper = mount(UserMenu, { attachTo: document.body })
    await wrapper.get('.user-menu-trigger').trigger('click')
    expect(wrapper.get('.user-menu-item').attributes('disabled')).toBeDefined()
    expect(wrapper.text()).toContain('Conecte-se para alterar a foto')

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    await nextTick()
    expect(wrapper.find('[role="menu"]').exists()).toBe(false)
    wrapper.unmount()
  })
})

describe('BattlemapToken photo', () => {
  const token = { participantId: 'p1', displayName: 'Lia', initials: 'L', colorKey: 1, column: 0, row: 0, positionVersion: 'v1' }
  const base = { token, x: 20, y: 20, size: 40, selected: false, controllable: true, pending: false }

  it('renders the photo clipped to the token circle instead of the initials', () => {
    const wrapper = mount(BattlemapToken, { props: { ...base, photo: 'blob:avatar-1' } })
    const image = wrapper.get('image.token-photo')
    expect(image.attributes('href')).toBe('blob:avatar-1')
    expect(image.attributes('clip-path')).toBe('url(#token-photo-p1)')
    expect(wrapper.find('clipPath#token-photo-p1').exists()).toBe(true)
    expect(wrapper.find('text').exists()).toBe(false)
  })

  it('keeps the initials when there is no photo', () => {
    const wrapper = mount(BattlemapToken, { props: base })
    expect(wrapper.find('image').exists()).toBe(false)
    expect(wrapper.get('text').text()).toBe('L')
  })
})
