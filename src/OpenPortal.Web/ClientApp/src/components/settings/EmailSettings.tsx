import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCircle2, Loader2, RotateCcw, Send, Trash2, XCircle } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { outboxApi, settingsApi } from '@/api/settings'
import type { MailSecurity, MailServerRequest, MailSettings, MailTestResult, OutboxStatus } from '@/api/types'
import { FormField } from '@/components/FormField'
import { Section } from '@/components/Section'
import { EmptyState, ErrorPanel, LoadingState } from '@/components/StatePanels'
import { FilterSelect, TableToolbar } from '@/components/TableToolbar'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { notify } from '@/hooks/useToast'
import { formatDate } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'
import { reportFormError, serverFieldErrors } from '@/lib/forms'
import { matchesSearch } from '@/lib/tableFilter'

const EMAIL_SETTINGS_KEY = ['admin-settings-email'] as const
const OUTBOX_KEY = ['admin-outbox'] as const
const SECURITY_OPTIONS: readonly MailSecurity[] = ['auto', 'starttls', 'ssl', 'none']
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

/** The SMTP server and sender, a test email, and the delivery queue (Settings → Email). */
export function EmailSettingsTab() {
  const settings = useQuery({ queryKey: EMAIL_SETTINGS_KEY, queryFn: ({ signal }) => settingsApi.email(signal) })

  return (
    <div className="space-y-6">
      {settings.isPending && <LoadingState />}
      {settings.isError && (
        <ErrorPanel
          message={describeError(settings.error)}
          traceId={traceIdOf(settings.error)}
          onRetry={() => void settings.refetch()}
        />
      )}
      {settings.data && <EmailForm key={settings.data.updatedAtUtc ?? 'defaults'} initial={settings.data} />}
      <OutboxPanel />
    </div>
  )
}

type Field = 'host' | 'port' | 'fromAddress' | 'to'

