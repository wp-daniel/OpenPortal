import { api, withQuery } from './client'
import type { AccessRequest, AccessRequestPage, MyAccessRequest, RequestableApplication } from './types'

/** The signed-in user's side: the applications they may ask for, and their requests. */
export const myAccessRequestsApi = {
  catalog: (signal?: AbortSignal) => api.get<RequestableApplication[]>('/api/account/access-requests/catalog', signal),

  list: (signal?: AbortSignal) => api.get<MyAccessRequest[]>('/api/account/access-requests', signal),

  submit: (applicationId: string, reason: string | null) =>
    api.post<MyAccessRequest>('/api/account/access-requests', { applicationId, reason }),

  /** Withdraws a pending request. */
  cancel: (id: string) => api.delete<void>(`/api/account/access-requests/${id}`),
}

/** The reviewers' side (administrators and holders of the access requests page). */
export const accessRequestsApi = {
  list: (params: { page: number; pageSize: number; status?: string; applicationId?: string }, signal?: AbortSignal) =>
    api.get<AccessRequestPage>(
      withQuery('/api/admin/access-requests', {
        page: params.page,
        pageSize: params.pageSize,
        status: params.status,
        applicationId: params.applicationId,
      }),
      signal,
    ),

  approve: (id: string, roles: readonly string[], note: string | null) =>
    api.post<AccessRequest>(`/api/admin/access-requests/${id}/approve`, { roles, note }),

  reject: (id: string, note: string | null) => api.post<AccessRequest>(`/api/admin/access-requests/${id}/reject`, { note }),
}

export const requestKeys = {
  catalog: ['access-request-catalog'] as const,
  mine: ['my-access-requests'] as const,
  admin: ['admin-access-requests'] as const,
  pendingCount: ['admin-access-requests', 'pending-count'] as const,
}
