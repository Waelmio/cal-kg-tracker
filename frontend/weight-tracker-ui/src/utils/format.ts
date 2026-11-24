import type { Status, WeightUnit } from '../types'
import { displayWeight, toUnit } from './units'

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
  const v = Math.abs(toUnit(deltaKg, unit))
  if (Number(v.toFixed(digits)) === 0) return `0.${'0'.repeat(digits)}`
  return `${deltaKg > 0 ? '▲' : '▼'} ${v.toFixed(digits)}`
}

/** A weight amount without needless decimals: "78", "0.41". */
export const amount = (kg: number, unit: WeightUnit): string => String(Math.round(toUnit(kg, unit) * 100) / 100)

/** "+25", "−30", "0". Uses a real minus sign. */
export const signed = (n: number): string => (n > 0 ? `+${n}` : n < 0 ? `−${Math.abs(n)}` : '0')

export const STATUS_COLORS: Record<Status, { solid: string; light: string; soft: string; text: string }> = {
  under: { solid: '#00bc7d', light: '#a4f4cf', soft: '#d0fae5', text: '#007a55' },
  balanced: { solid: '#ffb900', light: '#fee685', soft: '#fef3c6', text: '#973c00' },
  over: { solid: '#ff2056', light: '#ffccd3', soft: '#ffe4e6', text: '#c70036' },
}

export const statusText = (s: Status | null): string =>
  s === 'under' ? 'On or under target' : s === 'balanced' ? 'Over, average ok' : s === 'over' ? 'Over target' : 'No log'

/** Calendar cell palettes from the Calendar mockup: [background, text]. "Out" = outside the current goal (lighter). */
export const CALENDAR_COLORS = {
  day: { under: ['#d0fae5', '#006045'], balanced: ['#fef3c6', '#973c00'], over: ['#ffe4e6', '#a50036'] },
  dayOut: { under: ['#f3fcf8', '#3b6b58'], balanced: ['#fffcf0', '#8a5a1c'], over: ['#fff7f8', '#9d4358'] },
  week: { under: ['#00bc7d', '#ffffff'], balanced: ['#ffb900', '#ffffff'], over: ['#ff2056', '#ffffff'] },
  weekOut: { under: ['#a4f4cf', '#006045'], balanced: ['#fee685', '#973c00'], over: ['#ffccd3', '#a50036'] },
} as const satisfies Record<string, Record<Status, readonly [string, string]>>

/** Days without a status yet: [background, text, border]. A fill always means "logged", a dashed border "planned". */
export const PENDING_COLORS = {
  today: ['#e0f2fe', '#075985', '#0369a1'],       // logged today, still in progress
  ahead: ['#e2e8f0', '#1e293b', '#64748b'],       // a future day that is already logged
  planned: ['#ffffff', '#6b7280', '#9ca3af'],
} as const
