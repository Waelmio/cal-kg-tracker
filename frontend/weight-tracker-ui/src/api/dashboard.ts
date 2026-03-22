import client from './client'
import type { DashboardData } from '../types'

export const getDashboard = () => {
  const today = new Date().toLocaleDateString('en-CA') // yyyy-MM-dd in local timezone
  return client.get<DashboardData>('/dashboard', { params: { today } }).then((r) => r.data)
}
