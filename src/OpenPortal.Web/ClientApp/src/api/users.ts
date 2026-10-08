import { api, withQuery } from './client'
import type { CreateUserRequest, PagedResult, UpdateUserRequest, UserSummary } from './types'

export const userAdminApi = {
  list: (
    params: { page: number; pageSize: number; search?: string; administrator?: boolean; status?: string },
    signal?: AbortSignal,
  ): Promise<PagedResult<UserSummary>> =>
    api.get<PagedResult<UserSummary>>(
      withQuery('/api/admin/users', {
        page: params.page,
        pageSize: params.pageSize,
        search: params.search,
        administrator: params.administrator === undefined ? undefined : String(params.administrator),
        status: params.status,
      }),
      signal,
    ),

  create: (body: CreateUserRequest) => api.post<UserSummary>('/api/admin/users', body),

  update: (userId: string, body: UpdateUserRequest) =>
    api.put<UserSummary>(`/api/admin/users/${userId}`, body),

  resetPassword: (userId: string, newPassword: string) =>
    api.post<void>(`/api/admin/users/${userId}/reset-password`, { newPassword }),

  /** Turns two-factor off for an account that lost its authenticator; its sessions end. */
  resetTwoFactor: (userId: string) => api.delete<void>(`/api/admin/users/${userId}/two-factor`),

  uploadAvatar: (userId: string, image: Blob) => api.put<void>(`/api/admin/users/${userId}/avatar`, avatarForm(image)),

  removeAvatar: (userId: string) => api.delete<void>(`/api/admin/users/${userId}/avatar`),

  /** Deletes the account, its group memberships and its application grants. */
  remove: (userId: string) => api.delete<void>(`/api/admin/users/${userId}`),
}

/** The multipart body every avatar upload sends: one `file` part, judged by its bytes on the server. */
export function avatarForm(image: Blob): FormData {
  const form = new FormData()
  form.append('file', image, 'avatar')

  return form
}

/**
 * Where a user's picture is served, or null when they have none. The change stamp is part of the URL, so
 * the browser may cache it and still sees a new picture the moment it is replaced.
 */
export function avatarUrl(user: { readonly id: string; readonly avatarUpdatedAtUtc: string | null }): string | null {
  return user.avatarUpdatedAtUtc
    ? `/api/users/${user.id}/avatar?v=${encodeURIComponent(user.avatarUpdatedAtUtc)}`
    : null
}