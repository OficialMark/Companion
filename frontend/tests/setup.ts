import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'

Object.assign(globalThis, { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, watch })

import { config } from '@vue/test-utils'
// <Icon> is auto-registered by @nuxt/icon at runtime; render a lightweight stub in unit tests.
config.global.stubs = { ...config.global.stubs, Icon: { props: ['name'], template: '<i class="icon" :data-icon="name" aria-hidden="true" />' } }
