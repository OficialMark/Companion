import { applyDamage, applyHealing, hitPointAmount, MAX_HIT_POINT_CHANGE } from '~/utils/hitPoints'

describe('hit points (spec 012, US3)', () => {
  it('reads what the player typed as a whole, positive amount', () => {
    expect(hitPointAmount(7)).toBe(7)
    expect(hitPointAmount('7,9')).toBe(7)
    expect(hitPointAmount('')).toBe(0)
    expect(hitPointAmount(-3)).toBe(0)
    expect(hitPointAmount(Number.NaN)).toBe(0)
    expect(hitPointAmount(null)).toBe(0)
    expect(hitPointAmount(1e9)).toBe(MAX_HIT_POINT_CHANGE)
  })

  it('damage spends temporary hit points first and stops at 0', () => {
    expect(applyDamage({ current: 20, temporary: 5 }, 3)).toEqual({ current: 20, temporary: 2 })
    expect(applyDamage({ current: 20, temporary: 5 }, 8)).toEqual({ current: 17, temporary: 0 })
    expect(applyDamage({ current: 4, temporary: 0 }, 10)).toEqual({ current: 0, temporary: 0 })
    expect(applyDamage({ current: 4, temporary: 0 }, -10)).toEqual({ current: 4, temporary: 0 })
  })

  it('healing stops at the maximum and keeps temporary hit points', () => {
    expect(applyHealing({ current: 10, temporary: 3 }, 5, 42)).toEqual({ current: 15, temporary: 3 })
    expect(applyHealing({ current: 40, temporary: 0 }, 5, 42)).toEqual({ current: 42, temporary: 0 })
    expect(applyHealing({ current: 0, temporary: 0 }, 0, 42)).toEqual({ current: 0, temporary: 0 })
  })
})
