import { api, withQuery } from './client'
import type { PortalStatistics } from './types'

/** The browser's time zone, so "a day" in the charts is the viewer's day. */
function viewerTimeZone(): string | undefined {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone
  } catch {
    return undefined
  }
}

/** The administrators' overview (statistics page). */
export const statisticsApi = {
  get: (days: number, signal?: AbortSignal) =>
    api.get<PortalStatistics>(withQuery('/api/admin/statistics', { days, timeZone: viewerTimeZone() }), signal),
}

export const statisticsKeys = {
  period: (days: number) => ['admin-statistics', days] as const,
}
