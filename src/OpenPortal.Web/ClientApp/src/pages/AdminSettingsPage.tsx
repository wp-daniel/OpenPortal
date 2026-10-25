import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query'
import { KeyRound, Loader2, Lock, Mail, ShieldCheck, UserPlus } from 'lucide-react'
import { useState, type FormEvent, type ReactNode } from 'react'
import { settingsApi } from '@/api/settings'
import type { PrivacySettings, RegistrationMode, RegistrationSettings, SecuritySettings, TwoFactorRequirement } from '@/api/types'
import { FormField } from '@/components/FormField'
import { PageHeader } from '@/components/PageHeader'
import { Section } from '@/components/Section'
import { ErrorPanel, LoadingState } from '@/components/StatePanels'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Label } from '@/components/ui/label'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { Textarea } from '@/components/ui/textarea'
import { Switch } from '@/components/ui/switch'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { EmailSettingsTab } from '@/components/settings/EmailSettings'
import { ExternalLoginSettingsTab } from '@/components/settings/ExternalLoginSettings'
import { useTabParam } from '@/hooks/useTabParam'
import { SESSION_QUERY_KEY } from '@/hooks/useSession'
import { notify } from '@/hooks/useToast'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'
import { reportFormError, serverFieldErrors } from '@/lib/forms'

const SETTINGS_KEY = ['admin-settings-security'] as const
const REGISTRATION_KEY = ['admin-settings-registration'] as const
const PRIVACY_KEY = ['admin-settings-privacy'] as const
const SETTINGS_TABS = ['security', 'providers', 'registration', 'privacy', 'email'] as const
const REGISTRATION_MODES: readonly RegistrationMode[] = ['disabled', 'open', 'approval']
const REQUIREMENTS: readonly TwoFactorRequirement[] = ['none', 'administrators', 'everyone']

/** Maximum length of the authenticator issuer name (`SecuritySettings.IssuerMaxLength` on the server). */
const ISSUER_MAX_LENGTH = 64

/**
 * Portal settings, kept in the database and edited here (administrators only), one tab per area: sign-in
 * security, registration and invitations, privacy, and email delivery.
 */
export function AdminSettingsPage() {
  const { t } = useI18n()
  const [tab, setTab] = useTabParam(SETTINGS_TABS)

  return (
    <div className="space-y-6">
      <PageHeader title={t('settings.title')} description={t('settings.description')} />

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList variant="line" className="max-w-full flex-wrap justify-start group-data-[orientation=horizontal]/tabs:h-auto">
          <TabsTrigger value="security">
            <ShieldCheck />
            {t('settings.tabs.security')}
          </TabsTrigger>
          <TabsTrigger value="providers">
            <KeyRound />
            {t('settings.tabs.providers')}
          </TabsTrigger>
          <TabsTrigger value="registration">
            <UserPlus />
            {t('settings.tabs.registration')}
          </TabsTrigger>
          <TabsTrigger value="privacy">
            <Lock />
            {t('settings.tabs.privacy')}
          </TabsTrigger>
          <TabsTrigger value="email">
            <Mail />
            {t('settings.tabs.email')}
          </TabsTrigger>
        </TabsList>

        <TabsContent value="security" className="mt-4">
          <SecurityTab />
        </TabsContent>
        <TabsContent value="providers" className="mt-4">
          <ExternalLoginSettingsTab />
        </TabsContent>
        <TabsContent value="registration" className="mt-4">
          <RegistrationTab />
        </TabsContent>
        <TabsContent value="privacy" className="mt-4">
          <PrivacyTab />
        </TabsContent>
        <TabsContent value="email" className="mt-4">
          <EmailSettingsTab />
        </TabsContent>
      </Tabs>
    </div>
  )
}

function SecurityTab() {
  const settings = useQuery({ queryKey: SETTINGS_KEY, queryFn: ({ signal }) => settingsApi.security(signal) })

  return (
    <QueryState query={settings}>
      {/* Keyed by the saved stamp so the form restarts from the server's values after each save. */}
      {(data) => <SecurityForm key={data.updatedAtUtc ?? 'defaults'} initial={data} />}
    </QueryState>
  )
}

function RegistrationTab() {
  const registration = useQuery({ queryKey: REGISTRATION_KEY, queryFn: ({ signal }) => settingsApi.registration(signal) })

  return (
    <QueryState query={registration}>
      {(data) => <RegistrationForm key={data.updatedAtUtc ?? 'defaults'} initial={data} />}
    </QueryState>
  )
}

