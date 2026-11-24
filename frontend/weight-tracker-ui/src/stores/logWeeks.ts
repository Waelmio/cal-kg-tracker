import { defineStore } from 'pinia'
import { ref, watch } from 'vue'
import * as statsApi from '../api/stats'
import { useAppStore } from './app'
import { mondayOf } from '../utils/dates'
import type { LogDay, LogWeek } from '../types'

/**
 * Weeks already loaded for the log sheet, so opening it or moving to a neighbouring week is instant.
 * One request per week. Any change (`app.version`) makes every week stale: the sheet never fills its form
 * from data older than the last change, because saving replaces the whole day.
 */
export const useLogWeeksStore = defineStore('logWeeks', () => {
  const app = useAppStore()
  const weeks = ref<Record<string, LogWeek>>({})
  const error = ref<string | null>(null)
  const pending = new Map<string, Promise<void>>()

  function load(date: string): Promise<void> {
    const monday = mondayOf(date)
    if (weeks.value[monday]) return Promise.resolve()
    const running = pending.get(monday)
    if (running) return running

    const version = app.version
    const request: Promise<void> = statsApi.getLogWeek(monday)
      .then((week) => {
        if (version !== app.version) return     // something changed meanwhile: this answer is already old
        weeks.value[monday] = week
        error.value = null
      })
      .catch((e: Error) => { if (version === app.version) error.value = e.message })
      .finally(() => { if (pending.get(monday) === request) pending.delete(monday) })
    pending.set(monday, request)
    return request
  }

  // Synchronous: a load started right after a change must not be served a week from before it.
  watch(() => app.version, () => {
    weeks.value = {}
    pending.clear()
  }, { flush: 'sync' })

  const week = (date: string): LogWeek | undefined => weeks.value[mondayOf(date)]
  const day = (date: string): LogDay | undefined => week(date)?.days.find((d) => d.date === date)

  return { weeks, error, load, week, day }
})
