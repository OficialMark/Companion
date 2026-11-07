<script setup lang="ts">
import type { Participant } from '~/types/campaign'
defineProps<{ participants: Participant[] }>()
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
        <span class="avatar" :class="{ master: participant.role === 'Master' }">{{ participant.displayName.slice(0, 1).toUpperCase() }}</span>
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
