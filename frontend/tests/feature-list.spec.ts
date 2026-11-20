import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useCampaignStore } from '~/stores/campaign'
import { useCharacterStore } from '~/stores/character'
import FeatureList from '~/components/character/FeatureList.vue'
import type { CharacterFeature, CharacterFeatureData } from '~/types/character'
import { makeCharacter } from './character-fixture'

// Spec 013, US2: features with a use counter. The list sends deltas and rests to the server and shows
// what the server answers; it never computes the counter itself.
const api = {
  addFeature: vi.fn(), saveFeature: vi.fn(), removeFeature: vi.fn(), spendFeature: vi.fn(),
  restoreFeature: vi.fn(), moveFeature: vi.fn(), rest: vi.fn()
}
vi.stubGlobal('useCharacterApi', () => api)
vi.stubGlobal('useCampaignStore', () => useCampaignStore())
vi.stubGlobal('useCharacterStore', () => useCharacterStore())

function feature(id: string, sortOrder: number, data: Partial<CharacterFeatureData> = {}): CharacterFeature {
  return { id, sortOrder, data: { name: id, source: 'Paladino 5', description: null, maxUses: null, currentUses: 0, recovery: 'None', ...data } }
}
const withFeatures = (features: CharacterFeature[]) => makeCharacter({ features })

beforeEach(() => {
  setActivePinia(createPinia())
  useCampaignStore().session = { campaignId: 'c1', playerCode: '1234', masterCode: null, participantId: 'p1', displayName: 'Vash', role: 'Player', participantToken: 'tok' } as never
  for (const fn of Object.values(api)) fn.mockReset()
})

