<script setup lang="ts">
import type { Participant } from '~/types/campaign'
import UserAvatar from './UserAvatar.vue'

const props = withDefaults(defineProps<{ participants: Participant[]; avatars?: Record<string, string> }>(), { avatars: () => ({}) })
const store = useCampaignStore()
const visibleParticipants = computed(() => props.participants.filter(participant => !participant.isHiddenFromPanel))
const hiddenParticipants = computed(() => props.participants.filter(participant => participant.isHiddenFromPanel))
const contextMenu = ref<{ participantId: string; x: number; y: number } | null>(null)
const busy = ref(false)
const error = ref('')

function openContextMenu(event: MouseEvent, participant: Participant) {
  if (!store.isMaster) return
  event.preventDefault()
  contextMenu.value = {
    participantId: participant.id,
    x: Math.max(8, Math.min(event.clientX, window.innerWidth - 208)),
    y: Math.max(8, Math.min(event.clientY, window.innerHeight - 56))
  }
}

function closeContextMenu() { contextMenu.value = null }

async function setHidden(participantId: string, hidden: boolean) {
  if (busy.value) return
  busy.value = true
  error.value = ''
  const success = await store.setParticipantPanelVisibility(participantId, hidden)
  if (!success) error.value = store.error || 'Não foi possível atualizar o painel.'
  else closeContextMenu()
  busy.value = false
}

onMounted(() => window.addEventListener('click', closeContextMenu))
onBeforeUnmount(() => window.removeEventListener('click', closeContextMenu))
</script>

<template>
  <section class="panel participants-panel" aria-labelledby="participants-title" @keydown.esc="closeContextMenu">
    <div class="panel-heading">
      <div>
        <p class="eyebrow"><Icon name="lucide:users" />Na mesa</p>
        <h2 id="participants-title">Participantes</h2>
      </div>
      <span class="count-pill"><span class="presence-dot online" aria-hidden="true" />{{ visibleParticipants.filter(p => p.isOnline).length }} online</span>
    </div>
    <p v-if="error" class="form-error" role="alert"><Icon name="lucide:circle-alert" />{{ error }}</p>
    <ul class="participant-list">
      <li
        v-for="participant in visibleParticipants"
        :key="participant.id"
        :class="{ offline: !participant.isOnline, removable: store.isMaster }"
        :title="store.isMaster ? 'Clique com o botão direito para remover do painel' : undefined"
        @contextmenu="openContextMenu($event, participant)"
      >
        <UserAvatar :name="participant.displayName" :src="avatars[participant.id]" :master="participant.role === 'Master'" />
        <span class="participant-name">
          {{ participant.displayName }}
          <small class="role-label">
            <Icon :name="participant.role === 'Master' ? 'lucide:crown' : 'lucide:user-round'" />
            {{ participant.role === 'Master' ? 'Mestre' : 'Jogador' }}
          </small>
        </span>
        <span class="presence-dot" :class="{ online: participant.isOnline }" :title="participant.isOnline ? 'Online' : 'Offline'" :aria-label="participant.isOnline ? 'Online' : 'Offline'" role="img" />
      </li>
    </ul>
    <details v-if="store.isMaster && hiddenParticipants.length" class="hidden-participants">
      <summary>Ocultos do painel ({{ hiddenParticipants.length }})</summary>
      <ul>
        <li v-for="participant in hiddenParticipants" :key="participant.id">
          <span>{{ participant.displayName }} <small>· {{ participant.role === 'Master' ? 'Mestre' : 'Jogador' }}</small></span>
          <button class="button ghost small" type="button" :disabled="busy || store.connection === 'offline'" @click="setHidden(participant.id, false)">Mostrar</button>
        </li>
      </ul>
    </details>
    <div
      v-if="contextMenu"
      class="participant-context-menu"
      role="menu"
      aria-label="Ações do participante"
      :style="{ left: `${contextMenu.x}px`, top: `${contextMenu.y}px` }"
      @click.stop
    >
      <button
        type="button"
        role="menuitem"
        :disabled="busy || store.connection === 'offline'"
        @click="setHidden(contextMenu.participantId, true)"
      >Remover do painel</button>
    </div>
  </section>
</template>
