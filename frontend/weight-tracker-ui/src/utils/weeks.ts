// All Date objects passed to these functions must represent UTC midnight (e.g. created via
// new Date(dateStr + 'T00:00:00Z')). UTC components are used throughout so that local-timezone
// offsets never shift the calendar date.

export function getISOWeek(date: Date): number {
  const d = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate()))
  const dayNum = d.getUTCDay() || 7
  d.setUTCDate(d.getUTCDate() + 4 - dayNum)
  const yearStart = new Date(Date.UTC(d.getUTCFullYear(), 0, 1))
  return Math.ceil(((d.getTime() - yearStart.getTime()) / 86400000 + 1) / 7)
}

export function getISOWeekYear(date: Date): number {
  const d = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate()))
  const dayNum = d.getUTCDay() || 7
  d.setUTCDate(d.getUTCDate() + 4 - dayNum)
  return d.getUTCFullYear()
}

export function getWeekDates(isoWeekYear: number, isoWeek: number): string[] {
  const jan4 = new Date(Date.UTC(isoWeekYear, 0, 4))
  const jan4Day = jan4.getUTCDay() || 7
  const week1Monday = new Date(jan4)
  week1Monday.setUTCDate(jan4.getUTCDate() + 1 - jan4Day)
  const monday = new Date(week1Monday)
  monday.setUTCDate(week1Monday.getUTCDate() + (isoWeek - 1) * 7)
  return Array.from({ length: 7 }, (_, i) => {
    const d = new Date(monday)
    d.setUTCDate(monday.getUTCDate() + i)
    return d.toISOString().slice(0, 10)
  })
}

export function getLast6Weeks(): Array<{ year: number; week: number }> {
  const result: Array<{ year: number; week: number }> = []
  const today = new Date(new Date().toLocaleDateString('en-CA') + 'T00:00:00Z')
  for (let i = 5; i >= 0; i--) {
    const d = new Date(today)
    d.setUTCDate(today.getUTCDate() - i * 7)
    result.push({ year: getISOWeekYear(d), week: getISOWeek(d) })
  }
  return result
}
