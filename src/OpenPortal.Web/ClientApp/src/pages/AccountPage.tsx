import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { z } from 'zod'
import { accountApi } from '@/api/auth'
import { ApiError } from '@/api/client'
import type { PasswordPolicy } from '@/api/types'
import { FormField } from '@/components/FormField'
import { PageHeader } from '@/components/PageHeader'
import { Section } from '@/components/Section'
import { LoadingState } from '@/components/StatePanels'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { SESSION_QUERY_KEY, useSession } from '@/hooks/useSession'
import { notify } from '@/hooks/useToast'
import { formatDate, type TFunction } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { reportFormError, serverFieldErrors, zodFieldErrors } from '@/lib/forms'
import { describePasswordPolicy, newPasswordSchema } from '@/lib/passwordPolicy'

const makeProfileSchema = (t: TFunction) =>
  z.object({
    displayName: z
      .string()
      .trim()
      .min(2, t('validation.displayNameMin', { min: 2 }))
      .max(120, t('validation.displayNameMax', { max: 120 })),
  })

/**
 * The signed-in account's own settings.
 *
 * Email is shown read-only: it is the sign-in identifier, so changing it needs a confirmation flow that does
 * not exist here. Offering the field would let an account lock itself out.
 */
export function AccountPage() {
  const { user, session } = useSession()
  const { t } = useI18n()

  const profile = useQuery({
    queryKey: ['account-profile'],
    queryFn: ({ signal }) => accountApi.profile(signal),
  })

  return (
    <div className="space-y-6">
      <PageHeader title={t('account.title')} description={t('account.description')} />

      {profile.isPending && <LoadingState />}

      {profile.data && <ProfileForm initial={profile.data.displayName} />}

      <PasswordForm policy={session.passwordPolicy} />

      <Section title={t('account.details')}>
        <dl className="grid gap-4 text-sm sm:grid-cols-2">
          <div className="space-y-1">
            <dt className="text-muted-foreground">{t('common.email')}</dt>
            <dd className="font-mono text-sm break-all">{user?.email}</dd>
          </div>
          <div className="space-y-1">
            <dt className="text-muted-foreground">{t('account.emailConfirmed')}</dt>
            <dd>
              {user?.emailConfirmed ? (
                <Badge variant="success">{t('account.confirmed')}</Badge>
              ) : (
                <Badge variant="warning">{t('account.notConfirmed')}</Badge>
              )}
            </dd>
          </div>
          <div className="space-y-1">
            <dt className="text-muted-foreground">{t('common.roles')}</dt>
            <dd className="flex flex-wrap gap-1">
              {(user?.roles.length ?? 0) === 0 ? (
                <span className="text-muted-foreground">{t('common.none')}</span>
              ) : (
                user!.roles.map((role) => (
                  <Badge key={role} variant="secondary">
                    {t(`role.${role}`)}
                  </Badge>
                ))
              )}
            </dd>
          </div>
          {profile.data && (
            <div className="space-y-1">
              <dt className="text-muted-foreground">{t('account.memberSince')}</dt>
              <dd>{formatDate(profile.data.createdAtUtc)}</dd>
            </div>
          )}
        </dl>
      </Section>
    </div>
  )
}

