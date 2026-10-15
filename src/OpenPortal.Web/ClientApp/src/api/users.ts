import { api, withQuery } from './client'
import type {
  CreateUserRequest,
  PagedResult,
  PersonalAccessToken,
  UpdateUserRequest,
  UserBulkAction,
  UserBulkResult,
  UserImportResult,
  UserSummary,
} from './types'

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

  /** Lets a self-registered account that waits for approval sign in (to reject it, delete it). */
  approve: (userId: string) => api.post<UserSummary>(`/api/admin/users/${userId}/approve`),

  /** Deletes the account, its group memberships and its application grants. */
  remove: (userId: string) => api.delete<void>(`/api/admin/users/${userId}`),

  /** Where the CSV of the filter (or of the selected ids) downloads from: a plain link, the cookie goes along. */
  exportUrl: (params: { search?: string; administrator?: boolean; status?: string; ids?: readonly string[] }) => {
    const path = withQuery('/api/admin/users/export', {
      search: params.search,
      administrator: params.administrator === undefined ? undefined : String(params.administrator),
      status: params.status,
    })
    const ids = (params.ids ?? []).map((id) => `ids=${encodeURIComponent(id)}`).join('&')

    return ids ? `${path}${path.includes('?') ? '&' : '?'}${ids}` : path
  },

  /** Imports a CSV: new addresses are invited, existing accounts updated or skipped. A dry run writes nothing. */
  importCsv: (file: File, options: { dryRun: boolean; updateExisting: boolean }) => {
    const form = new FormData()
    form.append('file', file, file.name)

    return api.post<UserImportResult>(
      withQuery('/api/admin/users/import', { dryRun: String(options.dryRun), updateExisting: String(options.updateExisting) }),
      form,
    )
  },

  /** One action on several accounts; the answer says what happened to each. */
  bulk: (action: UserBulkAction, userIds: readonly string[], groupId?: string) =>
    api.post<UserBulkResult[]>('/api/admin/users/bulk', { action, userIds, groupId }),

  tokens: (userId: string, signal?: AbortSignal) =>
    api.get<PersonalAccessToken[]>(`/api/admin/users/${userId}/tokens`, signal),

  revokeToken: (userId: string, tokenId: string) => api.delete<void>(`/api/admin/users/${userId}/tokens/${tokenId}`),
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