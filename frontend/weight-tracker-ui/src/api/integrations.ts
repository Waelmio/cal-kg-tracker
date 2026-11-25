import client from './client'
import { localToday } from '../utils/dates'
import type { Integration, SyncApplyResult, SyncDecision, SyncPreview, SyncResult } from '../types'

const today = () => ({ params: { today: localToday() } })

export const getAll = () => client.get<Integration[]>('/integrations').then((r) => r.data)

/** The address to send the browser to. With a PIN, the provider shows a code to type here instead of coming back. */
export const connect = (id: string, usePin = false) =>
  client.post<{ authorizeUrl: string }>(`/integrations/${id}/connect`, { usePin }).then((r) => r.data.authorizeUrl)

export const verifyPin = (id: string, verifier: string) =>
  client.post(`/integrations/${id}/verify`, { verifier }).then(() => {})

export const sync = (id: string) =>
  client.post<SyncResult>(`/integrations/${id}/sync`, null, today()).then((r) => r.data)

/** The differences over a period. Writes nothing. */
export const preview = (id: string, from: string, to: string) =>
  client.post<SyncPreview>(`/integrations/${id}/sync/preview`, { from, to }, today()).then((r) => r.data)

export const apply = (id: string, previewId: string, decisions: SyncDecision[]) =>
  client.post<SyncApplyResult>(`/integrations/${id}/sync/apply`, { previewId, decisions }, today()).then((r) => r.data)

export const disconnect = (id: string) => client.delete(`/integrations/${id}`).then(() => {})