describe('Características', () => {
  it('mostra o vazio e esconde os descansos quando não há contador', () => {
    useCharacterStore().apply(withFeatures([]), { resetDraft: true })
    const wrapper = mount(FeatureList)
    expect(wrapper.find('[data-feature-empty]').exists()).toBe(true)
    expect(wrapper.find('[data-rest]').exists()).toBe(false)
  })

  it('mostra contador, origem e recuperação, na ordem do servidor', () => {
    useCharacterStore().apply(withFeatures([
      feature('Sentido Divino', 1, { maxUses: 5, currentUses: 3, recovery: 'LongRest' }),
      feature('Destruição Divina', 0)
    ]), { resetDraft: true })
    const wrapper = mount(FeatureList)
    const rows = wrapper.findAll('[data-feature-id]')
    expect(rows.map(row => row.attributes('data-feature-id'))).toEqual(['Destruição Divina', 'Sentido Divino'])
    expect(rows[0]!.find('[data-feature-counter]').exists()).toBe(false)
    expect(rows[1]!.get('[data-feature-uses]').text()).toBe('3/5')
    expect(rows[1]!.get('[data-feature-uses]').attributes('aria-label')).toBe('3 de 5 usos')
    expect(rows[1]!.get('[data-feature-recovery]').text()).toBe('Descanso longo')
    expect(rows[1]!.get('[data-feature-source]').text()).toBe('Paladino 5')
    expect(wrapper.findAll('[data-rest]')).toHaveLength(2)
  })

  it('-1 e +1 mandam deltas e mostram a resposta; limites desabilitam os botões', async () => {
    const sense = feature('sense', 0, { name: 'Sentido Divino', maxUses: 5, currentUses: 5, recovery: 'LongRest' })
    useCharacterStore().apply(withFeatures([sense]), { resetDraft: true })
    api.spendFeature.mockResolvedValueOnce(withFeatures([{ ...sense, data: { ...sense.data, currentUses: 4 } }]))
    const wrapper = mount(FeatureList)
    expect(wrapper.get('[data-regain]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-restore]').attributes('disabled')).toBeDefined()

    await wrapper.get('[data-spend]').trigger('click')
    await flushPromises()
    expect(api.spendFeature).toHaveBeenCalledWith(expect.anything(), 'sense', -1)
    expect(wrapper.get('[data-feature-uses]').text()).toBe('4/5')
    expect(wrapper.get('[data-regain]').attributes('disabled')).toBeUndefined()

    useCharacterStore().apply(withFeatures([{ ...sense, data: { ...sense.data, currentUses: 0 } }]))
    await flushPromises()
    expect(wrapper.get('[data-spend]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-feature-uses]').classes()).toContain('empty')
  })

  it('dois toques rápidos em -1 viram duas chamadas, em ordem', async () => {
    const lay = feature('lay', 0, { name: 'Cura pelas Mãos', maxUses: 25, currentUses: 25, recovery: 'LongRest' })
    useCharacterStore().apply(withFeatures([lay]), { resetDraft: true })
    api.spendFeature
      .mockResolvedValueOnce(withFeatures([{ ...lay, data: { ...lay.data, currentUses: 24 } }]))
      .mockResolvedValueOnce(withFeatures([{ ...lay, data: { ...lay.data, currentUses: 23 } }]))
    const wrapper = mount(FeatureList)
    await wrapper.get('[data-spend]').trigger('click')
    await wrapper.get('[data-spend]').trigger('click')
    await flushPromises()
    expect(api.spendFeature).toHaveBeenCalledTimes(2)
    expect(wrapper.get('[data-feature-uses]').text()).toBe('23/25')
  })

  it('descansos e restaurar chamam o servidor', async () => {
    const channel = feature('channel', 0, { name: 'Canalizar Divindade', maxUses: 1, currentUses: 0, recovery: 'ShortRest' })
    useCharacterStore().apply(withFeatures([channel]), { resetDraft: true })
    api.rest.mockResolvedValue(withFeatures([{ ...channel, data: { ...channel.data, currentUses: 1 } }]))
    api.restoreFeature.mockResolvedValue(withFeatures([{ ...channel, data: { ...channel.data, currentUses: 1 } }]))
    const wrapper = mount(FeatureList)

    await wrapper.get('[data-rest="Short"]').trigger('click')
    await flushPromises()
    expect(api.rest).toHaveBeenCalledWith(expect.anything(), 'Short')
    await wrapper.get('[data-rest="Long"]').trigger('click')
    await flushPromises()
    expect(api.rest).toHaveBeenLastCalledWith(expect.anything(), 'Long')

    useCharacterStore().apply(withFeatures([channel]))
    await flushPromises()
    await wrapper.get('[data-restore]').trigger('click')
    await flushPromises()
    expect(api.restoreFeature).toHaveBeenCalledWith(expect.anything(), 'channel')
  })

  it('move para cima e para baixo, com os extremos desabilitados', async () => {
    const list = [feature('A', 0), feature('B', 1)]
    useCharacterStore().apply(withFeatures(list), { resetDraft: true })
    api.moveFeature.mockResolvedValue(withFeatures(list))
    const wrapper = mount(FeatureList)
    const [first, second] = wrapper.findAll('[data-feature-id]')
    expect(first!.get('[data-move-up]').attributes('disabled')).toBeDefined()
    expect(second!.get('[data-move-down]').attributes('disabled')).toBeDefined()
    await second!.get('[data-move-up]').trigger('click')
    await flushPromises()
    expect(api.moveFeature).toHaveBeenCalledWith(expect.anything(), 'B', -1)
  })

  it('remover pede confirmação', async () => {
    useCharacterStore().apply(withFeatures([feature('A', 0)]), { resetDraft: true })
    api.removeFeature.mockResolvedValue(withFeatures([]))
    const wrapper = mount(FeatureList, { attachTo: document.body })
    await wrapper.get('[data-remove-feature]').trigger('click')
    expect(api.removeFeature).not.toHaveBeenCalled()
    await wrapper.get('[data-confirm-remove]').trigger('click')
    await flushPromises()
    expect(api.removeFeature).toHaveBeenCalledWith(expect.anything(), 'A')
    wrapper.unmount()
  })

  it('o editor cria com contador e manda o máximo e a recuperação', async () => {
    useCharacterStore().apply(withFeatures([]), { resetDraft: true })
    api.addFeature.mockResolvedValue(withFeatures([feature('x', 0, { name: 'Sentido Divino', maxUses: 5, currentUses: 5, recovery: 'LongRest' })]))
    const wrapper = mount(FeatureList, { attachTo: document.body })
    await wrapper.get('[data-new-feature]').trigger('click')
    const editor = wrapper.get('[data-feature-editor]')
    await editor.get('[data-feature-field="name"]').setValue('  Sentido Divino ')
    await editor.get('[data-feature-field="source"]').setValue('Paladino 5')
    await editor.get('[data-feature-field="hasCounter"]').setValue(true)
    await editor.get('[data-feature-field="maxUses"]').setValue('5')
    await editor.trigger('submit')
    await flushPromises()
    expect(api.addFeature).toHaveBeenCalledWith(expect.anything(), {
      name: 'Sentido Divino', source: 'Paladino 5', description: null, maxUses: 5, currentUses: 5, recovery: 'LongRest'
    })
    expect(wrapper.find('[data-feature-editor]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('o editor recusa usos fora de 1 a 999 sem chamar o servidor', async () => {
    useCharacterStore().apply(withFeatures([]), { resetDraft: true })
    const wrapper = mount(FeatureList, { attachTo: document.body })
    await wrapper.get('[data-new-feature]').trigger('click')
    const editor = wrapper.get('[data-feature-editor]')
    await editor.get('[data-feature-field="name"]').setValue('Demais')
    await editor.get('[data-feature-field="hasCounter"]').setValue(true)
    await editor.get('[data-feature-field="maxUses"]').setValue('1000')
    await editor.trigger('submit')
    await flushPromises()
    expect(api.addFeature).not.toHaveBeenCalled()
    expect(editor.get('[data-feature-error]').text()).toContain('entre 1 e 999')
    wrapper.unmount()
  })

  it('editar um contador que "Não recupera" mantém a recuperação (achado do crítico)', async () => {
    const relic = feature('relic', 0, { name: 'Relíquia', maxUses: 3, currentUses: 1, recovery: 'None' })
    useCharacterStore().apply(withFeatures([relic]), { resetDraft: true })
    api.saveFeature.mockResolvedValue(withFeatures([relic]))
    const wrapper = mount(FeatureList, { attachTo: document.body })
    await wrapper.get('[data-edit-feature]').trigger('click')
    const editor = wrapper.get('[data-feature-editor]')
    expect((editor.get('[data-feature-field="recovery"]').element as HTMLSelectElement).value).toBe('None')
    await editor.get('[data-feature-field="name"]').setValue('Relíquia antiga')
    await editor.trigger('submit')
    await flushPromises()
    expect(api.saveFeature).toHaveBeenCalledWith(expect.anything(), 'relic', expect.objectContaining({ name: 'Relíquia antiga', maxUses: 3, recovery: 'None' }))
    wrapper.unmount()
  })

  it('cada botão Restaurar diz de qual característica é', () => {
    useCharacterStore().apply(withFeatures([
      feature('a', 0, { name: 'Sentido Divino', maxUses: 5, currentUses: 1, recovery: 'LongRest' }),
      feature('b', 1, { name: 'Canalizar Divindade', maxUses: 1, currentUses: 0, recovery: 'ShortRest' })
    ]), { resetDraft: true })
    const wrapper = mount(FeatureList)
    expect(wrapper.findAll('[data-restore]').map(button => button.attributes('aria-label')))
      .toEqual(['Restaurar todos os usos de Sentido Divino', 'Restaurar todos os usos de Canalizar Divindade'])
  })
})
