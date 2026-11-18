import { defineStore } from 'pinia'

/** After a miss (404/offline) the same image is retried no sooner than this. */
export const IMAGE_RETRY_MS = 60_000

const inflight = new Map<string, Promise<void>>()

/**
 * Object URLs for authenticated image endpoints (creature images, spec 008). The API needs
 * the participant token, so <img src> cannot point at it directly; each URL is fetched once
 * and kept as a blob URL. URLs carry the image revision, so a new picture is a new key.
 * Concurrent calls for the same URL share one request (five "Goblin N" tokens = one fetch).
 */
export const useImageCacheStore = defineStore('imageCache', {
  state: () => ({
    urls: {} as Record<string, string>,
    failedAt: {} as Record<string, number>
  }),
  actions: {
    ensure(apiPath: string | null | undefined): Promise<void> {
      if (!apiPath || this.urls[apiPath] || typeof window === 'undefined') return Promise.resolve()
      const failed = this.failedAt[apiPath]
      if (failed && Date.now() - failed < IMAGE_RETRY_MS) return Promise.resolve()
      const running = inflight.get(apiPath)
      if (running) return running
      const store = useCampaignStore()
      const session = store.session
      if (!session) return Promise.resolve()
      const request = (async () => {
        try {
          const blob = await useCompanionApi().battlemapImage(session, apiPath)
          // The session may have ended (or changed) while the request was in flight.
          if (store.session !== session) return
          this.urls[apiPath] = URL.createObjectURL(blob)
          delete this.failedAt[apiPath]
        } catch {
          // No image (404) or offline: callers fall back to initials until the retry window.
          this.failedAt[apiPath] = Date.now()
        } finally {
          inflight.delete(apiPath)
        }
      })()
      inflight.set(apiPath, request)
      return request
    },
    clear() {
      for (const url of Object.values(this.urls)) URL.revokeObjectURL(url)
      this.urls = {}
      this.failedAt = {}
      inflight.clear()
    }
  }
})
