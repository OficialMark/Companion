import type { DiceRoll } from '~/types/campaign'
import { createUuid } from '~/utils/createUuid'

export function useAttackResolution() {
  const store = useCampaignStore(); const api = useCompanionApi()
  const requestId = ref(''); const roll = ref<DiceRoll | null>(null); const selected = ref<string[]>([]); const busy = ref(false); const error = ref(''); const message = ref('')
  const encounter = computed(() => store.snapshot?.activeEncounter ?? null)
  const canAttack = computed(() => encounter.value?.status === 'Active' && (store.isMaster || encounter.value.combatants.some(c => c.participantId === store.session?.participantId)))

  function rolled(result: DiceRoll) { roll.value = result; requestId.value = createUuid(); selected.value = []; error.value = '' }
  function cancel() { roll.value = null; selected.value = []; error.value = '' }
  async function confirm() {
    if (!store.session || !encounter.value || !roll.value || selected.value.length === 0) return
    busy.value = true; error.value = ''; message.value = ''
    try {
      const result = await api.resolveAttack(store.session, encounter.value.id, roll.value.id, selected.value, requestId.value)
      store.updateActiveEncounter(result.encounter)
      message.value = `Ataque de ${result.attack.damage} aplicado.`
      roll.value = null; selected.value = []
      return result
    } catch (exception) { error.value = (exception as Error).message }
    finally { busy.value = false }
  }

  return { encounter, canAttack, roll, selected, busy, error, message, rolled, cancel, confirm }
}
