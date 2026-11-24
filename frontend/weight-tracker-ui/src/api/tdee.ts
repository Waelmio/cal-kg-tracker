import client from './client'
import { localToday } from '../utils/dates'
import type { TdeeComputation } from '../types'

export const getComputedTdee = (days = 90) =>
  client.get<TdeeComputation>('/tdee/computed', { params: { days, today: localToday() } }).then((r) => r.data)