function PrivacyTab() {
  const privacy = useQuery({ queryKey: PRIVACY_KEY, queryFn: ({ signal }) => settingsApi.privacy(signal) })

  return (
    <QueryState query={privacy}>{(data) => <PrivacyForm key={data.updatedAtUtc ?? 'defaults'} initial={data} />}</QueryState>
  )
}

/** Loading, error with retry, or the loaded data. */
function QueryState<T>({ query, children }: { query: UseQueryResult<T>; children: (data: T) => ReactNode }) {
  if (query.isPending) return <LoadingState />

  if (query.isError) {
    return <ErrorPanel message={describeError(query.error)} traceId={traceIdOf(query.error)} onRetry={() => void query.refetch()} />
  }

  return <>{children(query.data)}</>
}

/** Whether account holders may delete their own account, and how long they have to change their mind. */
function PrivacyForm({ initial }: { initial: PrivacySettings }) {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const [enabled, setEnabled] = useState(initial.selfDeletionEnabled)
  const [days, setDays] = useState(String(initial.deletionGraceDays))
  const [error, setError] = useState<string>()

  const save = useMutation({
    mutationFn: () => settingsApi.updatePrivacy({ selfDeletionEnabled: enabled, deletionGraceDays: Number(days) }),
    meta: { handlesErrors: true },
    onSuccess: async (saved) => {
      queryClient.setQueryData(PRIVACY_KEY, saved)
      await queryClient.invalidateQueries({ queryKey: SESSION_QUERY_KEY })
      notify.success(t('settings.saved'))
    },
    onError: (failure) => reportFormError(failure, t('settings.saveFailed')),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    const value = Number(days)

    if (!Number.isInteger(value) || value < 0 || value > 365) {
      setError(t('settings.privacy.graceDaysInvalid'))
      return
    }

    setError(undefined)
    save.mutate()
  }

  const server = serverFieldErrors(save.error)

  return (
    <Section title={t('settings.privacy.title')} description={t('settings.privacy.description')}>
      <form onSubmit={submit} noValidate className="grid max-w-2xl items-start gap-6">
        <div className="flex items-start justify-between gap-6 rounded-lg border p-4">
          <div className="grid gap-1">
            <Label htmlFor="privacy-self-deletion">{t('settings.privacy.selfDeletion')}</Label>
            <p className="text-muted-foreground text-sm">{t('settings.privacy.selfDeletionHint')}</p>
          </div>
          <Switch id="privacy-self-deletion" checked={enabled} onCheckedChange={setEnabled} />
        </div>

        <FormField
          label={t('settings.privacy.graceDays')}
          htmlFor="privacy-grace-days"
          hint={t('settings.privacy.graceDaysHint')}
          error={error ?? server.deletionGraceDays?.[0]}
        >
          <Input type="number" min={0} max={365} className="max-w-32" value={days} onChange={(event) => setDays(event.target.value)} />
        </FormField>

        <div>
          <Button type="submit" disabled={save.isPending}>
            {save.isPending && <Loader2 className="animate-spin" />}
            {t('common.save')}
          </Button>
        </div>
      </form>
    </Section>
  )
}

/**
 * How accounts come to exist besides an administrator creating them: self-registration (off, open, or waiting
 * for approval), which email domains may register, whether the address must be confirmed, and how long an
 * invitation link stays valid.
 */
function RegistrationForm({ initial }: { initial: RegistrationSettings }) {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const [mode, setMode] = useState<RegistrationMode>(initial.mode)
  const [domains, setDomains] = useState(initial.allowedDomains.join('\n'))
  const [confirm, setConfirm] = useState(initial.requireEmailConfirmation)
  const [days, setDays] = useState(String(initial.invitationLifetimeDays))
  const [daysError, setDaysError] = useState<string | undefined>()

  const allowedDomains = domains
    .split(/[\s,;]+/)
    .map((domain) => domain.trim().toLowerCase())
    .filter((domain) => domain !== '')

  const save = useMutation({
    mutationFn: () =>
      settingsApi.updateRegistration({
        mode,
        allowedDomains,
        requireEmailConfirmation: confirm,
        invitationLifetimeDays: Number(days),
      }),
    meta: { handlesErrors: true },
    onSuccess: async (saved) => {
      notify.success(t('settings.saved'))
      queryClient.setQueryData(REGISTRATION_KEY, saved)
      // The sign-in page reads whether registration is offered from the session.
      await queryClient.invalidateQueries({ queryKey: SESSION_QUERY_KEY })
    },
    onError: (failure) => reportFormError(failure, t('settings.saveFailed')),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    const value = Number(days)

    if (!Number.isInteger(value) || value < 1 || value > 90) {
      setDaysError(t('settings.registration.daysInvalid'))
      return
    }

    setDaysError(undefined)
    save.mutate()
  }

  const serverErrors = serverFieldErrors(save.error)
  const needsEmail = mode !== 'disabled' && confirm && !initial.emailDeliveryAvailable

  return (
    <Section title={t('settings.registration.title')} description={t('settings.registration.description')}>
      <form onSubmit={submit} noValidate className="grid max-w-2xl items-start gap-6">
        {!initial.emailDeliveryAvailable && (
          <Alert>
            <AlertDescription>{t('settings.registration.noEmail')}</AlertDescription>
          </Alert>
        )}

        <fieldset className="grid gap-3">
          <legend className="mb-1 text-sm font-medium">{t('settings.registration.mode')}</legend>
          <RadioGroup value={mode} onValueChange={(value) => setMode(value as RegistrationMode)} className="gap-2">
            {REGISTRATION_MODES.map((value) => (
              <div key={value} className="flex items-start gap-3">
                <RadioGroupItem value={value} id={`registration-${value}`} className="mt-0.5" />
                <div className="grid gap-0.5">
                  <Label htmlFor={`registration-${value}`}>{t(`settings.registration.modes.${value}`)}</Label>
                  <p className="text-muted-foreground text-xs">{t(`settings.registration.modes.${value}Hint`)}</p>
                </div>
              </div>
            ))}
          </RadioGroup>
        </fieldset>

        <div className="flex items-start justify-between gap-6 rounded-lg border p-4">
          <div className="grid gap-1">
            <Label htmlFor="registration-confirm">{t('settings.registration.confirm')}</Label>
            <p className="text-muted-foreground text-sm">{t('settings.registration.confirmHint')}</p>
          </div>
          <Switch id="registration-confirm" checked={confirm} onCheckedChange={setConfirm} />
        </div>

        {needsEmail && <p className="text-destructive text-sm">{t('settings.registration.confirmNeedsEmail')}</p>}

        <FormField
          label={t('settings.registration.domains')}
          htmlFor="registration-domains"
          hint={t('settings.registration.domainsHint')}
          error={serverErrors.allowedDomains?.[0]}
        >
          <Textarea
            rows={3}
            className="font-mono text-xs"
            placeholder="example.com"
            value={domains}
            onChange={(event) => setDomains(event.target.value)}
          />
        </FormField>

        <FormField
          label={t('settings.registration.invitationDays')}
          htmlFor="registration-days"
          hint={t('settings.registration.invitationDaysHint')}
          error={daysError ?? serverErrors.invitationLifetimeDays?.[0]}
        >
          <Input type="number" min={1} max={90} className="w-32" value={days} onChange={(event) => setDays(event.target.value)} />
        </FormField>

        <div>
          <Button type="submit" disabled={save.isPending || needsEmail}>
            {save.isPending && <Loader2 className="animate-spin" />}
            {save.isPending ? t('common.saving') : t('common.save')}
          </Button>
        </div>
      </form>
    </Section>
  )
}

function SecurityForm({ initial }: { initial: SecuritySettings }) {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const [twoFactorEnabled, setTwoFactorEnabled] = useState(initial.twoFactorEnabled)
  const [issuer, setIssuer] = useState(initial.twoFactorIssuer)
  const [tokens, setTokens] = useState(initial.personalAccessTokensEnabled)
  const [passkeys, setPasskeys] = useState(initial.passkeysEnabled)
  const [requirement, setRequirement] = useState<TwoFactorRequirement>(initial.twoFactorRequirement)
  const [idleMinutes, setIdleMinutes] = useState(String(initial.idleTimeoutMinutes))
  const [issuerError, setIssuerError] = useState<string | undefined>()
  const [idleError, setIdleError] = useState<string | undefined>()

  const save = useMutation({
    mutationFn: () =>
      settingsApi.updateSecurity({
        twoFactorEnabled,
        twoFactorIssuer: issuer.trim(),
        personalAccessTokensEnabled: tokens,
        passkeysEnabled: passkeys,
        twoFactorRequirement: requirement,
        idleTimeoutMinutes: Number(idleMinutes),
      }),
    meta: { handlesErrors: true },
    onSuccess: async (saved) => {
      notify.success(t('settings.saved'))
      queryClient.setQueryData(SETTINGS_KEY, saved)
      // The session says whether the account page offers tokens.
      await queryClient.invalidateQueries({ queryKey: SESSION_QUERY_KEY })
    },
    onError: (failure) => reportFormError(failure, t('settings.saveFailed')),
  })

  function submit(event: FormEvent) {
    event.preventDefault()

    const value = issuer.trim()

    if (value.length === 0) {
      setIssuerError(t('settings.issuerRequired'))

      return
    }

    if (value.length > ISSUER_MAX_LENGTH || value.includes(':')) {
      setIssuerError(t('settings.issuerInvalid'))

      return
    }

    setIssuerError(undefined)

    const idle = Number(idleMinutes)
    if (!Number.isInteger(idle) || (idle !== 0 && (idle < 5 || idle > 1440))) {
      setIdleError(t('settings.idle.invalid'))

      return
    }

    setIdleError(undefined)
    save.mutate()
  }

  const serverErrors = serverFieldErrors(save.error)
  const dirty =
    twoFactorEnabled !== initial.twoFactorEnabled ||
    issuer.trim() !== initial.twoFactorIssuer ||
    tokens !== initial.personalAccessTokensEnabled ||
    passkeys !== initial.passkeysEnabled ||
    Number(idleMinutes) !== initial.idleTimeoutMinutes ||
    requirement !== initial.twoFactorRequirement
  const unsatisfiable = requirement !== 'none' && !twoFactorEnabled && !passkeys

  return (
    <Section title={t('settings.security')} description={t('settings.securityDescription')}>
      <form onSubmit={submit} noValidate className="grid max-w-2xl items-start gap-6">
        <div className="flex items-start justify-between gap-6 rounded-lg border p-4">
          <div className="grid gap-1">
            <Label htmlFor="twoFactorEnabled">{t('settings.twoFactor')}</Label>
            <p className="text-muted-foreground text-sm">{t('settings.twoFactorDescription')}</p>
          </div>
          <Switch id="twoFactorEnabled" checked={twoFactorEnabled} onCheckedChange={setTwoFactorEnabled} />
        </div>

        <FormField
          label={t('settings.issuer')}
          htmlFor="twoFactorIssuer"
          hint={t('settings.issuerHint')}
          error={issuerError ?? serverErrors.twoFactorIssuer?.[0]}
        >
          <Input
            name="twoFactorIssuer"
            maxLength={ISSUER_MAX_LENGTH}
            value={issuer}
            onChange={(event) => setIssuer(event.target.value)}
          />
        </FormField>

        <div className="flex items-start justify-between gap-6 rounded-lg border p-4">
          <div className="grid gap-1">
            <Label htmlFor="passkeysEnabled">{t('settings.passkeys')}</Label>
            <p className="text-muted-foreground text-sm">{t('settings.passkeysDescription')}</p>
          </div>
          <Switch id="passkeysEnabled" checked={passkeys} onCheckedChange={setPasskeys} />
        </div>

        <fieldset className="grid gap-3">
          <legend className="mb-1 text-sm font-medium">{t('settings.requirement.title')}</legend>
          <RadioGroup value={requirement} onValueChange={(value) => setRequirement(value as TwoFactorRequirement)} className="gap-2">
            {REQUIREMENTS.map((value) => (
              <div key={value} className="flex items-start gap-3">
                <RadioGroupItem value={value} id={`requirement-${value}`} className="mt-0.5" />
                <div className="grid gap-0.5">
                  <Label htmlFor={`requirement-${value}`}>{t(`settings.requirement.${value}`)}</Label>
                  <p className="text-muted-foreground text-xs">{t(`settings.requirement.${value}Hint`)}</p>
                </div>
              </div>
            ))}
          </RadioGroup>
          {unsatisfiable && <p className="text-destructive text-sm">{t('settings.requirement.unsatisfiable')}</p>}
        </fieldset>

        <div className="flex items-start justify-between gap-6 rounded-lg border p-4">
          <div className="grid gap-1">
            <Label htmlFor="personalAccessTokensEnabled">{t('settings.tokens')}</Label>
            <p className="text-muted-foreground text-sm">{t('settings.tokensDescription')}</p>
          </div>
          <Switch id="personalAccessTokensEnabled" checked={tokens} onCheckedChange={setTokens} />
        </div>

        <FormField
          label={t('settings.idle.label')}
          htmlFor="idleTimeoutMinutes"
          hint={t('settings.idle.hint')}
          error={idleError ?? serverErrors.idleTimeoutMinutes?.[0]}
        >
          <Input
            type="number"
            min={0}
            max={1440}
            className="w-32"
            value={idleMinutes}
            onChange={(event) => setIdleMinutes(event.target.value)}
          />
        </FormField>

        <div>
          <Button type="submit" disabled={save.isPending || !dirty || unsatisfiable}>
            {save.isPending && <Loader2 className="animate-spin" />}
            {save.isPending ? t('common.saving') : t('common.save')}
          </Button>
        </div>
      </form>
    </Section>
  )
}
