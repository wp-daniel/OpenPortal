import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { settingsApi } from '@/api/settings'
import type { SecuritySettings } from '@/api/types'
import { FormField } from '@/components/FormField'
import { PageHeader } from '@/components/PageHeader'
import { Section } from '@/components/Section'
import { ErrorPanel, LoadingState } from '@/components/StatePanels'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import { notify } from '@/hooks/useToast'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'
import { reportFormError, serverFieldErrors } from '@/lib/forms'

const SETTINGS_KEY = ['admin-settings-security'] as const

/** Maximum length of the authenticator issuer name (`SecuritySettings.IssuerMaxLength` on the server). */
const ISSUER_MAX_LENGTH = 64

/**
 * Portal settings, kept in the database and edited here (administrators only). Today: sign-in security, i.e.
 * whether accounts can protect themselves with an authenticator app.
 */
export function AdminSettingsPage() {
  const { t } = useI18n()

  const settings = useQuery({ queryKey: SETTINGS_KEY, queryFn: ({ signal }) => settingsApi.security(signal) })

  return (
    <div className="space-y-6">
      <PageHeader title={t('settings.title')} description={t('settings.description')} />

      {settings.isPending && <LoadingState />}

      {settings.isError && (
        <ErrorPanel
          message={describeError(settings.error)}
          traceId={traceIdOf(settings.error)}
          onRetry={() => void settings.refetch()}
        />
      )}

      {/* Keyed by the saved stamp so the form restarts from the server's values after each save. */}
      {settings.data && <SecurityForm key={settings.data.updatedAtUtc ?? 'defaults'} initial={settings.data} />}
    </div>
  )
}

function SecurityForm({ initial }: { initial: SecuritySettings }) {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const [twoFactorEnabled, setTwoFactorEnabled] = useState(initial.twoFactorEnabled)
  const [issuer, setIssuer] = useState(initial.twoFactorIssuer)
  const [issuerError, setIssuerError] = useState<string | undefined>()

  const save = useMutation({
    mutationFn: () => settingsApi.updateSecurity({ twoFactorEnabled, twoFactorIssuer: issuer.trim() }),
    meta: { handlesErrors: true },
    onSuccess: (saved) => {
      notify.success(t('settings.saved'))
      queryClient.setQueryData(SETTINGS_KEY, saved)
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
    save.mutate()
  }

  const serverErrors = serverFieldErrors(save.error)
  const dirty = twoFactorEnabled !== initial.twoFactorEnabled || issuer.trim() !== initial.twoFactorIssuer

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

        <div>
          <Button type="submit" disabled={save.isPending || !dirty}>
            {save.isPending && <Loader2 className="animate-spin" />}
            {save.isPending ? t('common.saving') : t('common.save')}
          </Button>
        </div>
      </form>
    </Section>
  )
}
