import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import type { UserSettings } from '../types'
import * as settingsApi from '../api/settings'
import { localToday } from '../utils/dates'

/**
 * App-wide state: settings, the local date, and a version counter. Every mutation calls `refresh()`;
 * views watch `version` and refetch their statistics. Nothing is computed on the client.
 */
export const useAppStore = defineStore('app', () => {
  const settings = ref<UserSettings>({ heightCm: null, preferredUnit: 'kg', tdeeKcal: null, averagingWeeks: 2 })
  const today = ref(localToday())
  const version = ref(0)
  const loaded = ref(false)

  const unit = computed(() => settings.value.preferredUnit)

  async function loadSettings() {
    settings.value = await settingsApi.getSettings()
    loaded.value = true
  }

  async function saveSettings(data: UserSettings) {
    settings.value = await settingsApi.updateSettings(data)
    refresh()
  }

  /** Re-read the local date (the tab may have been open overnight) and ask every view to refetch. */
  function refresh() {
    today.value = localToday()
    version.value++
  }

  return { settings, today, version, loaded, unit, loadSettings, saveSettings, refresh }
})
