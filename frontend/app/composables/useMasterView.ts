export type MasterView = 'mesa' | 'compendio'

/**
 * The master's current screen (spec 009, FR-009). It lives in the URL (`?tela=compendio`) so a
 * reload or a shared link reopens it. A player is always on the Mesa, whatever the URL asks.
 */
export function useMasterView() {
  const route = useRoute()
  const router = useRouter()
  const store = useCampaignStore()
  const view = computed<MasterView>(() => store.isMaster && route.query.tela === 'compendio' ? 'compendio' : 'mesa')

  function go(next: MasterView) {
    if (next === view.value) return
    const query = { ...route.query }
    if (next === 'compendio') query.tela = 'compendio'
    else delete query.tela
    // replace: switching screens is not a step the browser's back button should walk through.
    void router.replace({ query })
    if (typeof window !== 'undefined') window.scrollTo({ top: 0 })
  }

  return { view, go }
}
