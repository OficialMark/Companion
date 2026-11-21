interface ScrollSnapshot { element: HTMLElement; top: number; left: number }
export interface ResultScrollSession {
  show: (element: Element | null | undefined) => void
  restore: () => void
}
const USER_NAVIGATION_EVENTS = ['wheel', 'touchstart', 'pointerdown', 'keydown'] as const
const reducedMotion = () => typeof window.matchMedia === 'function'
  && window.matchMedia('(prefers-reduced-motion: reduce)').matches

function scrollableAncestors(element: Element): HTMLElement[] {
  const result: HTMLElement[] = []
  let parent = element.parentElement
  while (parent) {
    const documentScroller = parent === document.body || parent === document.documentElement
    if (!documentScroller && (parent.scrollHeight > parent.clientHeight || parent.scrollWidth > parent.clientWidth)) result.push(parent)
    parent = parent.parentElement
  }
  return result
}
function visibleBottom(element: Element, rect: DOMRect) {
  const height = document.documentElement.clientHeight || window.innerHeight
  if (element.closest('.modal-overlay')) return height

  const sheet = element.closest('.dashboard')?.querySelector('[data-sheet-summary]')?.getBoundingClientRect()
  if (!sheet || sheet.height <= 0 || sheet.top >= height || sheet.bottom <= 0 ||
      sheet.right <= rect.left || sheet.left >= rect.right) return height

  return Math.max(0, sheet.top)
}

function fullyVisible(rect: DOMRect, ancestors: HTMLElement[], bottom: number) {
  const width = document.documentElement.clientWidth || window.innerWidth
  if (rect.top < 0 || rect.left < 0 || rect.bottom > bottom || rect.right > width) return false
  return ancestors.every((ancestor) => {
    const bounds = ancestor.getBoundingClientRect()
    return rect.top >= bounds.top && rect.bottom <= bounds.bottom && rect.left >= bounds.left && rect.right <= bounds.right
  })
}

/** Moves only a clipped edge into view, then returns when the animation leaves unless the user navigated. */
export function createResultScrollSession(): ResultScrollSession {
  let originalWindow: { top: number; left: number } | null = null
  let originalContainers: ScrollSnapshot[] = []
  let userNavigated = false
  let listening = false
  const noteNavigation = () => { userNavigated = true }
  function listen() {
    if (listening) return
    listening = true
    for (const event of USER_NAVIGATION_EVENTS) window.addEventListener(event, noteNavigation, { capture: true, passive: true })
  }
  function unlisten() {
    if (!listening) return
    listening = false
    for (const event of USER_NAVIGATION_EVENTS) window.removeEventListener(event, noteNavigation, { capture: true })
  }
  function show(element: Element | null | undefined) {
    if (!element || typeof element.scrollIntoView !== 'function' || userNavigated) return
    const rect = element.getBoundingClientRect()
    const bottom = visibleBottom(element, rect)
    const height = document.documentElement.clientHeight || window.innerHeight
    const inset = height - bottom
    if (element instanceof HTMLElement) {
      element.style.setProperty('--roll-footer-inset', inset ? `${inset}px` : '')
    }
    const ancestors = scrollableAncestors(element)
    if (fullyVisible(rect, ancestors, bottom)) return
    if (!originalWindow) {
      originalWindow = { top: window.scrollY, left: window.scrollX }
      originalContainers = ancestors.map(element => ({ element, top: element.scrollTop, left: element.scrollLeft }))
      userNavigated = false
      listen()
    } else {
      const known = new Set(originalContainers.map(snapshot => snapshot.element))
      originalContainers.push(...ancestors.filter(element => !known.has(element))
        .map(element => ({ element, top: element.scrollTop, left: element.scrollLeft })))
    }
    const block = inset && rect.height + 32 > bottom ? 'start' : 'nearest'
    element.scrollIntoView({ block, inline: 'nearest', behavior: reducedMotion() ? 'auto' : 'smooth' })
  }
  function restore() {
    unlisten()
    const windowPosition = originalWindow
    const containers = originalContainers
    const shouldRestore = Boolean(windowPosition) && !userNavigated
    originalWindow = null; originalContainers = []; userNavigated = false
    if (!shouldRestore || !windowPosition) return
    const behavior = reducedMotion() ? 'auto' : 'smooth'
    for (const { element, top, left } of containers) {
      if (Math.abs(element.scrollTop - top) <= 1 && Math.abs(element.scrollLeft - left) <= 1) continue
      if (typeof element.scrollTo === 'function') element.scrollTo({ top, left, behavior })
      else { element.scrollTop = top; element.scrollLeft = left }
    }
    if (Math.abs(window.scrollY - windowPosition.top) > 1 || Math.abs(window.scrollX - windowPosition.left) > 1)
      window.scrollTo({ top: windowPosition.top, left: windowPosition.left, behavior })
  }
  return { show, restore }
}
