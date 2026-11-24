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
