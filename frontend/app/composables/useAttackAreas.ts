import type { AttackArea, AttackAreaShape, BattlemapToken } from '~/types/campaign'

export interface AttackAreaDraft {
  shape: AttackAreaShape
  radius: number
  width: number | null
  directionDegrees: number | null
}

export function defaultDraft(existing?: AttackArea | null): AttackAreaDraft {
  if (existing) {
    return { shape: existing.shape, radius: existing.radius, width: existing.width, directionDegrees: existing.directionDegrees }
  }
  return { shape: 'Circle', radius: 2, width: null, directionDegrees: null }
}

/**
 * Local (non-persisted) preview state for the attack-area panel: holds the
 * in-progress draft for whichever token is currently being configured, so the
 * drawing on the board updates live while sliders move, before the change is
 * committed to the backend (which is done by the caller, e.g. the page/store).
 */
export function useAttackAreas() {
  const previews = reactive<Record<string, AttackAreaDraft>>({})

  function draftFor(token: BattlemapToken): AttackAreaDraft {
    return previews[token.participantId] ?? defaultDraft(token.attackArea)
  }

  function preview(tokenId: string, draft: AttackAreaDraft) {
    previews[tokenId] = draft
  }

  function clearPreview(tokenId: string) {
    delete previews[tokenId]
  }

  return { previews, draftFor, preview, clearPreview }
}
