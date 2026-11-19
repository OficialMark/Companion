import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useCampaignStore } from '~/stores/campaign'
import { useCharacterStore } from '~/stores/character'
import PlayerRollPanel from '~/components/character/PlayerRollPanel.vue'
import { makeFighter } from './player-roll-fixture'

// Spec 011, FR-027 regression (found by the e2e): typing in the repetitions field must update the
// expression immediately, not only when the field loses focus.
vi.stubGlobal('useCompanionApi', () => ({ rollDice: vi.fn(), revealDiceRoll: vi.fn() }))
vi.stubGlobal('useCharacterApi', () => ({ addRoll: vi.fn(), saveRoll: vi.fn() }))
vi.stubGlobal('useCampaignStore', () => useCampaignStore())
vi.stubGlobal('useCharacterStore', () => useCharacterStore())

function setup() {
  setActivePinia(createPinia())
  const campaign = useCampaignStore()
  campaign.session = { campaignId: 'c1', playerCode: '1234', masterCode: null, participantId: 'p1', displayName: 'Loren', role: 'Player', participantToken: 't' } as never
  campaign.connection = 'online'
  campaign.snapshot = { diceRolls: [] } as never
  useCharacterStore().character = makeFighter()
  return mount(PlayerRollPanel, { global: { stubs: { DiceRollAnimation: true } } })
}

describe('repetições ao vivo', () => {
  it('digitar o número atualiza a expressão sem sair do campo', async () => {
    const wrapper = setup()
    await wrapper.get('[data-die="d20"]').trigger('click')
    const input = wrapper.get('[data-repetitions]')
    ;(input.element as HTMLInputElement).value = '4'
    await input.trigger('input')
    expect(wrapper.get('[data-expression]').text()).toBe('4#[d20]')
  })

  it('valor incompleto ou fora do limite espera a saída do campo', async () => {
    const wrapper = setup()
    await wrapper.get('[data-die="d20"]').trigger('click')
    const input = wrapper.get('[data-repetitions]')
    ;(input.element as HTMLInputElement).value = '25'
    await input.trigger('input')
    expect(wrapper.get('[data-expression]').text()).toBe('1d20')
    await input.trigger('change')
    expect(wrapper.get('[data-expression]').text()).toBe('20#[d20]')
  })
})
