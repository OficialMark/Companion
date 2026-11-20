import { mount } from '@vue/test-utils'
import { defineComponent, h, ref } from 'vue'
import DicePad from '~/components/character/DicePad.vue'
import { LONG_PRESS_MS } from '~/composables/useLongPress'
import type { DiceCounts } from '~/utils/playerRoll'

// Spec 011, FR-026: tap adds one die, hold 0.5 s removes one (and the release adds nothing),
// Delete/Backspace/"-" on the focused die removes one, and a button clears all.
function mountPad(initial: DiceCounts = {}) {
  const value = ref<DiceCounts>(initial)
  const Host = defineComponent({
    setup: () => () => h(DicePad, { modelValue: value.value, 'onUpdate:modelValue': (next: DiceCounts) => { value.value = next } })
  })
  const wrapper = mount(Host, { attachTo: document.body })
  return { wrapper, value }
}

const pointer = (type: string, button = 0) => new PointerEvent(type, { bubbles: true, button, pointerType: 'touch' })

beforeEach(() => { vi.useFakeTimers() })
afterEach(() => { vi.useRealTimers(); document.body.innerHTML = '' })

describe('DicePad', () => {
  it('shows the seven dice with a 44px-ready label and a count badge', async () => {
    const { wrapper } = mountPad({ d6: 3 })
    expect(wrapper.findAll('[data-die]').map(die => die.attributes('data-die'))).toEqual(['d4', 'd6', 'd8', 'd10', 'd12', 'd20', 'd100'])
    const d6 = wrapper.get('[data-die="d6"]')
    expect(d6.attributes('aria-label')).toBe('d6, 3 selecionados. Clique soma, segure para tirar. No teclado, Delete ou - tira um')
    expect(d6.get('.dice-pad-badge').text()).toBe('3')
    expect(wrapper.get('[data-die="d4"]').attributes('aria-label')).toBe('d4, nenhum selecionado. Clique soma, segure para tirar. No teclado, Delete ou - tira um')
    expect(wrapper.get('[data-die="d4"]').find('.dice-pad-badge').exists()).toBe(false)
  })

  it('a click adds one die', async () => {
    const { wrapper, value } = mountPad()
    await wrapper.get('[data-die="d6"]').trigger('click')
    await wrapper.get('[data-die="d6"]').trigger('click')
    await wrapper.get('[data-die="d20"]').trigger('click')
    expect(value.value).toEqual({ d6: 2, d20: 1 })
    expect(wrapper.get('[data-die="d6"]').attributes('aria-label')).toBe('d6, 2 selecionados. Clique soma, segure para tirar. No teclado, Delete ou - tira um')
  })

  it('holding 500 ms removes one and the click on release does not add one', async () => {
    const { wrapper, value } = mountPad({ d6: 3 })
    const d6 = wrapper.get('[data-die="d6"]').element
    d6.dispatchEvent(pointer('pointerdown'))
    await vi.advanceTimersByTimeAsync(LONG_PRESS_MS - 1)
    expect(value.value).toEqual({ d6: 3 })
    await vi.advanceTimersByTimeAsync(1)
    expect(value.value).toEqual({ d6: 2 })
    d6.dispatchEvent(pointer('pointerup'))
    await wrapper.get('[data-die="d6"]').trigger('click')
    expect(value.value).toEqual({ d6: 2 })
    // The next ordinary tap counts again.
    d6.dispatchEvent(pointer('pointerdown'))
    d6.dispatchEvent(pointer('pointerup'))
    await wrapper.get('[data-die="d6"]').trigger('click')
    expect(value.value).toEqual({ d6: 3 })
  })

  it('a short press is a tap, and leaving the die cancels the hold', async () => {
    const { wrapper, value } = mountPad({ d8: 1 })
    const d8 = wrapper.get('[data-die="d8"]').element
    d8.dispatchEvent(pointer('pointerdown'))
    await vi.advanceTimersByTimeAsync(200)
    d8.dispatchEvent(pointer('pointerup'))
    await wrapper.get('[data-die="d8"]').trigger('click')
    await vi.advanceTimersByTimeAsync(LONG_PRESS_MS)
    expect(value.value).toEqual({ d8: 2 })

    d8.dispatchEvent(pointer('pointerdown'))
    await vi.advanceTimersByTimeAsync(300)
    d8.dispatchEvent(pointer('pointerleave'))
    await vi.advanceTimersByTimeAsync(LONG_PRESS_MS)
    expect(value.value).toEqual({ d8: 2 })
  })

  it('a right-button press never starts a hold, and the context menu is suppressed', async () => {
    const { wrapper, value } = mountPad({ d10: 2 })
    const d10 = wrapper.get('[data-die="d10"]').element
    d10.dispatchEvent(pointer('pointerdown', 2))
    await vi.advanceTimersByTimeAsync(LONG_PRESS_MS * 2)
    expect(value.value).toEqual({ d10: 2 })
    const menu = new MouseEvent('contextmenu', { bubbles: true, cancelable: true })
    d10.dispatchEvent(menu)
    expect(menu.defaultPrevented).toBe(true)
  })

  it('Delete, Backspace and "-" on the focused die remove one', async () => {
    const { wrapper, value } = mountPad({ d12: 4 })
    const d12 = wrapper.get('[data-die="d12"]')
    await d12.trigger('keydown', { key: 'Delete' })
    await d12.trigger('keydown', { key: 'Backspace' })
    await d12.trigger('keydown', { key: '-' })
    expect(value.value).toEqual({ d12: 1 })
    await d12.trigger('keydown', { key: 'a' })
    expect(value.value).toEqual({ d12: 1 })
    await d12.trigger('keydown', { key: 'Delete' })
    await d12.trigger('keydown', { key: 'Delete' })
    expect(value.value).toEqual({})
  })

  it('the clear button removes every die', async () => {
    const { wrapper, value } = mountPad({ d4: 2, d20: 1 })
    await wrapper.get('[data-clear-dice]').trigger('click')
    expect(value.value).toEqual({})
    expect(wrapper.get('[data-clear-dice]').attributes('disabled')).toBeDefined()
  })

  it('stops at 20 dice of one kind', async () => {
    const { wrapper, value } = mountPad({ d100: 19 })
    await wrapper.get('[data-die="d100"]').trigger('click')
    await wrapper.get('[data-die="d100"]').trigger('click')
    expect(value.value).toEqual({ d100: 20 })
  })
})
