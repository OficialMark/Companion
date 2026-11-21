export type AppView = 'mesa' | 'compendio' | 'ficha' | 'campanha'

/**
 * The current screen of the top-bar navigation (spec 009 FR-009, spec 011 FR-001/FR-002). It lives
 * in the URL (`?tela=compendio` or `?tela=ficha`) so a reload or a shared link reopens it. The
 * Compendium belongs to the master and the Ficha to players; anything else falls back to the Mesa.
 */
export function useAppView() {
  const route = useRoute()
  const router = useRouter()
  const store = useCampaignStore()
  const view = computed<AppView>(() => {
    const asked = route.query.tela
    if (asked === 'campanha' && store.session) return 'campanha'
    if (asked === 'compendio' && store.isMaster) return 'compendio'
    if (asked === 'ficha' && store.session?.role === 'Player') return 'ficha'
    return 'mesa'
  })

  function go(next: AppView) {
    if (next === view.value) return
    const query = { ...route.query }
    if (next === 'mesa') delete query.tela
    else query.tela = next
    // replace: switching screens is not a step the browser's back button should walk through.
    void router.replace({ query })
    if (typeof window !== 'undefined') window.scrollTo({ top: 0 })
  }

  return { view, go }
}
