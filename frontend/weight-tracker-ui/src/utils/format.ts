import type { Status, WeightUnit } from '../types'
import { displayWeight } from './units'

const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']
const WEEKDAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']

const parts = (date: string) => {
  const [y, m, d] = date.split('-').map(Number)
  return { y, m, d, weekday: new Date(Date.UTC(y, m - 1, d)).getUTCDay() }
}

/** "Tuesday 24 November" */
export const longDate = (date: string): string => {
  const p = parts(date)
  return `${WEEKDAYS[p.weekday]} ${p.d} ${MONTHS[p.m - 1]}`
}

/** "24 Nov" or, with a year, "24 Nov 2026" */
export const shortDate = (date: string, withYear = false): string => {
  const p = parts(date)
  return `${p.d} ${MONTHS[p.m - 1].slice(0, 3)}${withYear ? ' ' + p.y : ''}`
}

/** "24 November 2026" */
export const fullDate = (date: string): string => {
  const p = parts(date)
  return `${p.d} ${MONTHS[p.m - 1]} ${p.y}`
}

export const monthTitle = (month: string): string => {
  const [y, m] = month.split('-').map(Number)
  return `${MONTHS[m - 1]} ${y}`
}

export const weekdayShort = (date: string): string => WEEKDAYS[parts(date).weekday].slice(0, 3)

/** "Today", "Yesterday", "Tomorrow" or "Tue 24 Nov" */
export const relativeDay = (date: string, today: string): string => {
  const diff = Math.round((Date.UTC(...ymd(date)) - Date.UTC(...ymd(today))) / 86400000)
  if (diff === 0) return 'Today'
  if (diff === -1) return 'Yesterday'
  if (diff === 1) return 'Tomorrow'
  return `${weekdayShort(date)} ${shortDate(date)}`
}

const ymd = (date: string): [number, number, number] => {
  const p = parts(date)
  return [p.y, p.m - 1, p.d]
}

/** Calories are shown as plain integers: no thousand separators. */
export const kcal = (n: number | null | undefined): string => (n == null ? '–' : String(Math.round(n)))

/** Weight in the user's unit, one decimal. */
export const weight = (kg: number | null | undefined, unit: WeightUnit, withUnit = false): string => {
  if (kg == null) return '–'
  const text = displayWeight(kg, unit).toFixed(1)
  return withUnit ? `${text} ${unit}` : text
}

/** Weight change with the triangle used only for weight: ▲ up, ▼ down. */
export const weightChange = (deltaKg: number | null | undefined, unit: WeightUnit, digits = 1): string => {
  if (deltaKg == null) return ''
  const v = Math.abs(displayWeight(deltaKg, unit))
  if (Number(v.toFixed(digits)) === 0) return `0.${'0'.repeat(digits)}`
  return `${deltaKg > 0 ? '▲' : '▼'} ${v.toFixed(digits)}`
}

/** "+25", "−30", "0". Uses a real minus sign. */
export const signed = (n: number): string => (n > 0 ? `+${n}` : n < 0 ? `−${Math.abs(n)}` : '0')

export const STATUS_COLORS: Record<Status, { solid: string; light: string; soft: string; text: string }> = {
  under: { solid: '#00bc7d', light: '#a4f4cf', soft: '#d0fae5', text: '#007a55' },
  balanced: { solid: '#ffb900', light: '#fee685', soft: '#fef3c6', text: '#973c00' },
  over: { solid: '#ff2056', light: '#ffccd3', soft: '#ffe4e6', text: '#c70036' },
}

export const statusText = (s: Status | null): string =>
  s === 'under' ? 'On or under target' : s === 'balanced' ? 'Over, average ok' : s === 'over' ? 'Over target' : 'No log'
