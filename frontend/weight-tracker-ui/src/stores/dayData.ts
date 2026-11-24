import { defineStore } from 'pinia'
import { ref, watch } from 'vue'
import * as logApi from '../api/dailyLog'
import * as statsApi from '../api/stats'
import { useAppStore } from './app'
import { addDays, mondayOf } from '../utils/dates'
import type { Calendar, CalendarDay, DailyLog, DashboardStats } from '../types'

/**
 * Everything the log sheet needs, kept in memory so opening it never waits on the network:
 * all logs, the calendar of each week visited (target, planned budget per day) and today's dashboard budget.
 *
 * It is a cache, not a second source of truth. Data is loaded once, requests in flight are shared, and every
 * mutation (`app.version` changes) refetches in the background while the old data stays on screen.
 * Saving also patches the cache straight away so the next read is already right.
 */
export const useDayDataStore = defineStore('dayData', () => {
  const app = useAppStore()

  const logs = ref<DailyLog[]>([])                    // all logs, newest first
  const logsLoaded = ref(false)
  const weeks = ref<Record<string, Calendar>>({})     // by Monday
  const dashboard = ref<DashboardStats | null>(null)
  const error = ref<string | null>(null)

  const inFlight = new Map<string, Promise<void>>()

  /** Runs one request per key at a time: callers asking meanwhile share the same promise. */
  function once(key: string, run: () => Promise<void>): Promise<void> {
    const running = inFlight.get(key)
    if (running) return running
    const p = run()
      .catch((e: Error) => { error.value = e.message })
      .finally(() => inFlight.delete(key))
    inFlight.set(key, p)
    return p
  }

  const loadLogs = () => once('logs', async () => {
    logs.value = await logApi.getAll()
    logsLoaded.value = true
    error.value = null
  })

  const loadWeek = (monday: string) => once(`week:${monday}`, async () => {
    const calendar = await statsApi.getCalendar(monday, addDays(monday, 6))
    // Merge after the await: several weeks load at once and each must keep the others.
    weeks.value = { ...weeks.value, [monday]: calendar }
    error.value = null
  })

  const loadDashboard = () => once('dashboard', async () => {
    dashboard.value = await statsApi.getDashboard()
    error.value = null
  })

  /**
   * Warm the cache around a date: the logs, its week and the neighbouring weeks, and today's budget.
   * Only what is missing is fetched. Cached data is refreshed when something changes (see the watch below).
   */
  function prefetch(date: string) {
    const monday = mondayOf(date)
    const missing = (m: string) => (m in weeks.value ? Promise.resolve() : loadWeek(m))
    return Promise.all([
      logsLoaded.value ? Promise.resolve() : loadLogs(),
      missing(monday),
      missing(addDays(monday, -7)),
      missing(addDays(monday, 7)),
      date === app.today && !dashboard.value ? loadDashboard() : Promise.resolve(),
    ]).then(() => {})
  }

  /** True when the sheet can show this date without waiting. */
  function isReady(date: string) {
    return logsLoaded.value && mondayOf(date) in weeks.value && (date !== app.today || dashboard.value != null)
  }

  const logOn = (date: string) => logs.value.find((l) => l.date === date)
  const weekLogs = (monday: string) => logs.value.filter((l) => l.date >= monday && l.date <= addDays(monday, 6))
  /** The latest weigh-in before a date, however long ago. */
  const lastWeightBefore = (date: string) => logs.value.find((l) => l.date < date && l.weightKg != null)?.weightKg ?? null
  const dayInfo = (date: string): CalendarDay | undefined => weeks.value[mondayOf(date)]?.days.find((d) => d.date === date)

  /** Patch the cache with what was just saved (null: the day was removed). */
  function applySaved(date: string, log: DailyLog | null) {
    const rest = logs.value.filter((l) => l.date !== date)
    logs.value = (log ? [...rest, log] : rest).sort((a, b) => b.date.localeCompare(a.date))
  }

  // Every mutation bumps app.version: refresh what is cached, keeping the old data until the new arrives.
  watch(() => app.version, () => {
    if (!logsLoaded.value) return
    loadLogs()
    Object.keys(weeks.value).forEach((m) => loadWeek(m))
    if (dashboard.value) loadDashboard()
  })

  return { logs, logsLoaded, weeks, dashboard, error, prefetch, isReady, logOn, weekLogs, lastWeightBefore, dayInfo, applySaved, loadLogs }
})
