/**
 * Brings a roll's dice and result into view (spec 012, FR-007): the page scrolls so the element sits
 * in the middle of the screen. Smooth by default, instant when the user asked for reduced motion.
 * A no-op without an element or in environments without scrollIntoView.
 */
export function scrollToResult(element: Element | null | undefined) {
  if (!element || typeof element.scrollIntoView !== 'function') return
  const reduce = typeof window !== 'undefined' && typeof window.matchMedia === 'function'
    && window.matchMedia('(prefers-reduced-motion: reduce)').matches
  element.scrollIntoView({ block: 'center', inline: 'nearest', behavior: reduce ? 'auto' : 'smooth' })
}
