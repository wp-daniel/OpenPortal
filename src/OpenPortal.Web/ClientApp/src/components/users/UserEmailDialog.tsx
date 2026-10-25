import { useMutation } from '@tanstack/react-query'
import { Loader2, Mail } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { z } from 'zod'
import type { UserSummary } from '@/api/types'
import { userAdminApi } from '@/api/users'
import { FormField } from '@/components/FormField'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { notify } from '@/hooks/useToast'
import { useI18n } from '@/i18n/useI18n'
import { reportFormError, serverFieldErrors } from '@/lib/forms'

/**
 * Gives an account another sign-in address, for an owner who cannot confirm one themselves (the old mailbox is gone).
 * The administrator vouches for the address; both addresses are told and the account's sessions end.
 */
export function UserEmailDialog({
  user,
  onClose,
  onChanged,
}: {
  user: UserSummary | null
  onClose: () => void
  onChanged: () => void
}) {
  return (
    <Dialog open={user !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>{user && <UserEmailForm user={user} onClose={onClose} onChanged={onChanged} />}</DialogContent>
    </Dialog>
  )
}

function UserEmailForm({ user, onClose, onChanged }: { user: UserSummary; onClose: () => void; onChanged: () => void }) {
  const { t } = useI18n()
  const [email, setEmail] = useState('')
  const [error, setError] = useState<string>()
  const schema = useMemo(
    () => z.string().trim().min(1, t('validation.emailRequiredNew')).email(t('validation.emailInvalid')),
    [t],
  )

  const change = useMutation({
    mutationFn: () => userAdminApi.changeEmail(user.id, email.trim()),
    meta: { handlesErrors: true },
    onSuccess: (updated) => {
      notify.success(t('users.emailChange.done'), updated.email)
      onChanged()
      onClose()
    },
    onError: (failure) => reportFormError(failure, t('users.emailChange.failed')),
  })

  function submit(event: FormEvent) {
    event.preventDefault()

    const parsed = schema.safeParse(email)
    if (!parsed.success) {
      setError(parsed.error.issues[0]?.message)
      return
    }

    setError(undefined)
    change.mutate()
  }

  return (
    <form onSubmit={submit} noValidate className="grid gap-4">
      <DialogHeader>
        <DialogTitle className="flex items-center gap-2">
          <Mail className="size-5" />
          {t('users.emailChange.title', { name: user.displayName })}
        </DialogTitle>
        <DialogDescription>{t('users.emailChange.description', { email: user.email })}</DialogDescription>
      </DialogHeader>

      <Alert>
        <AlertDescription>{t('users.emailChange.notice')}</AlertDescription>
      </Alert>

      <FormField label={t('emailChange.newEmail')} htmlFor="user-new-email" error={error ?? serverFieldErrors(change.error).email?.[0]}>
        <Input type="email" autoComplete="off" value={email} onChange={(event) => setEmail(event.target.value)} />
      </FormField>

      <DialogFooter>
        <Button variant="outline" onClick={onClose}>
          {t('common.cancel')}
        </Button>
        <Button type="submit" disabled={change.isPending}>
          {change.isPending && <Loader2 className="animate-spin" />}
          {t('users.emailChange.confirm')}
        </Button>
      </DialogFooter>
    </form>
  )
}
