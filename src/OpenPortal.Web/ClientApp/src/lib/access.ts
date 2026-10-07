import { useQueryClient } from '@tanstack/react-query'
import { useCallback } from 'react'
import { accessKeys } from '@/api/access'
import type { PortalPage } from '@/api/types'
import { SESSION_QUERY_KEY } from '@/hooks/useSession'

/**
 * Every access screen shows the same data from a different angle, so a change made on one must refresh all
 * of them (and the signed-in user's own launchpad and pages).
 */
export function useAccessRefresh() {
  const queryClient = useQueryClient()

  return useCallback(
    () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: accessKeys.applications }),
        queryClient.invalidateQueries({ queryKey: accessKeys.groups }),
        queryClient.invalidateQueries({ queryKey: accessKeys.tree }),
        queryClient.invalidateQueries({ queryKey: ['admin-access-user'] }),
        queryClient.invalidateQueries({ queryKey: accessKeys.launchpad }),
        queryClient.invalidateQueries({ queryKey: accessKeys.pagePermissions }),
        // The signed-in user's own pages may have changed with their groups.
        queryClient.invalidateQueries({ queryKey: SESSION_QUERY_KEY }),
      ]),
    [queryClient],
  )
}

/** One URI per line in a textarea, as the list the API expects. */
export function linesToList(value: string): string[] {
  return value
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line !== '')
}

/** Pages in catalog order, grouped by their area, keeping the order in which areas first appear. */
export function groupPagesByArea(pages: readonly PortalPage[]): [areaKey: string, pages: PortalPage[]][] {
  const areas = new Map<string, PortalPage[]>()

  for (const page of pages) {
    areas.set(page.areaKey, [...(areas.get(page.areaKey) ?? []), page])
  }

  return [...areas.entries()]
}
