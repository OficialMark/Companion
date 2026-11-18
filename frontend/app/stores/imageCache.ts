import { defineStore } from 'pinia'

/**
 * Object URLs for authenticated image endpoints (creature images, spec 008). The API needs
 * the participant token, so <img src> cannot point at it directly; each URL is fetched once
 * and kept as a blob URL. URLs carry the image revision, so a new picture is a new key.
 */
export const useImageCacheStore = defineStore('imageCache', {
  state: () => ({
    urls: {} as Record<string, string>,
    failed: {} as Record<string, true>
  }),
  actions: {
    async ensure(apiPath: string | null | undefined) {
      if (!apiPath || this.urls[apiPath] || this.failed[apiPath] || typeof window === 'undefined') return
      const store = useCampaignStore()
      if (!store.session) return
      pending.add(apiPath)
      try {
        const blob = await useCompanionApi().battlemapImage(store.session, apiPath)
        this.urls[apiPath] = URL.createObjectURL(blob)
      } catch {
        // No image (404) or offline: callers fall back to initials.
        this.failed[apiPath] = true
      } finally {
        pending.delete(apiPath)
      }
    },
    clear() {
      for (const url of Object.values(this.urls)) URL.revokeObjectURL(url)
      this.urls = {}
      this.failed = {}
    }
  }
})

const pending = new Set<string>()
