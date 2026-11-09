<script setup lang="ts">
import { AVATAR_ACCEPT, prepareAvatar } from '~/utils/avatarImage'
import UserAvatar from './UserAvatar.vue'

const emit = defineEmits<{ leave: [] }>()
const store = useCampaignStore()
const api = useCompanionApi()
const open = ref(false)
const busy = ref(false)
const error = ref('')
const root = ref<HTMLElement | null>(null)
const trigger = ref<HTMLButtonElement | null>(null)
const fileInput = ref<HTMLInputElement | null>(null)

const participant = computed(() => store.snapshot?.currentParticipant ?? null)
const name = computed(() => participant.value?.displayName ?? store.session?.displayName ?? '')
const isMaster = computed(() => store.session?.role === 'Master')
const photo = computed(() => participant.value ? store.avatarUrls[participant.value.id] ?? null : null)
const hasPhoto = computed(() => Boolean(participant.value?.avatarRevision))
const offline = computed(() => store.connection === 'offline')

function close(focusTrigger = false) {
  open.value = false
  if (focusTrigger) trigger.value?.focus()
}
function toggle() {
  open.value = !open.value
  if (open.value) error.value = ''
}
function onDocumentPointer(event: PointerEvent) {
  if (open.value && root.value && !root.value.contains(event.target as Node)) close()
}
function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape' && open.value) close(true)
}

function choosePhoto() {
  error.value = ''
  fileInput.value?.click()
}

async function onFileSelected(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file || !store.session) return
  busy.value = true
  error.value = ''
  try {
    const prepared = await prepareAvatar(file)
    store.applyOwnParticipant(await api.uploadAvatar(store.session, prepared.blob, prepared.fileName))
    close()
  } catch (exception) {
    error.value = (exception as Error).message
  } finally {
    busy.value = false
  }
}

async function removePhoto() {
  if (!store.session) return
  busy.value = true
  error.value = ''
  try {
    store.applyOwnParticipant(await api.removeAvatar(store.session))
    close()
  } catch (exception) {
    error.value = (exception as Error).message
  } finally {
    busy.value = false
  }
}

function leave() {
  close()
  emit('leave')
}

onMounted(() => {
  document.addEventListener('pointerdown', onDocumentPointer)
  document.addEventListener('keydown', onKeydown)
})
onBeforeUnmount(() => {
  document.removeEventListener('pointerdown', onDocumentPointer)
  document.removeEventListener('keydown', onKeydown)
})
</script>

<template>
  <div ref="root" class="user-menu">
    <button
      ref="trigger"
      class="user-menu-trigger"
      :class="{ open }"
      type="button"
      aria-haspopup="menu"
      :aria-expanded="open"
      aria-controls="user-menu-panel"
      :aria-label="`Menu de ${name}`"
      :title="`Menu de ${name}`"
      @click="toggle"
    >
      <UserAvatar :name="name" :src="photo" :master="isMaster" :size="34" />
      <Icon name="lucide:chevron-down" class="user-menu-chevron" />
    </button>

    <Transition name="menu-pop">
      <div v-if="open" id="user-menu-panel" class="user-menu-panel" role="menu" :aria-label="`Opções de ${name}`">
        <div class="user-menu-header">
          <UserAvatar :name="name" :src="photo" :master="isMaster" :size="48" />
          <div class="user-menu-identity">
            <strong>{{ name }}</strong>
            <small><Icon :name="isMaster ? 'lucide:crown' : 'lucide:user-round'" />{{ isMaster ? 'Mestre' : 'Jogador' }}</small>
          </div>
        </div>

        <button class="user-menu-item" type="button" role="menuitem" :disabled="busy || offline" @click="choosePhoto">
          <Icon :name="busy ? 'lucide:loader-circle' : 'lucide:camera'" :class="{ spin: busy }" />
          <span>
            {{ busy ? 'Enviando foto…' : hasPhoto ? 'Trocar foto' : 'Enviar foto' }}
            <small>{{ isMaster ? 'Aparece no seu perfil' : 'Aparece no seu perfil e no seu pino do mapa' }}</small>
          </span>
        </button>
        <button v-if="hasPhoto" class="user-menu-item" type="button" role="menuitem" :disabled="busy || offline" @click="removePhoto">
          <Icon name="lucide:image-off" />
          <span>Remover foto</span>
        </button>
        <p v-if="offline" class="user-menu-note"><Icon name="lucide:wifi-off" />Conecte-se para alterar a foto.</p>
        <p v-if="error" class="form-error user-menu-error" role="alert"><Icon name="lucide:circle-alert" />{{ error }}</p>

        <div class="user-menu-separator" role="separator" />
        <button class="user-menu-item danger" type="button" role="menuitem" @click="leave">
          <Icon name="lucide:log-out" />
          <span>Sair da campanha</span>
        </button>
      </div>
    </Transition>

    <input ref="fileInput" class="sr-only" type="file" :accept="AVATAR_ACCEPT" tabindex="-1" aria-hidden="true" @change="onFileSelected">
  </div>
</template>
