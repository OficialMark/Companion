<script setup lang="ts">
import type { Participant } from '~/types/campaign'
import UserAvatar from './UserAvatar.vue'
withDefaults(defineProps<{ participants: Participant[]; avatars?: Record<string, string> }>(), { avatars: () => ({}) })
</script>

<template>
  <section class="panel participants-panel" aria-labelledby="participants-title">
    <div class="panel-heading">
      <div>
        <p class="eyebrow"><Icon name="lucide:users" />Na mesa</p>
        <h2 id="participants-title">Participantes</h2>
      </div>
      <span class="count-pill"><span class="presence-dot online" aria-hidden="true" />{{ participants.filter(p => p.isOnline).length }} online</span>
    </div>
    <ul class="participant-list">
      <li v-for="participant in participants" :key="participant.id" :class="{ offline: !participant.isOnline }">
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
  </section>
</template>
