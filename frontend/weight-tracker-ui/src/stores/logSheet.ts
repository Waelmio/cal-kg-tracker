import { defineStore } from 'pinia'
import { ref } from 'vue'

/** The log sheet is one global component; anything can open it for a date. */
export const useLogSheetStore = defineStore('logSheet', () => {
  const date = ref<string | null>(null)
  const isOpen = ref(false)

  function open(forDate: string) {
    date.value = forDate
    isOpen.value = true
  }

  function close() {
    isOpen.value = false
  }

  return { date, isOpen, open, close }
})
