import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Trash2 } from 'lucide-react'
import { userAdminApi } from '@/api/users'
import type { UserSummary } from '@/api/types'
import { EmptyState, ErrorPanel, LoadingState } from '@/components/StatePanels'
import { TokenStatus } from '@/components/TokensSection'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { notify } from '@/hooks/useToast'
import { formatDate, formatRelative } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { describeError } from '@/lib/errors'

/** An account's personal access tokens, for the users page: when each was last used, and revoking one. */
export function UserTokensDialog({ user, onClose }: { user: UserSummary | null; onClose: () => void }) {
  return (
    <Dialog open={user !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-2xl">{user && <UserTokens user={user} />}</DialogContent>
    </Dialog>
  )
}

function UserTokens({ user }: { user: UserSummary }) {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const key = ['admin-user-tokens', user.id] as const

  const tokens = useQuery({ queryKey: key, queryFn: ({ signal }) => userAdminApi.tokens(user.id, signal) })

  const revoke = useMutation({
    mutationFn: (tokenId: string) => userAdminApi.revokeToken(user.id, tokenId),
    onSuccess: async () => {
      notify.success(t('tokens.revoked'))
      await queryClient.invalidateQueries({ queryKey: key })
    },
  })

  return (
    <>
      <DialogHeader>
        <DialogTitle>{t('tokens.userTitle', { name: user.displayName })}</DialogTitle>
        <DialogDescription>{t('tokens.userDescription')}</DialogDescription>
      </DialogHeader>

      {tokens.isPending && <LoadingState />}
      {tokens.isError && <ErrorPanel message={describeError(tokens.error)} onRetry={() => void tokens.refetch()} />}
      {tokens.data?.length === 0 && <EmptyState title={t('tokens.empty.title')} description={t('tokens.userEmpty')} />}

      {tokens.data && tokens.data.length > 0 && (
        <div className="overflow-x-auto">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('tokens.column.name')}</TableHead>
                <TableHead>{t('tokens.column.scope')}</TableHead>
                <TableHead>{t('tokens.column.expires')}</TableHead>
                <TableHead>{t('tokens.column.lastUsed')}</TableHead>
                <TableHead className="text-right">{t('common.actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {tokens.data.map((token) => (
                <TableRow key={token.id}>
                  <TableCell>
                    <div className="grid">
                      <span className="font-medium">{token.name}</span>
                      <code className="text-muted-foreground text-xs">{token.prefix}…</code>
                    </div>
                  </TableCell>
                  <TableCell>{token.scopes.includes('write') ? t('tokens.scope.write') : t('tokens.scope.read')}</TableCell>
                  <TableCell>
                    <div className="flex flex-wrap items-center gap-2">
                      {formatDate(token.expiresAtUtc, { dateStyle: 'medium' })}
                      <TokenStatus token={token} />
                    </div>
                  </TableCell>
                  <TableCell>{token.lastUsedAtUtc ? formatRelative(token.lastUsedAtUtc) : '—'}</TableCell>
                  <TableCell className="text-right">
                    {token.status === 'active' && (
                      <Button
                        variant="ghost"
                        size="sm"
                        aria-label={t('tokens.revoke')}
                        disabled={revoke.isPending}
                        onClick={() => revoke.mutate(token.id)}
                      >
                        <Trash2 />
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </>
  )
}
