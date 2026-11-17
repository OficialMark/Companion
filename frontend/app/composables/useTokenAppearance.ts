import { ref } from 'vue'
import type { BattlemapToken, StatusCondition } from '~/types/campaign'

export type TokenPanelTab = 'area' | 'token' | 'conditions'

/**
 * Last tab opened in the token menu, shared by every panel instance on purpose: after
 * marking a condition on one token, the next token opens on the same tab (a master
 * applying one spell to a group of creatures). Client-only UI state.
 */
export const tokenPanelTab = ref<TokenPanelTab>('area')

export interface TokenAppearanceDraft {
  footprintSize: number
  statusConditions: StatusCondition[]
}

// Labels/icons/colors live in utils/statusConditions.ts (single source of truth for
// the token menu and the board badges); re-exported here for existing imports.
export { STATUS_CONDITIONS as STATUS_CONDITION_OPTIONS } from '~/utils/statusConditions'

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
