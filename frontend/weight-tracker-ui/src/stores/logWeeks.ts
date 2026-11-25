import { defineStore } from 'pinia'
import * as statsApi from '../api/stats'
import { useKeyedCache } from '../composables/useKeyedCache'
import { mondayOf } from '../utils/dates'
import type { LogDay, LogWeek } from '../types'

/** Weeks loaded for the log sheet, one request per week. Saving replaces the whole day, so it must be fresh. */
export const useLogWeeksStore = defineStore('logWeeks', () => {
  const cache = useKeyedCache<LogWeek>((monday) => statsApi.getLogWeek(monday))

  const load = (date: string) => cache.load(mondayOf(date))
  const week = (date: string): LogWeek | undefined => cache.get(mondayOf(date))
  const day = (date: string): LogDay | undefined => week(date)?.days.find((d) => d.date === date)

  return { error: cache.error, load, week, day }
})
