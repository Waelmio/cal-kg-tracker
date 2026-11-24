import client from './client'
import { localToday } from '../utils/dates'
import type { CalorieGoal } from '../types'

export const getAll = () => client.get<CalorieGoal[]>('/calorie-goals').then((r) => r.data)

/** A null target turns the target off from today. */
export const create = (targetCalories: number | null) =>
  client.post<CalorieGoal>('/calorie-goals', { targetCalories }, { params: { today: localToday() } }).then((r) => r.data)

export const remove = (id: number) => client.delete(`/calorie-goals/${id}`).then(() => {})
