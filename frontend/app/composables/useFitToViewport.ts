import { onBeforeUnmount, onMounted, watch, type Ref } from 'vue'

/** Gap kept between a fitted column and the bottom of the window, in px. */
export const FIT_BOTTOM_GAP = 16

/**
 * Height a sticky side column may take so its own bottom (where "Rolar" or "Começar combate" stick)
 * is inside the window: from where the column starts now to the window bottom. CSS alone only knows
 * the stuck position (top: 88px), so on load, when the column still starts lower (under the greeting
 * and the sheet summary), its foot fell below the fold (design review, round 4).
 */
export function fittedHeight(top: number, viewportHeight: number, stickyTop: number, gap = FIT_BOTTOM_GAP) {
  return Math.max(240, Math.round(viewportHeight - Math.max(top, stickyTop) - gap))
}

/**
 * Keeps `--fit-height` on each element up to date while the page scrolls or resizes. One rAF per
 * frame at most; nothing is written when the element is not sticky (phones: the zones are not boxes).
 */
export function useFitToViewport(elements: Ref<HTMLElement | null>[]) {
  let frame = 0
  function update() {
    frame = 0
    for (const element of elements) {
      const el = element.value
      if (!el) continue
      const style = getComputedStyle(el)
      if (style.position !== 'sticky') { el.style.removeProperty('--fit-height'); continue }
      const stickyTop = parseFloat(style.top) || 0
      el.style.setProperty('--fit-height', `${fittedHeight(el.getBoundingClientRect().top, window.innerHeight, stickyTop)}px`)
    }
  }
  function schedule() {
    if (!frame) frame = requestAnimationFrame(update)
  }
  // The zones mount after the table loads (and remount on view changes): fit them when they appear.
  watch(() => elements.map(element => element.value), schedule, { flush: 'post' })
  onMounted(() => {
    window.addEventListener('scroll', schedule, { passive: true })
    window.addEventListener('resize', schedule, { passive: true })
    schedule()
  })
  onBeforeUnmount(() => {
    window.removeEventListener('scroll', schedule)
    window.removeEventListener('resize', schedule)
    if (frame) cancelAnimationFrame(frame)
  })
  return { update: schedule }
}
