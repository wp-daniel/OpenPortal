import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2, Unlink } from 'lucide-react'
import { userAdminApi } from '@/api/users'
import type { ExternalLogin, UserSummary } from '@/api/types'
import { ExternalProviderIcon } from '@/components/ExternalProviders'
import { EmptyState, ErrorPanel, LoadingState } from '@/components/StatePanels'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { notify } from '@/hooks/useToast'
import { useI18n } from '@/i18n/useI18n'
import { describeError } from '@/lib/errors'

/** The Google and Microsoft accounts connected to an account, for the users page, with a way to disconnect one. */
export function UserExternalLoginsDialog({ user, onClose }: { user: UserSummary | null; onClose: () => void }) {
  return (
    <Dialog open={user !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-lg">{user && <UserExternalLogins user={user} />}</DialogContent>
    </Dialog>
  )
}

function UserExternalLogins({ user }: { user: UserSummary }) {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const key = ['admin-user-external-logins', user.id] as const

  const logins = useQuery({ queryKey: key, queryFn: ({ signal }) => userAdminApi.externalLogins(user.id, signal) })

  const remove = useMutation({
    mutationFn: (login: ExternalLogin) => userAdminApi.removeExternalLogin(user.id, login.provider),
    onSuccess: async () => {
      notify.success(t('externalLogins.removed'))
      await queryClient.invalidateQueries({ queryKey: key })
    },
  })

  return (
    <>
      <DialogHeader>
        <DialogTitle>{t('externalLogins.userTitle', { name: user.displayName })}</DialogTitle>
        <DialogDescription>{t('externalLogins.userDescription')}</DialogDescription>
      </DialogHeader>

      {logins.isPending && <LoadingState />}
      {logins.isError && <ErrorPanel message={describeError(logins.error)} onRetry={() => void logins.refetch()} />}
      {logins.data?.length === 0 && <EmptyState title={t('externalLogins.none')} description={t('externalLogins.userNone')} />}

      {logins.data && logins.data.length > 0 && (
        <ul className="divide-y rounded-lg border">
          {logins.data.map((login) => (
            <li key={login.provider} className="flex items-center gap-3 p-3">
              <ExternalProviderIcon provider={login.provider} className="size-5 shrink-0" />
              <div className="grid min-w-0 flex-1 gap-0.5">
                <span className="truncate text-sm font-medium">{login.displayName}</span>
                {login.accountName && <span className="text-muted-foreground truncate text-xs">{login.accountName}</span>}
              </div>
              <Button size="sm" variant="ghost" disabled={remove.isPending} onClick={() => remove.mutate(login)}>
                {remove.isPending && remove.variables?.provider === login.provider ? <Loader2 className="animate-spin" /> : <Unlink />}
                {t('externalLogins.disconnect')}
              </Button>
            </li>
          ))}
        </ul>
      )}
    </>
  )
}
