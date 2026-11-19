/**
 * Creating/removing a representative token (marker). Repositioning and
 * configuring one (area/size/conditions) reuse the existing generic
 * per-token flows (useAttackAreas, useTokenAppearance, store.moveBattlemapToken)
 * once the backend resolves the token id as a marker — no separate move/
 * configure logic is needed here.
 */
export function useBattlemapMarkers() {
  const creating = ref(false)
  const removingId = ref<string | null>(null)

  async function create(label: string) {
    const store = useCampaignStore()
    creating.value = true
    try {
      await store.createMarker(label)
    } finally {
      creating.value = false
    }
  }

  async function remove(markerId: string) {
    const store = useCampaignStore()
    removingId.value = markerId
    try {
      await store.removeMarker(markerId)
    } finally {
      removingId.value = null
    }
  }

  return { creating, removingId, create, remove }
}
