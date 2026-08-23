<script setup lang="ts">
import type { Participant } from '~/types/campaign'
defineProps<{ participants: Participant[] }>()
</script>

<template>
  <section class="panel participants-panel" aria-labelledby="participants-title">
    <div class="panel-heading">
      <div>
        <p class="eyebrow">Na mesa</p>
        <h2 id="participants-title">Participantes</h2>
      </div>
      <span class="count-pill">{{ participants.filter(p => p.isOnline).length }} online</span>
    </div>
    <ul class="participant-list">
      <li v-for="participant in participants" :key="participant.id">
        <span class="avatar">{{ participant.displayName.slice(0, 1).toUpperCase() }}</span>
        <span class="participant-name">
          {{ participant.displayName }}
          <small>{{ participant.role === 'Master' ? 'Mestre' : 'Jogador' }}</small>
        </span>
        <span class="presence-dot" :class="{ online: participant.isOnline }" :title="participant.isOnline ? 'Online' : 'Offline'" />
      </li>
    </ul>
  </section>
</template>
