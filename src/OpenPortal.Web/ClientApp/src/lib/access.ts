import { useQueryClient } from '@tanstack/react-query'
import { useCallback } from 'react'
import { accessKeys } from '@/api/access'

/**
 * Every access screen shows the same data from a different angle, so a change made on one must refresh all
 * of them (and the signed-in user's own launchpad).
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
