import { useMutation } from '@tanstack/react-query'
import { Download, Loader2, LogOut, Trash2, UserCheck, UserMinus, UserPlus, X } from 'lucide-react'
import { useState } from 'react'
import { userAdminApi } from '@/api/users'
import type { UserBulkAction, UserBulkResult } from '@/api/types'
import { GroupPicker, type PickedPrincipal } from '@/components/access/PrincipalPicker'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { notify } from '@/hooks/useToast'
import { useI18n } from '@/i18n/useI18n'

type Confirming = { readonly action: UserBulkAction; readonly group?: PickedPrincipal }

/**
 * Shown above the user table while rows are selected: what to do with all of them. Every account is handled as if
 * alone (the same checks), and the toast says how many succeeded.
 */
export function UserBulkBar({
  selected,
  canManageGroups,
  onClear,
  onDone,
}: {
  selected: readonly string[]
  canManageGroups: boolean
  onClear: () => void
  onDone: (results: UserBulkResult[]) => Promise<void>
}) {
  const { t } = useI18n()
  const [confirming, setConfirming] = useState<Confirming | null>(null)
  const [groupFor, setGroupFor] = useState<'add_to_group' | 'remove_from_group' | null>(null)
  const [group, setGroup] = useState<PickedPrincipal | null>(null)

  const run = useMutation({
    mutationFn: ({ action, group }: Confirming) => userAdminApi.bulk(action, selected, group?.id),
    onSuccess: async (results) => {
      setConfirming(null)
      setGroupFor(null)
      setGroup(null)

      const failed = results.filter((result) => !result.succeeded).length
      const done = results.length - failed
      if (failed === 0) {
        notify.success(t('users.bulk.done', { count: done }))
      } else {
        notify.warning(t('users.bulk.partial', { done, failed }))
      }

      await onDone(results)
    },
  })

  return (
    <div className="bg-muted/50 mb-4 grid gap-3 rounded-lg border p-3">
      <div className="flex flex-wrap items-center gap-2">
        <span className="mr-2 text-sm font-medium">{t('users.bulk.selected', { count: selected.length })}</span>
        <Button size="sm" variant="outline" onClick={() => setConfirming({ action: 'approve' })}>
          <UserCheck />
          {t('users.approve')}
        </Button>
        {canManageGroups && (
          <>
            <Button size="sm" variant="outline" onClick={() => setGroupFor('add_to_group')}>
              <UserPlus />
              {t('users.bulk.addToGroup')}
            </Button>
            <Button size="sm" variant="outline" onClick={() => setGroupFor('remove_from_group')}>
              <UserMinus />
              {t('users.bulk.removeFromGroup')}
            </Button>
          </>
        )}
        <Button size="sm" variant="outline" onClick={() => setConfirming({ action: 'sign_out' })}>
          <LogOut />
          {t('users.bulk.signOut')}
        </Button>
        <Button size="sm" variant="outline" asChild>
          <a href={userAdminApi.exportUrl({ ids: selected })} download>
            <Download />
            {t('users.export')}
          </a>
        </Button>
        <Button size="sm" variant="destructive" onClick={() => setConfirming({ action: 'delete' })}>
          <Trash2 />
          {t('common.delete')}
        </Button>
        <Button size="sm" variant="ghost" className="ml-auto" onClick={onClear}>
          <X />
          {t('users.bulk.clear')}
        </Button>
      </div>

      {groupFor && (
        <div className="flex flex-wrap items-end gap-2">
          <div className="grid min-w-64 gap-1.5">
            <Label htmlFor="bulk-group">
              {groupFor === 'add_to_group' ? t('users.bulk.addToGroup') : t('users.bulk.removeFromGroup')}
            </Label>
            <GroupPicker id="bulk-group" value={group} onChange={setGroup} />
          </div>
          <Button size="sm" disabled={!group} onClick={() => group && setConfirming({ action: groupFor, group })}>
            {t('common.apply')}
          </Button>
          <Button
            size="sm"
            variant="ghost"
            onClick={() => {
              setGroupFor(null)
              setGroup(null)
            }}
          >
            {t('common.cancel')}
          </Button>
        </div>
      )}

      <AlertDialog open={confirming !== null} onOpenChange={(open) => !open && setConfirming(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {confirming && t(`users.bulk.confirm.${confirming.action}`, { count: selected.length, group: confirming.group?.label ?? '' })}
            </AlertDialogTitle>
            <AlertDialogDescription>{t('users.bulk.confirmDescription')}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t('common.cancel')}</AlertDialogCancel>
            <AlertDialogAction
              variant={confirming?.action === 'delete' ? 'destructive' : 'default'}
              disabled={run.isPending}
              onClick={(event) => {
                event.preventDefault()
                if (confirming) {
                  run.mutate(confirming)
                }
              }}
            >
              {run.isPending && <Loader2 className="animate-spin" />}
              {t('common.confirm')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}
