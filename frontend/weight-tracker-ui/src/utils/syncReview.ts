import type { SyncApplyResult, SyncChoice, SyncResult, SyncRow } from '../types'
import { addDays, mondayOf } from './dates'
import { shortDate } from './format'

/** Identifies a row: one field of one day. */
export const rowKey = (r: Pick<SyncRow, 'date' | 'field'>): string => `${r.date}:${r.field}`

export interface SyncWeek {
  monday: string
  title: string
  rows: SyncRow[]
}

/** Rows under their week (Monday to Sunday), the most recent week first, days in order inside it. */
export function groupByWeek(rows: SyncRow[]): SyncWeek[] {
  const weeks = new Map<string, SyncRow[]>()
  for (const row of rows) {
    const monday = mondayOf(row.date)
    weeks.set(monday, [...(weeks.get(monday) ?? []), row])
  }
  return [...weeks.entries()]
    .sort(([a], [b]) => (a < b ? 1 : -1))
    .map(([monday, list]) => ({ monday, title: `${shortDate(monday)} to ${shortDate(addDays(monday, 6), true)}`, rows: list }))
}

const plural = (n: number, one: string, many = one + 's') => `${n} ${n === 1 ? one : many}`

/** "12 to write, 4 kept" for the choices made so far. */
export function choiceSummary(rows: SyncRow[], choices: Record<string, SyncChoice>): string {
  const write = rows.filter((r) => choices[rowKey(r)] === 'Replace').length
  return `${write} to write, ${rows.length - write} kept`
}

/** What an automatic sync did: "Synced: 3 calories and 1 weight updated." */
export function syncSummary(r: SyncResult): string {
  const { calories, weight } = r
  const text = calories.written + weight.written === 0
    ? 'Synced: nothing new'
    : `Synced: ${plural(calories.written, 'calorie value')} and ${plural(weight.written, 'weight')} updated`
  const kept = calories.kept + weight.kept
  return kept ? `${text}, ${plural(kept, 'value')} you logged yourself left alone.` : `${text}.`
}

export function applySummary(r: SyncApplyResult): string {
  const text = `${r.added} added, ${r.replaced} replaced, ${r.kept} kept.`
  return r.changed ? `${text} ${plural(r.changed, 'value')} skipped, changed in the meantime.` : text
}
