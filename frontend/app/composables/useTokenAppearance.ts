import type { BattlemapToken, StatusCondition } from '~/types/campaign'

export interface TokenAppearanceDraft {
  footprintSize: number
  statusConditions: StatusCondition[]
}

// Single source of truth for how a status condition is labeled/abbreviated,
// shared by the token menu (AttackAreaPanel.vue) and the token badge itself
// (BattlemapToken.vue) so the two never drift out of sync.
export const STATUS_CONDITION_OPTIONS: Array<{ value: StatusCondition; label: string; abbreviation: string }> = [
  { value: 'Stunned', label: 'Atordoado', abbreviation: 'ATD' },
  { value: 'Restrained', label: 'Imobilizado', abbreviation: 'IMO' },
  { value: 'Blinded', label: 'Cego', abbreviation: 'CEG' },
  { value: 'Poisoned', label: 'Envenenado', abbreviation: 'VEN' },
  { value: 'Invisible', label: 'Invisível', abbreviation: 'INV' },
  { value: 'Prone', label: 'Caído', abbreviation: 'CAI' }
]

export function defaultAppearanceDraft(token?: BattlemapToken | null): TokenAppearanceDraft {
  return {
    footprintSize: token?.footprintSize ?? 1,
    statusConditions: token?.statusConditions ?? []
  }
}

/**
 * Local (non-persisted) preview state for the token size/conditions section
 * of the token interaction panel, mirroring useAttackAreas.ts: holds the
 * in-progress draft while the panel is open, committed to the backend by
 * the caller (the page/store) once the user confirms a change.
 */
export function useTokenAppearance() {
  const previews = reactive<Record<string, TokenAppearanceDraft>>({})

  function draftFor(token: BattlemapToken): TokenAppearanceDraft {
    return previews[token.participantId] ?? defaultAppearanceDraft(token)
  }

  function preview(tokenId: string, draft: TokenAppearanceDraft) {
    previews[tokenId] = draft
  }

  function clearPreview(tokenId: string) {
    delete previews[tokenId]
  }

  return { previews, draftFor, preview, clearPreview }
}
