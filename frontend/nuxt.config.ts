const apiTarget = (process.env.NUXT_API_TARGET || 'http://localhost:8080').replace(/\/+$/, '')

export default defineNuxtConfig({
  compatibilityDate: '2026-08-01',
  ssr: false,
  devtools: { enabled: true },
  modules: ['@pinia/nuxt', '@nuxt/icon'],
  icon: {
    // Icons are bundled into the client (Lucide set installed locally), so the
    // static nginx build never needs the Iconify API or a server endpoint.
    mode: 'svg',
    class: 'icon',
    size: '1.1em',
    provider: 'none',
    // .ts is scanned too: icon names living in plain modules (utils/statusConditions.ts)
    // would otherwise be missing from the bundle and render as empty <svg>s.
    clientBundle: {
      scan: {
        globInclude: ['**/*.{vue,ts,jsx,tsx}'],
        globExclude: ['node_modules', 'dist', 'build', 'coverage', 'test', 'tests', '.*']
      },
      sizeLimitKb: 256
    }
  },
  css: ['~/assets/css/main.css', '~/assets/css/dice.css', '~/assets/css/battlemap.css', '~/assets/css/creatures.css'],
  runtimeConfig: {
    public: {
      apiBase: '/api',
      hubUrl: '/hubs/campaign'
    }
  },
  app: {
    head: {
      title: 'Companion — Mesa de D&D',
      htmlAttrs: { lang: 'pt-BR' },
      meta: [
        { name: 'viewport', content: 'width=device-width, initial-scale=1, viewport-fit=cover' },
        { name: 'theme-color', content: '#191419' },
        { name: 'description', content: 'Dados e iniciativa compartilhados para sua campanha de D&D.' }
      ],
      link: [
        { rel: 'manifest', href: '/manifest.webmanifest' },
        { rel: 'icon', href: '/icon.svg', type: 'image/svg+xml' }
      ]
    }
  },
  nitro: {
    // NUXT_API_TARGET lets a second dev instance point at its own API (spec 011 quickstart)
    // without touching the stack that is serving the table.
    devProxy: {
      '/api': { target: `${apiTarget}/api`, changeOrigin: true },
      '/hubs': { target: `${apiTarget}/hubs`, changeOrigin: true, ws: true }
    }
  }
})
