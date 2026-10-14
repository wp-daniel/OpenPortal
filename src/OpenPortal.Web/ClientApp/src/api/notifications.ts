import { api, withQuery } from './client'
import type { NotificationCount, NotificationPage, PortalNotification } from './types'

/** The signed-in user's notification center. Every call is about the caller's own notifications. */
export const notificationsApi = {
  list: (params: { page: number; pageSize: number; unreadOnly?: boolean }, signal?: AbortSignal) =>
    api.get<NotificationPage>(
      withQuery('/api/notifications', {
        page: params.page,
        pageSize: params.pageSize,
        unreadOnly: params.unreadOnly ? 'true' : undefined,
      }),
      signal,
    ),

  /** Cheap enough to poll: what the bell shows. */
  count: (signal?: AbortSignal) => api.get<NotificationCount>('/api/notifications/count', signal),

  markRead: (id: string) => api.put<PortalNotification>(`/api/notifications/${id}/read`, {}),

  markUnread: (id: string) => api.delete<PortalNotification>(`/api/notifications/${id}/read`),

  markAllRead: () => api.post<void>('/api/notifications/read-all'),

  remove: (id: string) => api.delete<void>(`/api/notifications/${id}`),

  /** Deletes every notification already read. */
  removeRead: () => api.delete<void>('/api/notifications/read'),
}

export const notificationKeys = {
  all: ['notifications'] as const,
  count: ['notifications', 'count'] as const,
  list: (filter: { page: number; unreadOnly: boolean }) => ['notifications', 'list', filter] as const,
  latest: ['notifications', 'latest'] as const,
}
