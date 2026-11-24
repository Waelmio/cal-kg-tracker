import client from './client'
import { localToday } from '../utils/dates'
import type { UserSettings } from '../types'

export const getSettings = () => client.get<UserSettings>('/settings').then((r) => r.data)

export const updateSettings = (data: UserSettings) =>
  client.put<UserSettings>('/settings', { ...data, today: localToday() }).then((r) => r.data)
