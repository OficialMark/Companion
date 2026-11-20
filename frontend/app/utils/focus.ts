/**
 * Keyboard focus helpers for the sheet's dialogs and inline confirmations (spec 011, FR-031, FR-039).
 * Plain DOM, no framework state: callers pass the element that scopes the search.
 */
import { nextTick } from 'vue'

const FOCUSABLE = [
  'button:not([disabled])', 'input:not([disabled])', 'select:not([disabled])', 'textarea:not([disabled])',
  'a[href]', '[tabindex]:not([tabindex="-1"])'
].join(', ')

/**
 * ARIA radio group keys, attached to the element with role="radiogroup": arrows move to the
 * previous or next enabled radio (wrapping), Home and End to the first and last. The radio that
 * receives focus is also selected (clicked), as the WAI-ARIA radio group pattern asks. Pair it with
 * a roving tabindex (0 on the checked radio, -1 on the others).
 */
export function radioGroupKeydown(event: KeyboardEvent) {
  const group = event.currentTarget as HTMLElement | null
  if (!group) return
  const radios = [...group.querySelectorAll<HTMLElement>('[role="radio"]')]
    .filter(radio => !(radio as HTMLButtonElement).disabled)
  if (!radios.length) return
  const index = radios.indexOf(event.target as HTMLElement)
  let next: number
  switch (event.key) {
    case 'ArrowRight': case 'ArrowDown': next = index < 0 ? 0 : (index + 1) % radios.length; break
    case 'ArrowLeft': case 'ArrowUp': next = index < 0 ? radios.length - 1 : (index - 1 + radios.length) % radios.length; break
    case 'Home': next = 0; break
    case 'End': next = radios.length - 1; break
    default: return
  }
  event.preventDefault()
  radios[next]!.focus()
  radios[next]!.click()
}

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
