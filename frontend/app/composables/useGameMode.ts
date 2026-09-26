const active = ref(false)

export function useGameMode() {
  function enter() { active.value = true }
  function exit() { active.value = false }
  function toggle() { active.value = !active.value }
  return { active, enter, exit, toggle }
}
