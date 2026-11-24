import { ref, shallowReactive, watch } from 'vue'
import { useAppStore } from '../stores/app'

/**
 * Remembers what was loaded under each key, so going back to it (or to a preloaded neighbour) is instant.
 * One request per key at a time. Any change (`app.version`) drops everything, and an answer asked for before
 * the change is ignored: nothing older than the last change is ever handed out.
 */
export function useKeyedCache<T>(fetch: (key: string) => Promise<T>) {
  const app = useAppStore()
  const items = shallowReactive(new Map<string, T>())
  const error = ref<string | null>(null)
  const pending = new Map<string, Promise<void>>()

  function load(key: string): Promise<void> {
    if (items.has(key)) return Promise.resolve()
    const running = pending.get(key)
    if (running) return running

    const version = app.version
    const request: Promise<void> = fetch(key)
      .then((item) => {
        if (version !== app.version) return
        items.set(key, item)
        error.value = null
      })
      .catch((e: Error) => { if (version === app.version) error.value = e.message })
      .finally(() => { if (pending.get(key) === request) pending.delete(key) })
    pending.set(key, request)
    return request
  }

  // Synchronous: a load started right after a change must not be served something from before it.
  watch(() => app.version, () => {
    items.clear()
    pending.clear()
  }, { flush: 'sync' })

  return { error, load, get: (key: string): T | undefined => items.get(key) }
}
