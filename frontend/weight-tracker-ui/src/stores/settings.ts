import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { UserSettings } from '../types'
import { getSettings, updateSettings } from '../api/settings'

export const useSettingsStore = defineStore('settings', () => {
  const settings = ref<UserSettings>({ heightCm: null, preferredUnit: 'kg', tdeeKcal: null })

  async function fetch() {
    settings.value = await getSettings()
  }

  async function save(data: UserSettings) {
    settings.value = await updateSettings(data)
  }

  return { settings, fetch, save }
})
