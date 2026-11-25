import { onMounted, ref, watch, type Ref } from 'vue'
import { useAppStore } from '../stores/app'

/**
 * Loads data with loading and error state and refetches whenever `app.version` changes
 * (every mutation bumps it), or when any extra source changes.
 */
export function useAsyncData<T>(load: () => Promise<T>, extraSources: Ref<unknown>[] = []) {
  const app = useAppStore()
  const data = ref<T | null>(null) as Ref<T | null>
  const loading = ref(true)
  const error = ref<string | null>(null)
  let seq = 0

  async function run() {
    const mine = ++seq
    loading.value = true
    error.value = null
    try {
      const result = await load()
      if (mine === seq) data.value = result
    } catch (e) {
      if (mine === seq) error.value = (e as Error).message
    } finally {
      if (mine === seq) loading.value = false
    }
  }

  onMounted(run)
  watch(() => app.version, run)
  extraSources.forEach((s) => watch(s, run))

  return { data, loading, error, reload: run }
}
