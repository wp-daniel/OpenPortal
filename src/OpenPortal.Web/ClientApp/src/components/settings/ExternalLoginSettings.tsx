import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Copy, Loader2 } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { settingsApi } from '@/api/settings'
import type { ExternalProviderSettings, ExternalSignUpMode } from '@/api/types'
import { ExternalProviderIcon } from '@/components/ExternalProviders'
import { FormField } from '@/components/FormField'
import { Section } from '@/components/Section'
import { ErrorPanel, LoadingState } from '@/components/StatePanels'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { Switch } from '@/components/ui/switch'
import { Textarea } from '@/components/ui/textarea'
import { SESSION_QUERY_KEY } from '@/hooks/useSession'
import { notify } from '@/hooks/useToast'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'
import { reportFormError, serverFieldErrors } from '@/lib/forms'

const EXTERNAL_LOGINS_KEY = ['admin-settings-external-logins'] as const
const SIGN_UP_MODES: readonly ExternalSignUpMode[] = ['disabled', 'open', 'approval']
const SHARED_TENANTS = ['organizations', 'common', 'consumers'] as const

/** Google and Microsoft sign-in (Settings → Sign-in providers): one form per provider. */
export function ExternalLoginSettingsTab() {
  const { t } = useI18n()
  const providers = useQuery({ queryKey: EXTERNAL_LOGINS_KEY, queryFn: ({ signal }) => settingsApi.externalLogins(signal) })

  return (
    <div className="space-y-6">
      <Alert>
        <AlertDescription>{t('settings.external.intro')}</AlertDescription>
      </Alert>
      {providers.isPending && <LoadingState />}
      {providers.isError && (
        <ErrorPanel
          message={describeError(providers.error)}
          traceId={traceIdOf(providers.error)}
          onRetry={() => void providers.refetch()}
        />
      )}
      {providers.data?.map((provider) => (
        <ProviderForm key={`${provider.key}:${provider.updatedAtUtc ?? 'new'}`} initial={provider} />
      ))}
    </div>
  )
}

