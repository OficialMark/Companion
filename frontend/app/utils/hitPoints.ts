/** Hit points of a character sheet (spec 012, US3). */
export interface HitPointState { current: number; temporary: number }

/** Largest single change the overview accepts, the same ceiling as the sheet's HP fields. */
export const MAX_HIT_POINT_CHANGE = 9999

/** Whole, non-negative amount from what the player typed; 0 for anything else. */
export function hitPointAmount(raw: number | string | null | undefined): number {
  const value = typeof raw === 'string' ? Number(raw.replace(',', '.')) : raw ?? 0
  if (!Number.isFinite(value) || value <= 0) return 0
  return Math.min(MAX_HIT_POINT_CHANGE, Math.trunc(value))
}

/** Damage takes temporary hit points first (D&D 5e), then current hit points, never below 0. */
export function applyDamage(state: HitPointState, amount: number): HitPointState {
  const damage = hitPointAmount(amount)
  const fromTemporary = Math.min(state.temporary, damage)
  return { temporary: state.temporary - fromTemporary, current: Math.max(0, state.current - (damage - fromTemporary)) }
}

/** Healing raises current hit points up to the maximum; temporary hit points are untouched. */
export function applyHealing(state: HitPointState, amount: number, max: number): HitPointState {
  return { temporary: state.temporary, current: Math.min(Math.max(0, max), state.current + hitPointAmount(amount)) }
}
