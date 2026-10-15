import { api } from './client'
import type { ActiveAnnouncement, Announcement, SaveAnnouncement } from './types'

/** The banners shown across the portal, and their management. */
export const announcementsApi = {
  /** What shows now for this reader, in their language (the request's Accept-Language). Open to everyone. */
  active: (signal?: AbortSignal) => api.get<ActiveAnnouncement[]>('/api/announcements', signal),

  list: (signal?: AbortSignal) => api.get<Announcement[]>('/api/admin/announcements', signal),

  create: (body: SaveAnnouncement) => api.post<Announcement>('/api/admin/announcements', body),

  update: (id: string, body: SaveAnnouncement) => api.put<Announcement>(`/api/admin/announcements/${id}`, body),

  remove: (id: string) => api.delete<void>(`/api/admin/announcements/${id}`),
}

export const announcementKeys = {
  all: ['announcements'] as const,
  active: (language: string, signedIn: boolean) => ['announcements', 'active', language, signedIn] as const,
  admin: ['announcements', 'admin'] as const,
}
