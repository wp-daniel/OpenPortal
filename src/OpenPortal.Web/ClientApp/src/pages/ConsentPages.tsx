import { useQuery } from '@tanstack/react-query'
import { CheckCircle2, Loader2, MonitorSmartphone, ShieldQuestion, XCircle } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useLocation, useSearchParams } from 'react-router-dom'
import { myOrganizationsApi, organizationKeys } from '@/api/organizations'
import { FormField } from '@/components/FormField'
import { Section } from '@/components/Section'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { useSession } from '@/hooks/useSession'
import { useI18n } from '@/i18n/useI18n'
import { postForm } from '@/lib/formPost'
import { serverReturnUrl } from '@/lib/returnUrl'

const CARD = 'bg-card/80 shadow-lg backdrop-blur'

/** The scopes the portal describes; anything else is shown as it is. */
const KNOWN_SCOPES = ['openid', 'profile', 'email', 'phone', 'address', 'offline_access', 'roles', 'groups', 'organization'] as const

/** What an application asks to receive, one line per scope. */
function ScopeList({ scopes }: { scopes: readonly string[] }) {
  const { t } = useI18n()

  return (
    <ul className="grid gap-2 rounded-lg border p-3 text-left text-sm">
      {scopes.map((scope) => (
        <li key={scope} className="flex items-start gap-2">
          <CheckCircle2 className="text-muted-foreground mt-0.5 size-4 shrink-0" />
          <span>{(KNOWN_SCOPES as readonly string[]).includes(scope) ? t(`consent.scope.${scope}`) : scope}</span>
        </li>
      ))}
    </ul>
  )
}

/**
 * An application that asks for consent (usually a third party's) sends the person here before its first sign-in. The
 * answer goes back to the authorization endpoint as a form post with the antiforgery token, and the browser follows
 * its redirect to the application.
 */
export function ConsentPage() {
  const { t } = useI18n()
  const { session } = useSession()
  const location = useLocation()
  const [searchParams] = useSearchParams()
  const [busy, setBusy] = useState(false)

  const returnUrl = serverReturnUrl(location.search)
  const app = searchParams.get('app') ?? ''
  const request = returnUrl ? new URL(returnUrl, window.location.origin).searchParams : null
  const scopes = (request?.get('scope') ?? '').split(' ').filter((scope) => scope !== '')

  async function answer(consent: 'granted' | 'denied') {
    if (!request) return

    setBusy(true)
    const fields = new URLSearchParams(request)
    fields.set('consent', consent)
    await postForm('/connect/authorize', fields)
  }

  if (!request) {
    return (
      <Section className={CARD} title={t('consent.title')}>
        <Alert variant="destructive">
          <AlertDescription>{t('consent.invalid')}</AlertDescription>
        </Alert>
      </Section>
    )
  }

  return (
    <Section
      className={CARD}
      title={
        <span className="flex items-center gap-2">
          <ShieldQuestion className="size-5" />
          {t('consent.title')}
        </span>
      }
      description={t('consent.description', { app, user: session.user?.email ?? '' })}
    >
      <div className="grid gap-5">
        <ScopeList scopes={scopes} />
        <p className="text-muted-foreground text-xs">{t('consent.hint')}</p>
        <div className="flex flex-wrap justify-end gap-2">
          <Button variant="outline" disabled={busy} onClick={() => void answer('denied')}>
            {t('consent.deny')}
          </Button>
          <Button disabled={busy} onClick={() => void answer('granted')}>
            {busy && <Loader2 className="animate-spin" />}
            {t('consent.allow', { app })}
          </Button>
        </div>
      </div>
    </Section>
  )
}

/**
 * The device flow's page (RFC 8628): the person types the code a tool showed them, sees which application it is and
 * what it wants, and approves or refuses. The portal's verification endpoint does the checks and comes back here
 * with the outcome.
 */
export function DevicePage() {
  const { t } = useI18n()
  const [searchParams] = useSearchParams()
  const [code, setCode] = useState('')
  const [organization, setOrganization] = useState<string>('')
  const [busy, setBusy] = useState(false)

  const status = searchParams.get('status')
  const error = searchParams.get('error')
  const userCode = searchParams.get('user_code')
  const app = searchParams.get('app') ?? ''
  const scopes = (searchParams.get('scope') ?? '').split(' ').filter((scope) => scope !== '')

  const organizations = useQuery({
    queryKey: organizationKeys.mine,
    queryFn: ({ signal }) => myOrganizationsApi.list(signal),
    enabled: userCode !== null,
  })

  function check(event: FormEvent) {
    event.preventDefault()
    const value = code.trim()
    if (value) window.location.assign(`/connect/verify?user_code=${encodeURIComponent(value)}`)
  }

  async function decide(decision: 'approve' | 'deny') {
    if (!userCode) return

    setBusy(true)
    const fields: Record<string, string> = { user_code: userCode, decision }
    if (organization) fields.organization = organization
    await postForm('/connect/verify', fields)
  }

  const title = (
    <span className="flex items-center gap-2">
      <MonitorSmartphone className="size-5" />
      {t('device.title')}
    </span>
  )

  if (status === 'approved' || status === 'denied') {
    return (
      <Section className={CARD} title={title}>
        <div className="grid justify-items-center gap-3 py-2 text-center">
          {status === 'approved' ? <CheckCircle2 className="text-success size-10" /> : <XCircle className="text-destructive size-10" />}
          <p className="text-sm">{t(`device.${status}`)}</p>
        </div>
      </Section>
    )
  }

  if (userCode) {
    const choices = organizations.data ?? []

    return (
      <Section className={CARD} title={title} description={t('device.confirm', { app, code: userCode })}>
        <div className="grid gap-5">
          <ScopeList scopes={scopes} />
          {choices.length > 1 && (
            <fieldset className="grid gap-2">
              <legend className="mb-1 text-sm font-medium">{t('device.organization')}</legend>
              <RadioGroup value={organization} onValueChange={setOrganization} className="gap-2">
                {choices.map((choice) => (
                  <div key={choice.id} className="flex items-center gap-2">
                    <RadioGroupItem value={choice.slug} id={`device-org-${choice.id}`} />
                    <Label htmlFor={`device-org-${choice.id}`} className="font-normal">
                      {choice.name}
                    </Label>
                  </div>
                ))}
              </RadioGroup>
            </fieldset>
          )}
          <p className="text-muted-foreground text-xs">{t('device.warning')}</p>
          <div className="flex flex-wrap justify-end gap-2">
            <Button variant="outline" disabled={busy} onClick={() => void decide('deny')}>
              {t('device.deny')}
            </Button>
            <Button disabled={busy} onClick={() => void decide('approve')}>
              {busy && <Loader2 className="animate-spin" />}
              {t('device.approve')}
            </Button>
          </div>
        </div>
      </Section>
    )
  }

  return (
    <Section className={CARD} title={title} description={t('device.description')}>
      <form onSubmit={check} noValidate className="grid gap-5">
        <FormField label={t('device.code')} htmlFor="device-code" error={error === 'invalid_code' ? t('device.invalidCode') : undefined}>
          <Input
            autoFocus
            autoComplete="off"
            spellCheck={false}
            className="text-center font-mono text-lg tracking-widest uppercase"
            placeholder="ABCD-EFGH"
            value={code}
            onChange={(event) => setCode(event.target.value)}
          />
        </FormField>
        <Button type="submit" disabled={!code.trim()}>
          {t('device.continue')}
        </Button>
      </form>
    </Section>
  )
}
