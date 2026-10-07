import { useQuery } from '@tanstack/react-query'
import { authApi } from '@/api/auth'
import { UNAVAILABLE_PASSWORD_POLICY } from '@/lib/passwordPolicy'

export const SESSION_QUERY_KEY = ['session'] as const

const NO_PAGES: readonly string[] = []

/**
 * The signed-in session, as the whole app sees it.
 *
 * A single query key means every screen shares one cache entry, so signing in or out invalidates it
 * everywhere rather than leaving stale state on another page. `staleTime: 0` because the session cookie can
 * be rotated by another tab at any moment and must never be served from cache.
 *
 * `isPending` is true until the first answer arrives. Route guards wait on it: treating "not loaded yet" as
 * "signed out" would bounce a signed-in user to the sign-in page on every hard refresh.
 */
export function useSession() {
  const { data, isPending, isError, refetch } = useQuery({
    queryKey: SESSION_QUERY_KEY,
    queryFn: ({ signal }) => authApi.session(signal),
    staleTime: 0,
  })

  const isAdministrator = data?.user?.roles.includes('Administrator') ?? false
  const pages = data?.user?.pages ?? NO_PAGES

  return {
    // Placeholder rather than undefined so consumers never branch on the session being absent. The policy is
    // the marked "unknown" value, which makes password forms skip client-side complexity checks until the
    // server has actually said what it enforces.
    session: data ?? {
      isAuthenticated: false,
      user: null,
      passwordPolicy: UNAVAILABLE_PASSWORD_POLICY,
    },
    isPending,
    isError,
    refetch,
    user: data?.user ?? null,
    isAdministrator,
    /** Keys of the portal pages the user may open (`PortalPages` on the server). */
    pages,
    /** Whether the user may open the page `key`; administrators may open every page. */
    canOpen: (key: string) => isAdministrator || pages.includes(key),
  }
}
