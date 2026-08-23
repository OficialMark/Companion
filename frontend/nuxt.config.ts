export default defineNuxtConfig({
  compatibilityDate: '2026-08-01',
  ssr: false,
  devtools: { enabled: true },
  modules: ['@pinia/nuxt'],
  css: ['~/assets/css/main.css'],
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
    devProxy: {
      '/api': { target: 'http://localhost:8080/api', changeOrigin: true },
      '/hubs': { target: 'http://localhost:8080/hubs', changeOrigin: true, ws: true }
    }
  }
})
