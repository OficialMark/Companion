import { emptyConfig, resolveRoll } from '~/utils/playerRoll'
import { makeFighter } from './player-roll-fixture'

// Spec 011: the SRD blowgun deals a fixed 1 damage. There is nothing to roll unless the player adds dice.
function blowgunCharacter() {
  const character = makeFighter()
  character.stats.weaponRolls = [{
    itemId: 'blowgun', name: 'Zarabatana', equipped: true, ability: 'Dex', attackBonus: 2,
    attack: [{ label: 'DES', value: 2, source: 'DES', itemId: null }],
    damage: { dice: '1', damageType: 'perfurante', parts: [{ label: 'DES', value: 2, source: 'DES', itemId: null }] },
    versatile: null
  }]
  return character
}

describe('dano fixo (zarabatana do SRD: 1 de dano)', () => {
  it('não rola: mostra o total fixo e bloqueia o envio', () => {
    const character = blowgunCharacter()
    const result = resolveRoll({ ...emptyConfig(), kind: 'Damage', source: { type: 'weapon', itemId: 'blowgun' } }, character.stats, character)
    expect(result.expression).toBe('')
    expect(result.error).toBe('Dano fixo de 3: não precisa rolar.')
  })

  it('com dados extras no painel, o dano fixo entra como constante rotulada', () => {
    const character = blowgunCharacter()
    const result = resolveRoll({ ...emptyConfig(), kind: 'Damage', source: { type: 'weapon', itemId: 'blowgun' }, dice: { d6: 1 } }, character.stats, character)
    expect(result.error).toBeNull()
    expect(result.expression).toBe('d6 + (dano) 1 + (DES) 2')
  })
})
