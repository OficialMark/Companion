/** How long a press must last to count as a long press (spec 011, FR-026). */
export const LONG_PRESS_MS = 500

/**
 * Long press on touch and mouse alike (pointer events). Holding the pointer down for `delay` ms calls
 * `onLongPress(arg)`, and the click the browser fires on release is swallowed, so a hold never also
 * counts as a tap. Releasing, leaving the target or a cancelled pointer before `delay` ms cancels it.
 *
 * One instance serves many targets: `bind(arg)` gives the listeners for one element, and the click
 * handler asks `consumeClick()` before acting.
 */
export function useLongPress<T>(onLongPress: (arg: T) => void, delay = LONG_PRESS_MS) {
  let timer: ReturnType<typeof setTimeout> | undefined
  let suppressClick = false

  function cancel() {
    if (timer) clearTimeout(timer)
    timer = undefined
  }

  function start(event: PointerEvent, arg: T) {
    // Only the primary button (or a touch / pen contact) starts a press; right click never does.
    if (event.button !== 0) return
    cancel()
    suppressClick = false
    timer = setTimeout(() => {
      timer = undefined
      suppressClick = true
      onLongPress(arg)
    }, delay)
  }

  /** True when the click that just arrived ends a long press: the caller must ignore it. */
  function consumeClick() {
    if (!suppressClick) return false
    suppressClick = false
    return true
  }

  function bind(arg: T) {
    return {
      onPointerdown: (event: PointerEvent) => start(event, arg),
      onPointerup: cancel,
      onPointerleave: cancel,
      onPointercancel: cancel,
      // A touch hold opens the browser's context menu on Android; the hold is ours.
      onContextmenu: (event: Event) => event.preventDefault()
    }
  }

  onBeforeUnmount(cancel)

  return { bind, consumeClick, cancel }
}
