import client from './client'
import type { TdeeComputation } from '../types'

export const getComputedTdee = (days = 90) =>
  client.get<TdeeComputation>('/tdee/computed', { params: { days } }).then((r) => r.data)
