import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Info, Loader2, LockKeyhole, UserRound } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { z } from 'zod'
import { accountApi } from '@/api/auth'
import { ApiError } from '@/api/client'
import type { AccountProfile, PasswordPolicy, SessionUser } from '@/api/types'
import { AvatarUpload } from '@/components/AvatarUpload'
import { FormField } from '@/components/FormField'
import { PageHeader } from '@/components/PageHeader'
import { Section } from '@/components/Section'
import { LoadingState } from '@/components/StatePanels'
import { TwoFactorSection } from '@/components/TwoFactorSection'
import { UserDetailsFields } from '@/components/UserDetailsFields'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Separator } from '@/components/ui/separator'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { SESSION_QUERY_KEY, useSession } from '@/hooks/useSession'
import { useTabParam } from '@/hooks/useTabParam'
import { notify } from '@/hooks/useToast'
import { formatDate } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { reportFormError, serverFieldErrors, zodFieldErrors } from '@/lib/forms'
import { describePasswordPolicy, newPasswordSchema } from '@/lib/passwordPolicy'
import {
  detailsToForm,
  formToDetails,
  userDetailsShape,
  type UserDetailsField,
  type UserDetailsForm,
} from '@/lib/userDetails'


/**
 * The signed-in account's own settings.
 *
 * Email is shown read-only: it is the sign-in identifier, so changing it needs a confirmation flow that does
 * not exist here. Offering the field would let an account lock itself out.
 */
export function AccountPage() {
  const { user, session } = useSession()
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const [tab, setTab] = useTabParam(ACCOUNT_TABS)

  const profile = useQuery({
    queryKey: ['account-profile'],
    queryFn: ({ signal }) => accountApi.profile(signal),
  })

  return (
    <div className="space-y-6">
      <PageHeader title={t('account.title')} description={t('account.description')} />

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList variant="line" className="max-w-full flex-wrap justify-start group-data-[orientation=horizontal]/tabs:h-auto">
          <TabsTrigger value="profile">
            <UserRound />
            {t('account.tab.profile')}
          </TabsTrigger>
          <TabsTrigger value="security">
            <LockKeyhole />
            {t('account.tab.security')}
          </TabsTrigger>
          <TabsTrigger value="details">
            <Info />
            {t('account.tab.details')}
          </TabsTrigger>
        </TabsList>
        <Separator className="-mt-2" />

        <TabsContent value="profile" className="space-y-6 pt-4">
          {profile.isPending && <LoadingState />}

          {profile.data && (
            <>
              <Section title={t('avatar.title')} description={t('account.avatarDescription')}>
                <AvatarUpload
                  user={profile.data}
                  upload={accountApi.uploadAvatar}
                  remove={accountApi.removeAvatar}
                  // The session carries the stamp the sidebar's picture is versioned by.
                  onChanged={() => {
                    void queryClient.invalidateQueries({ queryKey: SESSION_QUERY_KEY })
                    void queryClient.invalidateQueries({ queryKey: ['account-profile'] })
                  }}
                />
              </Section>
              <ProfileForm initial={profile.data} />
            </>
          )}
        </TabsContent>

        <TabsContent value="security" className="space-y-6 pt-4">
          <PasswordForm policy={session.passwordPolicy} />
          <TwoFactorSection />
        </TabsContent>

        <TabsContent value="details" className="pt-4">
          <AccountDetails user={user} profile={profile.data} />
        </TabsContent>
      </Tabs>
    </div>
  )
}

const ACCOUNT_TABS = ['profile', 'security', 'details'] as const

/** Read-only facts about the account that the user cannot edit here. */
function AccountDetails({ user, profile }: { user: SessionUser | null; profile: AccountProfile | undefined }) {
  const { t } = useI18n()

  return (
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
          <dt className="text-muted-foreground">{t('users.administrator')}</dt>
          <dd>{user?.roles.includes('Administrator') ? t('common.yes') : t('common.no')}</dd>
        </div>
        {profile && (
          <div className="space-y-1">
            <dt className="text-muted-foreground">{t('account.memberSince')}</dt>
            <dd>{formatDate(profile.createdAtUtc)}</dd>
          </div>
        )}
      </dl>
    </Section>
  )
}

function ProfileForm({ initial }: { initial: AccountProfile }) {
  const queryClient = useQueryClient()
  const { t } = useI18n()
  const profileSchema = useMemo(() => z.object(userDetailsShape(t)), [t])
  const [details, setDetails] = useState<UserDetailsForm>(detailsToForm(initial))
  const [errors, setErrors] = useState<Partial<Record<UserDetailsField, string>>>({})

  const save = useMutation({
    mutationFn: () => accountApi.updateProfile(formToDetails(details)),
    meta: { handlesErrors: true },
    onSuccess: async (updated) => {
      notify.success(t('account.profileSaved'))
      setErrors({})
      setDetails(detailsToForm(updated))
      // The session query carries the display name shown in the header, so it is stale after this write.
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: SESSION_QUERY_KEY }),
        queryClient.invalidateQueries({ queryKey: ['account-profile'] }),
      ])
    },
    onError: (failure) => reportFormError(failure, t('account.saveFailed')),
  })

  function submit(event: FormEvent) {
    event.preventDefault()

    const parsed = profileSchema.safeParse(details)

    if (!parsed.success) {
      setErrors(zodFieldErrors(parsed.error))

      return
    }

    setErrors({})
    save.mutate()
  }

  const serverErrors = serverFieldErrors(save.error)
  const errorFor = (field: UserDetailsField) => errors[field] ?? serverErrors[field]?.[0]

  return (
    <Section title={t('account.profileSection')} description={t('account.profileDescription')}>
      <form onSubmit={submit} noValidate className="grid gap-6">
        <UserDetailsFields
          idPrefix="profile"
          values={details}
          onChange={(field, value) => setDetails((current) => ({ ...current, [field]: value }))}
          errors={{
            firstName: errorFor('firstName'),
            lastName: errorFor('lastName'),
            phoneNumber: errorFor('phoneNumber'),
            jobTitle: errorFor('jobTitle'),
            company: errorFor('company'),
            department: errorFor('department'),
            addressLine: errorFor('addressLine'),
            city: errorFor('city'),
            postalCode: errorFor('postalCode'),
            country: errorFor('country'),
          }}
        />

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