function EmailForm({ initial }: { initial: MailSettings }) {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const [host, setHost] = useState(initial.host ?? '')
  const [port, setPort] = useState(String(initial.port))
  const [security, setSecurity] = useState<MailSecurity>(initial.security)
  const [username, setUsername] = useState(initial.username ?? '')
  const [password, setPassword] = useState('')
  const [clearPassword, setClearPassword] = useState(false)
  const [fromAddress, setFromAddress] = useState(initial.fromAddress ?? '')
  const [fromName, setFromName] = useState(initial.fromName ?? '')
  const [testTo, setTestTo] = useState('')
  const [errors, setErrors] = useState<Partial<Record<Field, string>>>({})
  const [testResult, setTestResult] = useState<MailTestResult>()

  const body = (): MailServerRequest => ({
    host: host.trim(),
    port: Number(port),
    security,
    username: username.trim(),
    password,
    clearPassword,
    fromAddress: fromAddress.trim(),
    fromName: fromName.trim(),
  })

  /** The server fields are needed only when a host is entered: an empty host falls back to the configuration file. */
  function validate(includeRecipient: boolean): boolean {
    const next: Partial<Record<Field, string>> = {}
    const portValue = Number(port)

    if (includeRecipient && host.trim() === '') next.host = t('settings.email.hostRequired')
    if (host.trim() !== '' && /\s|:\/\//.test(host.trim())) next.host = t('settings.email.hostInvalid')
    if (host.trim() !== '' && (!Number.isInteger(portValue) || portValue < 1 || portValue > 65535)) next.port = t('settings.email.portInvalid')
    if (host.trim() !== '' && !EMAIL_PATTERN.test(fromAddress.trim())) next.fromAddress = t('settings.email.fromInvalid')
    if (includeRecipient && !EMAIL_PATTERN.test(testTo.trim())) next.to = t('settings.email.toInvalid')

    setErrors(next)

    return Object.keys(next).length === 0
  }

  const save = useMutation({
    mutationFn: () => settingsApi.updateEmail(body()),
    meta: { handlesErrors: true },
    onSuccess: async (saved) => {
      notify.success(t('settings.saved'))
      queryClient.setQueryData(EMAIL_SETTINGS_KEY, saved)
      // Registration settings say whether email can be sent.
      await queryClient.invalidateQueries({ queryKey: ['admin-settings-registration'] })
    },
    onError: (failure) => reportFormError(failure, t('settings.saveFailed')),
  })

  const test = useMutation({
    mutationFn: () => settingsApi.testEmail({ ...body(), to: testTo.trim() }),
    meta: { handlesErrors: true },
    onMutate: () => setTestResult(undefined),
    onSuccess: (result) => {
      setTestResult(result)
      if (result.delivered) {
        notify.success(t('settings.email.testSent', { to: testTo.trim() }))
      }
    },
    onError: (failure) => reportFormError(failure, t('settings.email.testFailed')),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    if (validate(false)) save.mutate()
  }

  function sendTest() {
    if (validate(true)) test.mutate()
  }

  const server = serverFieldErrors(save.error)

  return (
    <Section title={t('settings.email.title')} description={t('settings.email.description')}>
      <form onSubmit={submit} noValidate className="grid max-w-2xl items-start gap-6">
        <Alert>
          <AlertDescription>{t(`settings.email.source.${initial.source}`)}</AlertDescription>
        </Alert>

        <div className="grid items-start gap-5 sm:grid-cols-[1fr_8rem]">
          <FormField
            label={t('settings.email.host')}
            htmlFor="mail-host"
            hint={t('settings.email.hostHint')}
            error={errors.host ?? server.host?.[0]}
          >
            <Input
              placeholder="smtp.example.com"
              autoComplete="off"
              spellCheck={false}
              value={host}
              onChange={(event) => setHost(event.target.value)}
            />
          </FormField>
          <FormField label={t('settings.email.port')} htmlFor="mail-port" error={errors.port ?? server.port?.[0]}>
            <Input type="number" min={1} max={65535} value={port} onChange={(event) => setPort(event.target.value)} />
          </FormField>
        </div>

        <fieldset className="grid gap-3">
          <legend className="mb-1 text-sm font-medium">{t('settings.email.security')}</legend>
          <RadioGroup
            value={security}
            onValueChange={(value) => setSecurity(value as MailSecurity)}
            className="grid gap-2 sm:grid-cols-2"
          >
            {SECURITY_OPTIONS.map((value) => (
              <div key={value} className="flex items-start gap-3">
                <RadioGroupItem value={value} id={`mail-security-${value}`} className="mt-0.5" />
                <div className="grid gap-0.5">
                  <Label htmlFor={`mail-security-${value}`}>{t(`settings.email.securityOptions.${value}`)}</Label>
                  <p className="text-muted-foreground text-xs">{t(`settings.email.securityOptions.${value}Hint`)}</p>
                </div>
              </div>
            ))}
          </RadioGroup>
        </fieldset>

        <div className="grid items-start gap-5 sm:grid-cols-2">
          <FormField label={t('settings.email.username')} htmlFor="mail-username" hint={t('settings.email.usernameHint')}>
            <Input autoComplete="off" spellCheck={false} value={username} onChange={(event) => setUsername(event.target.value)} />
          </FormField>
          <FormField
            label={t('settings.email.password')}
            htmlFor="mail-password"
            hint={initial.hasPassword ? t('settings.email.passwordStored') : t('settings.email.passwordHint')}
          >
            <Input
              type="password"
              autoComplete="new-password"
              placeholder={initial.hasPassword ? '••••••••' : undefined}
              disabled={clearPassword}
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />
          </FormField>
        </div>

        {initial.hasPassword && (
          <div className="flex items-center gap-2">
            <Checkbox
              id="mail-clear-password"
              checked={clearPassword}
              onCheckedChange={(checked) => {
                setClearPassword(checked === true)
                setPassword('')
              }}
            />
            <Label htmlFor="mail-clear-password" className="font-normal">
              {t('settings.email.clearPassword')}
            </Label>
          </div>
        )}

        <div className="grid items-start gap-5 sm:grid-cols-2">
          <FormField
            label={t('settings.email.fromAddress')}
            htmlFor="mail-from"
            hint={t('settings.email.fromAddressHint')}
            error={errors.fromAddress ?? server.fromAddress?.[0]}
          >
            <Input
              type="email"
              placeholder="portal@example.com"
              value={fromAddress}
              onChange={(event) => setFromAddress(event.target.value)}
            />
          </FormField>
          <FormField label={t('settings.email.fromName')} htmlFor="mail-from-name" hint={t('settings.email.fromNameHint')}>
            <Input placeholder="OpenPortal" value={fromName} onChange={(event) => setFromName(event.target.value)} />
          </FormField>
        </div>

        <div>
          <Button type="submit" disabled={save.isPending}>
            {save.isPending && <Loader2 className="animate-spin" />}
            {save.isPending ? t('common.saving') : t('common.save')}
          </Button>
        </div>

        <div className="grid gap-4 rounded-lg border p-4">
          <div className="grid gap-1">
            <p className="text-sm font-medium">{t('settings.email.testTitle')}</p>
            <p className="text-muted-foreground text-sm">{t('settings.email.testDescription')}</p>
          </div>
          <div className="flex flex-col items-start gap-3 sm:flex-row">
            <FormField label={t('settings.email.testTo')} htmlFor="mail-test-to" error={errors.to} className="w-full sm:max-w-sm">
              <Input type="email" placeholder="you@example.com" value={testTo} onChange={(event) => setTestTo(event.target.value)} />
            </FormField>
            <Button variant="outline" className="sm:mt-6" onClick={sendTest} disabled={test.isPending}>
              {test.isPending ? <Loader2 className="animate-spin" /> : <Send />}
              {t('settings.email.testSend')}
            </Button>
          </div>

          {testResult &&
            (testResult.delivered ? (
              <Alert>
                <CheckCircle2 className="text-success" />
                <AlertTitle>{t('settings.email.testOk')}</AlertTitle>
                <AlertDescription>{t('settings.email.testOkHint')}</AlertDescription>
              </Alert>
            ) : (
              <Alert variant="destructive">
                <XCircle />
                <AlertTitle>{t('settings.email.testKo')}</AlertTitle>
                <AlertDescription className="font-mono text-xs break-all">{testResult.error}</AlertDescription>
              </Alert>
            ))}
        </div>
      </form>
    </Section>
  )
}

const STATUS_VARIANT = { pending: 'warning', delivered: 'success', failed: 'destructive' } as const

/** What waits to be delivered (emails, calls to applications), what failed and why, with a retry. */
function OutboxPanel() {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<OutboxStatus | ''>('')
  const [search, setSearch] = useState('')
  const queue = useQuery({
    queryKey: [...OUTBOX_KEY, status],
    queryFn: ({ signal }) => outboxApi.list(status, signal),
    refetchInterval: 15_000,
  })

  const refresh = () => queryClient.invalidateQueries({ queryKey: OUTBOX_KEY })

  const retry = useMutation({
    mutationFn: (id: string) => outboxApi.retry(id),
    onSuccess: async () => {
      notify.success(t('outbox.retried'))
      await refresh()
    },
  })

  const retryFailed = useMutation({
    mutationFn: () => outboxApi.retryFailed(),
    onSuccess: async (result) => {
      notify.success(t('outbox.retriedMany', { count: result.count }))
      await refresh()
    },
  })

  const remove = useMutation({
    mutationFn: (id: string) => outboxApi.remove(id),
    onSuccess: async () => {
      notify.success(t('outbox.deleted'))
      await refresh()
    },
  })

  const summary = queue.data?.summary
  const items = (queue.data?.items ?? []).filter((message) => matchesSearch(search, message.description, message.type, message.lastError))

  return (
    <Section
      title={t('outbox.title')}
      description={t('outbox.description')}
      actions={
        summary && summary.failed > 0 ? (
          <Button size="sm" variant="outline" onClick={() => retryFailed.mutate()} disabled={retryFailed.isPending}>
            <RotateCcw />
            {t('outbox.retryFailed')}
          </Button>
        ) : undefined
      }
    >
      <div className="space-y-4">
        {summary && (
          <div className="flex flex-wrap gap-2 text-sm">
            <Badge variant="warning">{t('outbox.summary.pending', { count: summary.pending })}</Badge>
            <Badge variant={summary.failed > 0 ? 'destructive' : 'secondary'}>{t('outbox.summary.failed', { count: summary.failed })}</Badge>
            <Badge variant="success">{t('outbox.summary.delivered', { count: summary.deliveredLastDay })}</Badge>
            {summary.oldestPendingAtUtc && (
              <span className="text-muted-foreground">
                {t('outbox.summary.oldest', { date: formatDate(summary.oldestPendingAtUtc, { dateStyle: 'short', timeStyle: 'short' }) })}
              </span>
            )}
          </div>
        )}

        <TableToolbar
          search={search}
          onSearchChange={setSearch}
          filtersActive={search !== '' || status !== ''}
          onReset={() => {
            setSearch('')
            setStatus('')
          }}
          resultCount={queue.data ? items.length : undefined}
        >
          <FilterSelect
            label={t('outbox.status')}
            value={status}
            onChange={(value) => setStatus(value as OutboxStatus | '')}
            options={(['pending', 'failed', 'delivered'] as const).map((value) => ({ value, label: t(`outbox.statuses.${value}`) }))}
          />
        </TableToolbar>

        {queue.isPending && <LoadingState />}
        {queue.isError && (
          <ErrorPanel message={describeError(queue.error)} traceId={traceIdOf(queue.error)} onRetry={() => void queue.refetch()} />
        )}
        {queue.data && items.length === 0 && (
          <EmptyState title={t('outbox.empty')} description={t('outbox.emptyDescription')} />
        )}
        {queue.data && items.length > 0 && (
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('outbox.columns.message')}</TableHead>
                  <TableHead>{t('outbox.status')}</TableHead>
                  <TableHead className="text-right">{t('outbox.columns.attempts')}</TableHead>
                  <TableHead>{t('outbox.columns.created')}</TableHead>
                  <TableHead>{t('outbox.columns.error')}</TableHead>
                  <TableHead className="text-right">{t('common.actions')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((message) => (
                  <TableRow key={message.id}>
                    <TableCell className="max-w-72">
                      <div className="truncate font-medium" title={message.description ?? undefined}>
                        {message.description ?? message.type}
                      </div>
                      <div className="text-muted-foreground text-xs">{t(`outbox.types.${message.type}`)}</div>
                    </TableCell>
                    <TableCell>
                      <Badge variant={STATUS_VARIANT[message.status]}>{t(`outbox.statuses.${message.status}`)}</Badge>
                      {message.status === 'pending' && message.attempts > 0 && (
                        <div className="text-muted-foreground mt-1 text-xs">
                          {t('outbox.nextAttempt', { date: formatDate(message.availableAtUtc, { timeStyle: 'short' }) })}
                        </div>
                      )}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">{message.attempts}</TableCell>
                    <TableCell className="whitespace-nowrap">
                      {formatDate(message.createdAtUtc, { dateStyle: 'short', timeStyle: 'short' })}
                    </TableCell>
                    <TableCell className="max-w-80">
                      {message.lastError && (
                        <span className="text-destructive line-clamp-2 font-mono text-xs break-all" title={message.lastError}>
                          {message.lastError}
                        </span>
                      )}
                    </TableCell>
                    <TableCell className="text-right whitespace-nowrap">
                      {message.status !== 'delivered' && (
                        <Button size="sm" variant="ghost" onClick={() => retry.mutate(message.id)} disabled={retry.isPending}>
                          <RotateCcw />
                          {t('outbox.retry')}
                        </Button>
                      )}
                      <Button
                        size="sm"
                        variant="ghost"
                        aria-label={t('common.delete')}
                        onClick={() => remove.mutate(message.id)}
                        disabled={remove.isPending}
                      >
                        <Trash2 />
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </div>
    </Section>
  )
}