function ProfileForm({ initial }: { initial: string }) {
  const queryClient = useQueryClient()
  const { t } = useI18n()
  const profileSchema = useMemo(() => makeProfileSchema(t), [t])
  const [displayName, setDisplayName] = useState(initial)
  const [error, setError] = useState<string | undefined>()

  const save = useMutation({
    mutationFn: () => accountApi.updateProfile({ displayName }),
    meta: { handlesErrors: true },
    onSuccess: async (updated) => {
      notify.success(t('account.displayNameSaved'))
      setError(undefined)
      setDisplayName(updated.displayName)
      // The session query carries the display name shown in the header, so it is stale after this write.
      await queryClient.invalidateQueries({ queryKey: SESSION_QUERY_KEY })
    },
    onError: (failure) => reportFormError(failure, t('account.saveFailed')),
  })

  function submit(event: FormEvent) {
    event.preventDefault()

    const parsed = profileSchema.safeParse({ displayName })

    if (!parsed.success) {
      setError(parsed.error.issues[0]?.message)

      return
    }

    setError(undefined)
    save.mutate()
  }

  return (
    <Section title={t('account.displayNameSection')}>
      <form onSubmit={submit} noValidate className="grid max-w-md gap-4">
        <FormField
          label={t('common.displayName')}
          htmlFor="displayName"
          error={error ?? serverFieldErrors(save.error).displayName?.[0]}
        >
          <Input
            name="displayName"
            required
            value={displayName}
            onChange={(event) => setDisplayName(event.target.value)}
          />
        </FormField>

        <div>
          <Button type="submit" disabled={save.isPending}>
            {save.isPending && <Loader2 className="animate-spin" />}
            {save.isPending ? t('common.saving') : t('common.save')}
          </Button>
        </div>
      </form>
    </Section>
  )
}

function PasswordForm({ policy }: { policy: PasswordPolicy }) {
  const { t } = useI18n()
  const [values, setValues] = useState({ currentPassword: '', newPassword: '' })
  const [errors, setErrors] = useState<Partial<Record<keyof typeof values, string>>>({})

  // Built from the policy the server published, so there is no second copy of the rules to drift out of
  // step with the user store.
  const schema = useMemo(
    () =>
      z.object({
        currentPassword: z.string().min(1, t('validation.currentPasswordRequired')),
        newPassword: newPasswordSchema(policy, t, t('validation.newPasswordRequired')),
      }),
    [policy, t],
  )

  const change = useMutation({
    mutationFn: () => accountApi.changePassword(values),
    meta: { handlesErrors: true },
    onSuccess: () => {
      notify.success(t('account.passwordChanged'))
      setErrors({})
      // Cleared rather than kept: leaving the new password in the form after a successful change invites it
      // to be submitted again by a second click.
      setValues({ currentPassword: '', newPassword: '' })
    },
    onError: (failure) => {
      // The server reports a rejected current password without saying which half failed, so neither can this.
      if (failure instanceof ApiError && failure.status === 400) {
        notify.error(t('account.passwordRejected'), t('account.passwordRejectedDetail'))
      } else {
        reportFormError(failure, t('account.passwordFailed'))
      }
    },
  })

  function submit(event: FormEvent) {
    event.preventDefault()

    const parsed = schema.safeParse(values)

    if (!parsed.success) {
      setErrors(zodFieldErrors<keyof typeof values>(parsed.error))

      return
    }

    setErrors({})
    change.mutate()
  }

  return (
    <Section title={t('account.passwordSection')}>
      <form onSubmit={submit} noValidate className="grid max-w-md gap-4">
        <FormField label={t('account.currentPassword')} htmlFor="currentPassword" error={errors.currentPassword}>
          <Input
            name="currentPassword"
            type="password"
            autoComplete="current-password"
            required
            value={values.currentPassword}
            onChange={(event) => setValues({ ...values, currentPassword: event.target.value })}
          />
        </FormField>

        <FormField
          label={t('account.newPassword')}
          htmlFor="newPassword"
          error={errors.newPassword}
          hint={describePasswordPolicy(policy, t)}
        >
          <Input
            name="newPassword"
            type="password"
            autoComplete="new-password"
            required
            value={values.newPassword}
            onChange={(event) => setValues({ ...values, newPassword: event.target.value })}
          />
        </FormField>

        <div>
          <Button type="submit" disabled={change.isPending}>
            {change.isPending && <Loader2 className="animate-spin" />}
            {change.isPending ? t('account.changing') : t('account.passwordSection')}
          </Button>
        </div>
      </form>
    </Section>
  )
}