function ProviderForm({ initial }: { initial: ExternalProviderSettings }) {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const microsoft = initial.key === 'microsoft'
  const [enabled, setEnabled] = useState(initial.enabled)
  const [clientId, setClientId] = useState(initial.clientId ?? '')
  const [clientSecret, setClientSecret] = useState('')
  const initialTenant = initial.tenant ?? 'organizations'
  const sharedInitially = (SHARED_TENANTS as readonly string[]).includes(initialTenant)
  const [tenantKind, setTenantKind] = useState<string>(sharedInitially ? initialTenant : 'single')
  const [tenant, setTenant] = useState(sharedInitially ? '' : initialTenant)
  const [signUp, setSignUp] = useState<ExternalSignUpMode>(initial.signUp)
  const [domains, setDomains] = useState(initial.allowedDomains.join('\n'))
  const [errors, setErrors] = useState<{ clientId?: string; clientSecret?: string; tenant?: string }>({})

  const redirectUri = `${window.location.origin}${initial.callbackPath}`

  const save = useMutation({
    mutationFn: () =>
      settingsApi.updateExternalLogin(initial.key, {
        enabled,
        clientId: clientId.trim() || null,
        clientSecret: clientSecret.trim() || undefined,
        tenant: microsoft ? (tenantKind === 'single' ? tenant.trim() : tenantKind) : null,
        signUp,
        allowedDomains: domains
          .split(/[\s,;]+/)
          .map((domain) => domain.trim().toLowerCase())
          .filter((domain) => domain !== ''),
      }),
    meta: { handlesErrors: true },
    onSuccess: async () => {
      notify.success(t('settings.saved'))
      await queryClient.invalidateQueries({ queryKey: EXTERNAL_LOGINS_KEY })
      // The sign-in page reads the providers from the session.
      await queryClient.invalidateQueries({ queryKey: SESSION_QUERY_KEY })
    },
    onError: (failure) => reportFormError(failure, t('settings.saveFailed')),
  })

  function submit(event: FormEvent) {
    event.preventDefault()

    const next: typeof errors = {}
    if (enabled && clientId.trim() === '') next.clientId = t('settings.external.clientIdRequired')
    if (enabled && !initial.hasClientSecret && clientSecret.trim() === '') next.clientSecret = t('settings.external.secretRequired')
    if (microsoft && tenantKind === 'single' && tenant.trim() === '') next.tenant = t('settings.external.tenantRequired')
    setErrors(next)

    if (Object.keys(next).length === 0) save.mutate()
  }

  async function copyRedirectUri() {
    try {
      await navigator.clipboard.writeText(redirectUri)
      notify.success(t('common.copied'))
    } catch {
      notify.error(t('common.copyFailed'))
    }
  }

  const server = serverFieldErrors(save.error)

  return (
    <Section
      title={
        <span className="flex items-center gap-2">
          <ExternalProviderIcon provider={initial.key} className="size-5" />
          {initial.displayName}
          <Badge variant={initial.enabled ? 'success' : 'secondary'}>
            {initial.enabled ? t('settings.external.on') : t('settings.external.off')}
          </Badge>
        </span>
      }
      description={t(`settings.external.${initial.key}.description`)}
    >
      <form onSubmit={submit} noValidate className="grid max-w-2xl items-start gap-6">
        <FormField label={t('settings.external.redirectUri')} htmlFor={`${initial.key}-redirect`} hint={t(`settings.external.${initial.key}.redirectHint`)}>
          <div className="flex gap-2">
            <Input readOnly value={redirectUri} className="font-mono text-xs" onFocus={(event) => event.target.select()} />
            <Button variant="outline" size="icon" onClick={() => void copyRedirectUri()} aria-label={t('common.copy')}>
              <Copy />
            </Button>
          </div>
        </FormField>

        <div className="grid items-start gap-5 sm:grid-cols-2">
          <FormField label={t('settings.external.clientId')} htmlFor={`${initial.key}-client-id`} error={errors.clientId ?? server.clientId?.[0]}>
            <Input autoComplete="off" spellCheck={false} value={clientId} onChange={(event) => setClientId(event.target.value)} />
          </FormField>
          <FormField
            label={t('settings.external.clientSecret')}
            htmlFor={`${initial.key}-client-secret`}
            hint={initial.hasClientSecret ? t('settings.external.secretStored') : undefined}
            error={errors.clientSecret ?? server.clientSecret?.[0]}
          >
            <Input
              type="password"
              autoComplete="new-password"
              placeholder={initial.hasClientSecret ? '••••••••' : ''}
              value={clientSecret}
              onChange={(event) => setClientSecret(event.target.value)}
            />
          </FormField>
        </div>

        {microsoft && (
          <fieldset className="grid gap-3">
            <legend className="mb-1 text-sm font-medium">{t('settings.external.tenant')}</legend>
            <RadioGroup value={tenantKind} onValueChange={setTenantKind} className="gap-2">
              {[...SHARED_TENANTS, 'single'].map((value) => (
                <div key={value} className="flex items-start gap-3">
                  <RadioGroupItem value={value} id={`microsoft-tenant-${value}`} className="mt-0.5" />
                  <div className="grid gap-0.5">
                    <Label htmlFor={`microsoft-tenant-${value}`}>{t(`settings.external.tenants.${value}`)}</Label>
                    <p className="text-muted-foreground text-xs">{t(`settings.external.tenants.${value}Hint`)}</p>
                  </div>
                </div>
              ))}
            </RadioGroup>
            {tenantKind === 'single' && (
              <FormField label={t('settings.external.tenantId')} htmlFor="microsoft-tenant-id" error={errors.tenant ?? server.tenant?.[0]}>
                <Input
                  spellCheck={false}
                  placeholder="contoso.onmicrosoft.com"
                  className="font-mono text-xs"
                  value={tenant}
                  onChange={(event) => setTenant(event.target.value)}
                />
              </FormField>
            )}
          </fieldset>
        )}

        <fieldset className="grid gap-3">
          <legend className="mb-1 text-sm font-medium">{t('settings.external.signUp')}</legend>
          <RadioGroup value={signUp} onValueChange={(value) => setSignUp(value as ExternalSignUpMode)} className="gap-2">
            {SIGN_UP_MODES.map((value) => (
              <div key={value} className="flex items-start gap-3">
                <RadioGroupItem value={value} id={`${initial.key}-signup-${value}`} className="mt-0.5" />
                <div className="grid gap-0.5">
                  <Label htmlFor={`${initial.key}-signup-${value}`}>{t(`settings.external.signUpModes.${value}`)}</Label>
                  <p className="text-muted-foreground text-xs">{t(`settings.external.signUpModes.${value}Hint`)}</p>
                </div>
              </div>
            ))}
          </RadioGroup>
        </fieldset>

        {signUp !== 'disabled' && (
          <FormField
            label={t('settings.external.domains')}
            htmlFor={`${initial.key}-domains`}
            hint={t('settings.external.domainsHint')}
            error={server.allowedDomains?.[0]}
          >
            <Textarea
              rows={3}
              className="font-mono text-xs"
              placeholder="example.com"
              value={domains}
              onChange={(event) => setDomains(event.target.value)}
            />
          </FormField>
        )}

        <div className="flex items-start justify-between gap-6 rounded-lg border p-4">
          <div className="grid gap-1">
            <Label htmlFor={`${initial.key}-enabled`}>{t('settings.external.enable', { provider: initial.displayName })}</Label>
            <p className="text-muted-foreground text-sm">{t('settings.external.enableHint')}</p>
          </div>
          <Switch id={`${initial.key}-enabled`} checked={enabled} onCheckedChange={setEnabled} />
        </div>

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
