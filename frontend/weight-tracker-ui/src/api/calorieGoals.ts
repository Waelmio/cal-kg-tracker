import client from './client'
import type { CalorieGoal } from '../types'

export const getAll = () =>
  client.get<CalorieGoal[]>('/calorie-goals').then((r) => r.data)

export const getActive = () =>
  client.get<CalorieGoal>('/calorie-goals/active').then((r) => r.data)

export const create = (targetCalories: number) =>
  client.post<CalorieGoal>('/calorie-goals', { targetCalories }).then((r) => r.data)

export const remove = (id: number) =>
  client.delete(`/calorie-goals/${id}`)
