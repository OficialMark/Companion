import { mount } from '@vue/test-utils'
import GameModeToggle from '~/components/GameModeToggle.vue'
import { useGameMode } from '~/composables/useGameMode'

describe('useGameMode', () => {
  afterEach(() => { useGameMode().exit() })

  it('starts inactive and toggles/enters/exits correctly', () => {
    const gameMode = useGameMode()
    expect(gameMode.active.value).toBe(false)
    gameMode.enter()
    expect(gameMode.active.value).toBe(true)
    gameMode.toggle()
    expect(gameMode.active.value).toBe(false)
    gameMode.toggle()
    expect(gameMode.active.value).toBe(true)
    gameMode.exit()
    expect(gameMode.active.value).toBe(false)
  })

  it('is shared across every consumer (module-level singleton)', () => {
    useGameMode().enter()
    expect(useGameMode().active.value).toBe(true)
  })
})

describe('GameModeToggle', () => {
  afterEach(() => { useGameMode().exit() })

  it('shows "Modo Jogo" when inactive and switches the game mode state on click', async () => {
    const wrapper = mount(GameModeToggle)
    expect(wrapper.text()).toContain('Modo Jogo')
    expect(useGameMode().active.value).toBe(false)

    await wrapper.get('button').trigger('click')

    expect(useGameMode().active.value).toBe(true)
    expect(wrapper.text()).toContain('Voltar à visão completa')
    wrapper.unmount()
  })

  it('reflects an already-active game mode and switches it back off on click', async () => {
    useGameMode().enter()
    const wrapper = mount(GameModeToggle)
    expect(wrapper.text()).toContain('Voltar à visão completa')

    await wrapper.get('button').trigger('click')

    expect(useGameMode().active.value).toBe(false)
    expect(wrapper.text()).toContain('Modo Jogo')
    wrapper.unmount()
  })
})
