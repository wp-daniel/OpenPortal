import { useQuery } from '@tanstack/react-query'
import { authApi } from '../api/auth'
import { UNAVAILABLE_PASSWORD_POLICY } from '../lib/passwordPolicy'

/**
 * The signed-in session, as the whole app sees it.
 *
 * A single query key means every screen shares one cache entry, so signing in or out invalidates it
 * everywhere rather than leaving a stale "anonymous" banner on another page. `staleTime: 0` because the
 * session cookie can be rotated by another tab at any moment and must never be served from cache.
 */
export function useSession() {
  const { data, isPending } = useQuery({
    queryKey: ['session'],
    queryFn: ({ signal }) => authApi.session(signal),
    staleTime: 0,
  })

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
    user: data?.user ?? null,
    isAdministrator: data?.user?.roles.includes('Administrator') ?? false,
  }
}