import client from './client'
import { localToday } from '../utils/dates'
import type { TdeeEntry } from '../types'

export const getEntries = () => client.get<TdeeEntry[]>('/tdee-entries').then((r) => r.data)

export const addEntry = (kcal: number, source: 'Manual' | 'Estimate' = 'Manual') =>
  client.post<TdeeEntry>('/tdee-entries', { kcal, source }, { params: { today: localToday() } }).then((r) => r.data)
