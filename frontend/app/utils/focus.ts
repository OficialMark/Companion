/**
 * Keyboard focus helpers for the sheet's dialogs and inline confirmations (spec 011, FR-031, FR-039).
 * Plain DOM, no framework state: callers pass the element that scopes the search.
 */
import { nextTick } from 'vue'

const FOCUSABLE = [
  'button:not([disabled])', 'input:not([disabled])', 'select:not([disabled])', 'textarea:not([disabled])',
  'a[href]', '[tabindex]:not([tabindex="-1"])'
].join(', ')

export function focusableIn(root: ParentNode): HTMLElement[] {
  return [...root.querySelectorAll<HTMLElement>(FOCUSABLE)].filter(element => !element.hidden && !element.closest('[inert]'))
}

/**
 * Keeps Tab inside the innermost modal that contains the focus (a nested editor opened from the
 * bag traps its own fields), falling back to `root`. Call it from a keydown handler.
 */
export function trapTab(event: KeyboardEvent, root: HTMLElement) {
  if (event.key !== 'Tab') return
  const target = event.target instanceof Element ? event.target : null
  const scope = (target?.closest<HTMLElement>('[aria-modal="true"]') ?? root)
  const focusable = focusableIn(scope)
  if (!focusable.length) { event.preventDefault(); return }
  const first = focusable[0]!
  const last = focusable[focusable.length - 1]!
  const active = document.activeElement
  const inside = active instanceof Node && scope.contains(active)
  if (event.shiftKey && (active === first || !inside || !focusable.includes(active as HTMLElement))) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && (active === last || !inside)) {
    event.preventDefault()
    first.focus()
  }
}

/** The element of a list row, matched by a data attribute without building a CSS selector from user ids. */
export function rowOf(root: ParentNode | null | undefined, attribute: string, id: string): HTMLElement | null {
  if (!root) return null
  return [...root.querySelectorAll<HTMLElement>(`[${attribute}]`)].find(element => element.getAttribute(attribute) === id) ?? null
}

/** Focus after Vue has rendered the change that creates the element. */
export async function focusAfterRender(find: () => HTMLElement | null | undefined) {
  await nextTick()
  find()?.focus()
}
