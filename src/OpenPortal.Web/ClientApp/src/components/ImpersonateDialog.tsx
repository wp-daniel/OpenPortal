import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Headset, Loader2 } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { authApi } from '@/api/auth'
import type { UserSummary } from '@/api/types'
import { FormField } from '@/components/FormField'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Textarea } from '@/components/ui/textarea'
import { notify } from '@/hooks/useToast'
import { useI18n } from '@/i18n/useI18n'
import { reportFormError } from '@/lib/forms'

const REASON_MAX = 200

/** Whether an administrator may start a support session as this account (the server checks it again). */
export function canImpersonate(user: UserSummary, selfId: string | undefined): boolean {
  return (
    user.id !== selfId && !user.isAdministrator && !user.pendingApproval && !user.isLockedOut && !user.deletionScheduledAtUtc
  )
}

/** Asks why, then turns the administrator's session into a support session as `user`. */
export function ImpersonateDialog({ user, onClose }: { user: UserSummary | null; onClose: () => void }) {
  return (
    <Dialog open={user !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>{user && <ImpersonateForm user={user} onClose={onClose} />}</DialogContent>
    </Dialog>
  )
}

function ImpersonateForm({ user, onClose }: { user: UserSummary; onClose: () => void }) {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const [reason, setReason] = useState('')
  const [error, setError] = useState<string>()

  const start = useMutation({
    mutationFn: () => authApi.impersonate(user.id, reason.trim()),
    meta: { handlesErrors: true },
    onSuccess: () => {
      // Nothing cached as the administrator may show up inside the support session.
      queryClient.clear()
      notify.info(t('impersonation.started', { user: user.displayName }))
      navigate('/', { replace: true })
    },
    onError: (failure) => reportFormError(failure, t('impersonation.failed')),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    const value = reason.trim()

    if (value.length === 0 || value.length > REASON_MAX) {
      setError(t('impersonation.reasonRequired'))
      return
    }

    setError(undefined)
    start.mutate()
  }

  return (
    <form onSubmit={submit} noValidate className="grid gap-4">
      <DialogHeader>
        <DialogTitle className="flex items-center gap-2">
          <Headset className="size-5" />
          {t('impersonation.title', { user: user.displayName })}
        </DialogTitle>
        <DialogDescription>{t('impersonation.description')}</DialogDescription>
      </DialogHeader>

      <Alert>
        <AlertDescription>{t('impersonation.notice')}</AlertDescription>
      </Alert>

      <FormField label={t('impersonation.reason')} htmlFor="impersonation-reason" hint={t('impersonation.reasonHint')} error={error}>
        <Textarea rows={3} maxLength={REASON_MAX} value={reason} onChange={(event) => setReason(event.target.value)} />
      </FormField>

      <DialogFooter>
        <Button variant="outline" onClick={onClose}>
          {t('common.cancel')}
        </Button>
        <Button type="submit" disabled={start.isPending}>
          {start.isPending && <Loader2 className="animate-spin" />}
          {t('impersonation.start')}
        </Button>
      </DialogFooter>
    </form>
  )
}
