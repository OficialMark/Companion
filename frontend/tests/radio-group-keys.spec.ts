import { mount } from '@vue/test-utils'
import { radioGroupKeydown } from '~/utils/focus'

// Spec 011 review (UX finding 10): radio groups follow the WAI-ARIA pattern. Arrows move and
// select, Home/End jump, disabled radios are skipped, and only the checked radio is in the Tab order.
const Group = {
  props: ['disabled'],
  data: () => ({ value: 'b' }),
  methods: { radioGroupKeydown },
  template: `
    <div role="radiogroup" @keydown="radioGroupKeydown">
      <button v-for="option in ['a', 'b', 'c', 'd']" :key="option" type="button" role="radio"
        :data-option="option" :aria-checked="value === option" :tabindex="value === option ? 0 : -1"
        :disabled="option === disabled" @click="value = option">{{ option }}</button>
    </div>`
}

describe('teclado nos grupos de rádio', () => {
  it('setas andam e selecionam, com volta ao início', async () => {
    const wrapper = mount(Group, { attachTo: document.body })
    await wrapper.get('[data-option="b"]').trigger('keydown', { key: 'ArrowRight' })
    expect(wrapper.vm.value).toBe('c')
    expect(document.activeElement?.getAttribute('data-option')).toBe('c')
    await wrapper.get('[data-option="c"]').trigger('keydown', { key: 'ArrowDown' })
    await wrapper.get('[data-option="d"]').trigger('keydown', { key: 'ArrowRight' })
    expect(wrapper.vm.value).toBe('a')
    await wrapper.get('[data-option="a"]').trigger('keydown', { key: 'ArrowLeft' })
    expect(wrapper.vm.value).toBe('d')
    wrapper.unmount()
  })

  it('Home e End vão às pontas e o desabilitado é pulado', async () => {
    const wrapper = mount(Group, { props: { disabled: 'c' }, attachTo: document.body })
    await wrapper.get('[data-option="b"]').trigger('keydown', { key: 'ArrowRight' })
    expect(wrapper.vm.value).toBe('d')
    await wrapper.get('[data-option="d"]').trigger('keydown', { key: 'Home' })
    expect(wrapper.vm.value).toBe('a')
    await wrapper.get('[data-option="a"]').trigger('keydown', { key: 'End' })
    expect(wrapper.vm.value).toBe('d')
    wrapper.unmount()
  })

  it('só o marcado fica na ordem do Tab, e outras teclas não fazem nada', async () => {
    const wrapper = mount(Group, { attachTo: document.body })
    expect(wrapper.findAll('[tabindex="0"]').map(x => x.attributes('data-option'))).toEqual(['b'])
    await wrapper.get('[data-option="b"]').trigger('keydown', { key: 'Enter' })
    expect(wrapper.vm.value).toBe('b')
    wrapper.unmount()
  })
})
