import client from './client'
import { localToday } from '../utils/dates'
import type { Goal, CreateGoalRequest } from '../types'

export const getActive = () =>
  client.get<Goal>('/goals/active').then((r) => r.data)

export const create = (data: CreateGoalRequest) =>
  client.post<Goal>('/goals', data, { params: { today: localToday() } }).then((r) => r.data)

export const remove = (id: number) =>
  client.delete(`/goals/${id}`)
