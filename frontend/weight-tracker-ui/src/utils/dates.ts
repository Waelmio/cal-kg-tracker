// All "today" values come from the browser's local calendar, never from UTC.

const pad = (n: number) => String(n).padStart(2, '0')

/** yyyy-MM-dd of a Date in the browser's local time zone. */
export const toLocalDateString = (d: Date): string =>
  `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`

/** Today's local date as yyyy-MM-dd. */
export const localToday = (): string => toLocalDateString(new Date())

/** Adds days to a yyyy-MM-dd string using calendar arithmetic (DST-safe). */
export const addDays = (date: string, days: number): string => {
  const [y, m, d] = date.split('-').map(Number)
  const utc = new Date(Date.UTC(y, m - 1, d + days))
  return `${utc.getUTCFullYear()}-${pad(utc.getUTCMonth() + 1)}-${pad(utc.getUTCDate())}`
}

/** Monday of the ISO week containing the date (yyyy-MM-dd in, yyyy-MM-dd out). */
export const mondayOf = (date: string): string => {
  const [y, m, d] = date.split('-').map(Number)
  const dow = new Date(Date.UTC(y, m - 1, d)).getUTCDay()
  return addDays(date, -((dow + 6) % 7))
}

/** yyyy-MM of a date. */
export const monthOf = (date: string): string => date.slice(0, 7)

/** First day of the month after (or before, with a negative count) the given yyyy-MM. */
export const addMonths = (month: string, count: number): string => {
  const [y, m] = month.split('-').map(Number)
  const t = new Date(Date.UTC(y, m - 1 + count, 1))
  return `${t.getUTCFullYear()}-${pad(t.getUTCMonth() + 1)}`
}

export const lastDayOfMonth = (month: string): string => addDays(`${addMonths(month, 1)}-01`, -1)

/** ISO week number (display only: the server owns every statistic). */
export const isoWeek = (date: string): number => {
  const [y, m, d] = date.split('-').map(Number)
  const t = new Date(Date.UTC(y, m - 1, d))
  const day = t.getUTCDay() || 7
  t.setUTCDate(t.getUTCDate() + 4 - day)
  const yearStart = Date.UTC(t.getUTCFullYear(), 0, 1)
  return Math.ceil(((t.getTime() - yearStart) / 86400000 + 1) / 7)
}

/** Days from a to b (b - a), calendar-wise. */
export const daysBetween = (a: string, b: string): number => {
  const [ya, ma, da] = a.split('-').map(Number)
  const [yb, mb, db] = b.split('-').map(Number)
  return Math.round((Date.UTC(yb, mb - 1, db) - Date.UTC(ya, ma - 1, da)) / 86400000)
}
