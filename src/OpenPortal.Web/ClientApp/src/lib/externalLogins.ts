import { useQuery } from '@tanstack/react-query'
import { accountApi } from '@/api/auth'

export const EXTERNAL_LOGINS_KEY = ['account-external-logins'] as const

/** The account's connected external accounts, and whether it has a password at all. */
export function useExternalLogins() {
  return useQuery({ queryKey: EXTERNAL_LOGINS_KEY, queryFn: ({ signal }) => accountApi.externalLogins(signal) })
}
