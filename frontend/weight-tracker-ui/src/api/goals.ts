import client from './client'
import { localToday } from '../utils/dates'
import type { CreateGoalRequest, Goal, UpdateGoalRequest } from '../types'

const params = () => ({ today: localToday() })

export const getAll = () => client.get<Goal[]>('/goals').then((r) => r.data)

export const create = (data: CreateGoalRequest) =>
  client.post<Goal>('/goals', data, { params: params() }).then((r) => r.data)

export const update = (id: number, data: UpdateGoalRequest) =>
  client.put<Goal>(`/goals/${id}`, data).then((r) => r.data)

export const end = (id: number, reason: 'Ended' | 'Reached') =>
  client.post<Goal>(`/goals/${id}/end`, { reason }, { params: params() }).then((r) => r.data)

export const maintain = () =>
  client.post<Goal>('/goals/maintain', null, { params: params() }).then((r) => r.data)

export const remove = (id: number) => client.delete(`/goals/${id}`).then(() => {})
