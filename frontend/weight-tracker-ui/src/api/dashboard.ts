import client from './client'
import { localToday } from '../utils/dates'
import type { DashboardData } from '../types'

export const getDashboard = () => {
  const today = localToday()
  return client.get<DashboardData>('/dashboard', { params: { today } }).then((r) => r.data)
}
