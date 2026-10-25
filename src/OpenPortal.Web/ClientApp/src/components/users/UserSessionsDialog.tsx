import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { LogOut } from 'lucide-react'
import { userAdminApi } from '@/api/users'
import type { SessionInfo, UserSummary } from '@/api/types'
import { SessionRow } from '@/components/SessionsSection'
import { EmptyState, ErrorPanel, LoadingState } from '@/components/StatePanels'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { notify } from '@/hooks/useToast'
import { useI18n } from '@/i18n/useI18n'
import { describeError } from '@/lib/errors'

/** Where an account is signed in, for the users page, with a way to end one session (a shared computer). */
export function UserSessionsDialog({ user, onClose }: { user: UserSummary | null; onClose: () => void }) {
  return (
    <Dialog open={user !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-2xl">{user && <UserSessions user={user} />}</DialogContent>
    </Dialog>
  )
}

function UserSessions({ user }: { user: UserSummary }) {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const key = ['admin-user-sessions', user.id] as const

  const sessions = useQuery({ queryKey: key, queryFn: ({ signal }) => userAdminApi.sessions(user.id, signal) })

  const revoke = useMutation({
    mutationFn: (session: SessionInfo) => userAdminApi.revokeSession(user.id, session.id),
    onSuccess: async () => {
      notify.success(t('sessions.revoked'))
      await queryClient.invalidateQueries({ queryKey: key })
    },
  })

  return (
    <>
      <DialogHeader>
        <DialogTitle>{t('sessions.userTitle', { name: user.displayName })}</DialogTitle>
        <DialogDescription>{t('sessions.userDescription')}</DialogDescription>
      </DialogHeader>

      {sessions.isPending && <LoadingState />}
      {sessions.isError && <ErrorPanel message={describeError(sessions.error)} onRetry={() => void sessions.refetch()} />}

      {sessions.data && sessions.data.active.length === 0 && (
        <EmptyState title={t('sessions.noneTitle')} description={t('sessions.noneDescription')} />
      )}

      {sessions.data && sessions.data.active.length > 0 && (
        <ul className="max-h-80 divide-y overflow-y-auto rounded-lg border">
          {sessions.data.active.map((session) => (
            <SessionRow
              key={session.id}
              session={session}
              action={
                <Button size="sm" variant="ghost" disabled={revoke.isPending} onClick={() => revoke.mutate(session)}>
                  <LogOut />
                  {t('sessions.revoke')}
                </Button>
              }
            />
          ))}
        </ul>
      )}

      {sessions.data && sessions.data.recent.length > 0 && (
        <div className="grid gap-2">
          <p className="text-sm font-medium">{t('sessions.history')}</p>
          <ul className="max-h-60 divide-y overflow-y-auto rounded-lg border opacity-80">
            {sessions.data.recent.map((session) => (
              <SessionRow key={session.id} session={session} />
            ))}
          </ul>
        </div>
      )}
    </>
  )
}
