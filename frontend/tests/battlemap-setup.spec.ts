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

  it('warns before shrinking a grid that evicts placed tokens and requires confirmation', async () => {
    const battlemap = {
      id: 'map-1', encounterId: 'encounter-1', columns: 20, rows: 15, isGridVisible: true,
      revision: 'rev-1', imageRevision: 'img-1', imageUrl: 'blob:existing', imageMediaType: 'image/png',
      imageFileName: 'map.png', updatedAt: new Date().toISOString(),
      tokens: [{ participantId: 'p1', displayName: 'Jogador Um', initials: 'JU', colorKey: 1, column: 8, row: 6, positionVersion: 'v1' }]
    }
    const wrapper = mount(BattlemapSetup, { props: { encounterId: 'encounter-1', battlemap } })
    await wrapper.get('[aria-label="Colunas do grid"]').setValue('5')
    const fileInput = wrapper.get('input[type="file"]').element as HTMLInputElement
    Object.defineProperty(fileInput, 'files', { value: [new File(['x'], 'map.png', { type: 'image/png' })], configurable: true })
    await fileInput.dispatchEvent(new Event('change'))

    await wrapper.get('form').trigger('submit')
    expect(wrapper.text()).toContain('Jogador Um')
    expect(wrapper.text()).toContain('fora do novo grid')
    expect(configureBattlemap).not.toHaveBeenCalled()

    await wrapper.get('form').trigger('submit')
    expect(configureBattlemap).toHaveBeenCalled()
  })

  it('adjusts columns/rows to match the uploaded image aspect ratio, avoiding an unwanted crop', async () => {
    const originalImage = globalThis.Image
    class FakeImage {
      onload: (() => void) | null = null
      naturalWidth = 800
      naturalHeight = 1800
      set src(_value: string) { queueMicrotask(() => this.onload?.()) }
    }
    vi.stubGlobal('Image', FakeImage)

    const wrapper = mount(BattlemapSetup, { props: { encounterId: 'encounter-1', battlemap: null } })
    const fileInput = wrapper.get('input[type="file"]').element as HTMLInputElement
    Object.defineProperty(fileInput, 'files', { value: [new File(['x'], 'tall.png', { type: 'image/png' })], configurable: true })
    await fileInput.dispatchEvent(new Event('change'))
    await new Promise(resolve => queueMicrotask(resolve))
    await nextTick()

    // 800x1800 = 4:9 -> 8x18 matches the image exactly (no stretch, no crop)
    expect((wrapper.get('[aria-label="Colunas do grid"]').element as HTMLInputElement).value).toBe('8')
    expect((wrapper.get('[aria-label="Linhas do grid"]').element as HTMLInputElement).value).toBe('18')

    vi.stubGlobal('Image', originalImage)
  })

  it('reproduces a real tall dungeon map (499x1183) suggesting an 8x19 grid that matches its proportion within 0.2%', async () => {
    const originalImage = globalThis.Image
    class FakeImage {
      onload: (() => void) | null = null
      naturalWidth = 499
      naturalHeight = 1183
      set src(_value: string) { queueMicrotask(() => this.onload?.()) }
    }
    vi.stubGlobal('Image', FakeImage)

    const wrapper = mount(BattlemapSetup, { props: { encounterId: 'encounter-1', battlemap: null } })
    const fileInput = wrapper.get('input[type="file"]').element as HTMLInputElement
    Object.defineProperty(fileInput, 'files', { value: [new File(['x'], 'dungeon.png', { type: 'image/png' })], configurable: true })
    await fileInput.dispatchEvent(new Event('change'))
    await new Promise(resolve => queueMicrotask(resolve))
    await nextTick()

    expect((wrapper.get('[aria-label="Colunas do grid"]').element as HTMLInputElement).value).toBe('8')
    expect((wrapper.get('[aria-label="Linhas do grid"]').element as HTMLInputElement).value).toBe('19')

    vi.stubGlobal('Image', originalImage)
  })

  it.each([
    [1920, 1080],
    [4000, 500],
    [500, 4000],
    [1000, 1000],
    [3508, 2480]
  ])('suggests a grid within 3%% of the %ix%i image proportion', async (width, height) => {
    const originalImage = globalThis.Image
    class FakeImage {
      onload: (() => void) | null = null
      naturalWidth = width
      naturalHeight = height
      set src(_value: string) { queueMicrotask(() => this.onload?.()) }
    }
    vi.stubGlobal('Image', FakeImage)
    const wrapper = mount(BattlemapSetup, { props: { encounterId: 'encounter-1', battlemap: null } })
    const fileInput = wrapper.get('input[type="file"]').element as HTMLInputElement
    Object.defineProperty(fileInput, 'files', { value: [new File(['x'], 'map.png', { type: 'image/png' })], configurable: true })
    await fileInput.dispatchEvent(new Event('change'))
    await new Promise(resolve => queueMicrotask(resolve))
    await nextTick()

    const columns = Number((wrapper.get('[aria-label="Colunas do grid"]').element as HTMLInputElement).value)
    const rows = Number((wrapper.get('[aria-label="Linhas do grid"]').element as HTMLInputElement).value)
    expect(Math.abs((columns / rows) / (width / height) - 1)).toBeLessThan(.03)
    expect(wrapper.find('.battlemap-ratio-warning').exists()).toBe(false)
    vi.stubGlobal('Image', originalImage)
  })

  it('warns when a manual grid would stretch the image and can match rows back to it', async () => {
    const originalImage = globalThis.Image
    class FakeImage {
      onload: (() => void) | null = null
      naturalWidth = 1600
      naturalHeight = 900
      set src(_value: string) { queueMicrotask(() => this.onload?.()) }
    }
    vi.stubGlobal('Image', FakeImage)
    const wrapper = mount(BattlemapSetup, { props: { encounterId: 'encounter-1', battlemap: null } })
    const fileInput = wrapper.get('input[type="file"]').element as HTMLInputElement
    Object.defineProperty(fileInput, 'files', { value: [new File(['x'], 'map.png', { type: 'image/png' })], configurable: true })
    await fileInput.dispatchEvent(new Event('change'))
    await new Promise(resolve => queueMicrotask(resolve))
    await nextTick()

    await wrapper.get('[aria-label="Colunas do grid"]').setValue('20')
    await wrapper.get('[aria-label="Linhas do grid"]').setValue('20')
    expect(wrapper.find('.battlemap-ratio-warning').exists()).toBe(true)
    await wrapper.get('.battlemap-ratio-warning .button.ghost').trigger('click')
    expect((wrapper.get('[aria-label="Linhas do grid"]').element as HTMLInputElement).value).toBe('11')
    expect(wrapper.find('.battlemap-ratio-warning').exists()).toBe(false)
    vi.stubGlobal('Image', originalImage)
  })
})
