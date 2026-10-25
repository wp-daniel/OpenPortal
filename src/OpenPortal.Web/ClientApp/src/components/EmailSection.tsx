import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2, Mail, MailCheck } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { z } from 'zod'
import { accountApi } from '@/api/auth'
import { FormField } from '@/components/FormField'
import { Section } from '@/components/Section'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { useSession } from '@/hooks/useSession'
import { notify } from '@/hooks/useToast'
import { useI18n } from '@/i18n/useI18n'
import { useExternalLogins } from '@/lib/externalLogins'
import { reportFormError, serverFieldErrors, zodFieldErrors } from '@/lib/forms'

const PROFILE_KEY = ['account-profile']

/**
 * The address the account signs in with. Changing it sends a link to the new address and nothing changes until it is
 * opened, so a typo cannot lock the account out; the old address is told once it is done.
 */
export function EmailSection() {
  const { t } = useI18n()
  const { user, session } = useSession()
  const queryClient = useQueryClient()
  const [changing, setChanging] = useState(false)

  const profile = useQuery({ queryKey: PROFILE_KEY, queryFn: ({ signal }) => accountApi.profile(signal) })
  const pending = profile.data?.pendingEmail ?? null

  const cancel = useMutation({
    mutationFn: accountApi.cancelEmailChange,
    onSuccess: (updated) => {
      queryClient.setQueryData(PROFILE_KEY, updated)
      notify.info(t('emailChange.cancelled'))
    },
  })

  // Changing needs email delivery (the link); the session says whether the portal can send email.
  const available = session.registration.passwordReset

  return (
    <Section title={t('emailChange.title')} description={t('emailChange.description')}>
      <div className="grid gap-4">
        <div className="flex flex-wrap items-center gap-3">
          <Mail className="text-muted-foreground size-4" />
          <span className="font-mono text-sm break-all">{user?.email}</span>
          {user?.emailConfirmed ? (
            <Badge variant="success">{t('account.confirmed')}</Badge>
          ) : (
            <Badge variant="warning">{t('account.notConfirmed')}</Badge>
          )}
        </div>

        {pending && (
          <Alert>
            <MailCheck />
            <AlertDescription className="flex flex-wrap items-center justify-between gap-3">
              <span>{t('emailChange.pending', { email: pending })}</span>
              <Button variant="outline" size="sm" disabled={cancel.isPending} onClick={() => cancel.mutate()}>
                {t('emailChange.cancel')}
              </Button>
            </AlertDescription>
          </Alert>
        )}

        {available ? (
          <div>
            <Button variant="outline" onClick={() => setChanging(true)}>
              {t('emailChange.open')}
            </Button>
          </div>
        ) : (
          <p className="text-muted-foreground text-sm">{t('emailChange.unavailable')}</p>
        )}
      </div>

      <Dialog open={changing} onOpenChange={setChanging}>
        <DialogContent>{changing && <ChangeEmailForm onClose={() => setChanging(false)} />}</DialogContent>
      </Dialog>
    </Section>
  )
}

function ChangeEmailForm({ onClose }: { onClose: () => void }) {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const logins = useExternalLogins()
  // An account created through Google or Microsoft has no password: a recent sign-in proves the owner instead.
  const hasPassword = logins.data?.hasPassword !== false
  const [values, setValues] = useState({ newEmail: '', currentPassword: '' })
  const [errors, setErrors] = useState<Partial<Record<keyof typeof values, string>>>({})

  const schema = useMemo(
    () =>
      z.object({
        newEmail: z.string().trim().min(1, t('validation.emailRequiredNew')).email(t('validation.emailInvalid')),
        currentPassword: hasPassword ? z.string().min(1, t('validation.currentPasswordRequired')) : z.string(),
      }),
    [hasPassword, t],
  )

  const request = useMutation({
    mutationFn: () =>
      accountApi.requestEmailChange({
        newEmail: values.newEmail.trim(),
        currentPassword: hasPassword ? values.currentPassword : undefined,
      }),
    meta: { handlesErrors: true },
    onSuccess: (updated) => {
      queryClient.setQueryData(PROFILE_KEY, updated)
      notify.success(t('emailChange.sent', { email: values.newEmail.trim() }), t('emailChange.sentDetail'))
      onClose()
    },
    onError: (failure) => reportFormError(failure, t('emailChange.failed')),
  })

  function submit(event: FormEvent) {
    event.preventDefault()

    const parsed = schema.safeParse(values)
    if (!parsed.success) {
      setErrors(zodFieldErrors<keyof typeof values>(parsed.error))
      return
    }

    setErrors({})
    request.mutate()
  }

  const serverErrors = serverFieldErrors(request.error)

  return (
    <form onSubmit={submit} noValidate className="grid gap-4">
      <DialogHeader>
        <DialogTitle>{t('emailChange.dialogTitle')}</DialogTitle>
        <DialogDescription>{t('emailChange.dialogDescription')}</DialogDescription>
      </DialogHeader>

      <FormField label={t('emailChange.newEmail')} htmlFor="new-email" error={errors.newEmail ?? serverErrors.newEmail?.[0]}>
        <Input
          type="email"
          autoComplete="email"
          value={values.newEmail}
          onChange={(event) => setValues({ ...values, newEmail: event.target.value })}
        />
      </FormField>

      {hasPassword ? (
        <FormField label={t('account.currentPassword')} htmlFor="email-change-password" error={errors.currentPassword}>
          <Input
            type="password"
            autoComplete="current-password"
            value={values.currentPassword}
            onChange={(event) => setValues({ ...values, currentPassword: event.target.value })}
          />
        </FormField>
      ) : (
        <p className="text-muted-foreground text-sm">{t('emailChange.recentSignInHint')}</p>
      )}

      <DialogFooter>
        <Button variant="outline" onClick={onClose}>
          {t('common.cancel')}
        </Button>
        <Button type="submit" disabled={request.isPending}>
          {request.isPending && <Loader2 className="animate-spin" />}
          {t('emailChange.send')}
        </Button>
      </DialogFooter>
    </form>
  )
}
