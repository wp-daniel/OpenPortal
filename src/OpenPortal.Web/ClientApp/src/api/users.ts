import { api, withQuery } from './client'
import type { CreateUserRequest, PagedResult, UpdateUserRequest, UserSummary } from './types'

export const userAdminApi = {
  list: (
    params: { page: number; pageSize: number; search?: string },
    signal?: AbortSignal,
  ): Promise<PagedResult<UserSummary>> =>
    api.get<PagedResult<UserSummary>>(
      withQuery('/api/admin/users', {
        page: params.page,
        pageSize: params.pageSize,
        search: params.search,
      }),
      signal,
    ),

  create: (body: CreateUserRequest) => api.post<UserSummary>('/api/admin/users', body),

  update: (userId: string, body: UpdateUserRequest) =>
    api.put<UserSummary>(`/api/admin/users/${userId}`, body),

  resetPassword: (userId: string, newPassword: string) =>
    api.post<void>(`/api/admin/users/${userId}/reset-password`, { newPassword }),
}