import client from './client'
import { localToday } from '../utils/dates'
import type { Calendar, DashboardStats, LogWeek, MonthSummary, Period, TdeeStats } from '../types'

const today = () => localToday()

export const getDashboard = () =>
  client.get<DashboardStats>('/stats/dashboard', { params: { today: today() } }).then((r) => r.data)

export const getCalendar = (from: string, to: string) =>
  client.get<Calendar>('/stats/calendar', { params: { from, to, today: today() } }).then((r) => r.data)

/** The week around a date, with everything the log sheet shows and edits. */
export const getLogWeek = (date: string) =>
  client.get<LogWeek>('/stats/log-week', { params: { date, today: today() } }).then((r) => r.data)

export const getPeriods = (unit: 'week' | 'month', count: number) =>
  client.get<Period[]>('/stats/periods', { params: { unit, count, today: today() } }).then((r) => r.data)

export const getMonth = (month: string) =>
  client.get<MonthSummary>(`/stats/month/${month}`, { params: { today: today() } }).then((r) => r.data)

export const getTdee = (days: 30 | 60 | 90) =>
  client.get<TdeeStats>('/stats/tdee', { params: { days, today: today() } }).then((r) => r.data)

export const getWeights = (from: string) =>
  client.get<{ date: string; kg: number; avg7: number }[]>('/stats/weights', { params: { from, today: today() } }).then((r) => r.data)

export const getBudgetByWindow = () =>
  client.get<{ weeks: number; todayBudget: number | null }[]>('/stats/budget-by-window', { params: { today: today() } }).then((r) => r.data)
