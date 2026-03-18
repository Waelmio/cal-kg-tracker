import client from './client'
import type { DailyLog, UpsertDailyLogRequest } from '../types'

export const getAll = (params?: { from?: string; to?: string; limit?: number }) =>
  client.get<DailyLog[]>('/daily-logs', { params }).then((r) => r.data)

export const getByDate = (date: string) =>
  client.get<DailyLog>(`/daily-logs/${date}`).then((r) => r.data)

export const upsert = (date: string, data: UpsertDailyLogRequest) =>
  client.put<DailyLog>(`/daily-logs/${date}`, data).then((r) => r.data)

export const deleteDay = (date: string) =>
  client.delete(`/daily-logs/${date}`).then(() => {})

export const deleteWeight = (date: string) =>
  client.delete<DailyLog | null>(`/daily-logs/${date}/weight`).then((r) => r.data)

export const deleteCalories = (date: string) =>
  client.delete<DailyLog | null>(`/daily-logs/${date}/calories`).then((r) => r.data)
