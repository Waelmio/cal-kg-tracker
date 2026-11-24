import client from './client'
import { localToday } from '../utils/dates'
import type { DailyLog, SaveDayRequest } from '../types'

export const getAll = (params?: { from?: string; to?: string; limit?: number }) =>
  client.get<DailyLog[]>('/daily-logs', { params }).then((r) => r.data)

/** Full replace of the day. Resolves to null when nothing is left and the day was removed. */
export const saveDay = (date: string, data: SaveDayRequest) =>
  client
    .put<DailyLog | ''>(`/daily-logs/${date}`, { date, ...data }, { params: { today: localToday() } })
    .then((r) => (r.status === 204 || !r.data ? null : (r.data as DailyLog)))

export const deleteDay = (date: string) =>
  client.delete(`/daily-logs/${date}`).then(() => {})
