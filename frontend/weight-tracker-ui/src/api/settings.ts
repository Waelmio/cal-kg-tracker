import client from './client'
import type { UserSettings } from '../types'

export const getSettings = () =>
  client.get<UserSettings>('/settings').then((r) => r.data)

export const updateSettings = (data: UserSettings) =>
  client.put<UserSettings>('/settings', data).then((r) => r.data)
