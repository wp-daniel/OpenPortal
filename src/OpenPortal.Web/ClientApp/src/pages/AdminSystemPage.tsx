import { useMutation, useQuery } from '@tanstack/react-query'
import { Activity, CheckCircle2, Clock, Cpu, KeyRound, Loader2, Play, RefreshCw, XCircle } from 'lucide-react'
import { api } from '@/api/client'
import { PageHeader } from '@/components/PageHeader'
import { Section } from '@/components/Section'
import { ErrorPanel, LoadingState } from '@/components/StatePanels'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { notify } from '@/hooks/useToast'
import { formatDate, formatRelative } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'

interface SystemCheck {
  readonly name: string
  readonly status: 'Healthy' | 'Degraded' | 'Unhealthy'
  readonly description: string | null
  readonly durationMs: number
}

interface SystemJob {
  readonly name: string
  readonly intervalMinutes: number
  readonly lastStartedAtUtc: string | null
  readonly lastDurationMs: number | null
  readonly lastSucceeded: boolean | null
  readonly lastResult: number | null
  readonly lastError: string | null
  readonly runs: number
  readonly failures: number
}

interface SystemStatus {
  readonly version: string
  readonly environment: string
  readonly runtime: string
  readonly machineName: string
  readonly startedAtUtc: string
  readonly databaseProvider: string
  readonly healthStatus: SystemCheck['status']
  readonly checks: readonly SystemCheck[]
  readonly jobs: readonly SystemJob[]
  readonly outbox: { readonly pending: number; readonly failed: number; readonly deliveredLastDay: number; readonly oldestPendingAtUtc: string | null }
  readonly features: { readonly email: boolean; readonly redis: boolean; readonly externalProviders: number; readonly telemetry: boolean }
  readonly signingKeys: readonly { readonly thumbprint: string; readonly notBeforeUtc: string; readonly notAfterUtc: string; readonly inUse: boolean }[]
}

const SYSTEM_KEY = ['admin-system'] as const

function StatusBadge({ status }: { status: SystemCheck['status'] }) {
  const { t } = useI18n()
  const variant = status === 'Healthy' ? 'success' : status === 'Degraded' ? 'warning' : 'destructive'

  return <Badge variant={variant}>{t(`system.status.${status}`)}</Badge>
}

/**
 * This instance at a glance (administrators only): version and uptime, the readiness checks, the background jobs with
 * their last run (and running one now), the delivery queue, the optional parts of the deployment, and the token
 * signing keys with their expiry.
 */
export function AdminSystemPage() {
  const { t } = useI18n()
  const status = useQuery({ queryKey: SYSTEM_KEY, queryFn: ({ signal }) => api.get<SystemStatus>('/api/admin/system', signal) })

  const run = useMutation({
    mutationFn: (name: string) =>
      api.post<{ succeeded: boolean; result: number | null; error: string | null }>(`/api/admin/system/jobs/${encodeURIComponent(name)}/run`),
    onSuccess: async (result, name) => {
      if (result.succeeded) {
        notify.success(t('system.jobs.ran', { name: t(`system.job.${name}`) }), t('system.jobs.result', { count: result.result ?? 0 }))
      } else {
        notify.error(t('system.jobs.failed', { name: t(`system.job.${name}`) }), result.error ?? undefined)
      }

      await status.refetch()
    },
  })

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('system.title')}
        description={t('system.description')}
        actions={
          <Button variant="outline" onClick={() => void status.refetch()} disabled={status.isFetching}>
            {status.isFetching ? <Loader2 className="animate-spin" /> : <RefreshCw />}
            {t('system.refresh')}
          </Button>
        }
      />

      {status.isPending && <LoadingState />}
      {status.isError && (
        <ErrorPanel message={describeError(status.error)} traceId={traceIdOf(status.error)} onRetry={() => void status.refetch()} />
      )}

      {status.data && (
        <>
          <Section
            title={
              <span className="flex items-center gap-2">
                <Cpu className="size-5" />
                {t('system.instance')}
                <StatusBadge status={status.data.healthStatus} />
              </span>
            }
          >
            <dl className="grid gap-x-6 gap-y-3 text-sm sm:grid-cols-2 lg:grid-cols-3">
              {[
                [t('system.version'), status.data.version],
                [t('system.environment'), status.data.environment],
                [t('system.runtime'), status.data.runtime],
                [t('system.machine'), status.data.machineName],
                [t('system.database'), status.data.databaseProvider],
                [t('system.started'), `${formatDate(status.data.startedAtUtc, { dateStyle: 'medium', timeStyle: 'short' })} (${formatRelative(status.data.startedAtUtc)})`],
              ].map(([label, value]) => (
                <div key={label} className="grid gap-0.5">
                  <dt className="text-muted-foreground text-xs">{label}</dt>
                  <dd className="font-medium break-words">{value}</dd>
                </div>
              ))}
            </dl>
            <div className="mt-4 flex flex-wrap gap-2">
              {(['email', 'redis', 'telemetry'] as const).map((feature) => (
                <Badge key={feature} variant={status.data.features[feature] ? 'success' : 'secondary'}>
                  {t(`system.feature.${feature}`)}: {status.data.features[feature] ? t('system.on') : t('system.off')}
                </Badge>
              ))}
              <Badge variant="secondary">{t('system.feature.providers', { count: status.data.features.externalProviders })}</Badge>
            </div>
          </Section>

          <Section
            title={
              <span className="flex items-center gap-2">
                <Activity className="size-5" />
                {t('system.checks')}
              </span>
            }
          >
            <ul className="divide-y rounded-lg border">
              {status.data.checks.map((check) => (
                <li key={check.name} className="flex flex-wrap items-center gap-3 p-3 text-sm">
                  <span className="font-mono">{check.name}</span>
                  <StatusBadge status={check.status} />
                  {check.description && <span className="text-muted-foreground text-xs">{check.description}</span>}
                  <span className="text-muted-foreground ml-auto text-xs tabular-nums">{Math.round(check.durationMs)} ms</span>
                </li>
              ))}
            </ul>
          </Section>

          <Section
            title={
              <span className="flex items-center gap-2">
                <Clock className="size-5" />
                {t('system.jobs.title')}
              </span>
            }
            description={t('system.jobs.description')}
          >
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t('system.jobs.name')}</TableHead>
                    <TableHead>{t('system.jobs.every')}</TableHead>
                    <TableHead>{t('system.jobs.last')}</TableHead>
                    <TableHead>{t('system.jobs.outcome')}</TableHead>
                    <TableHead className="text-right">{t('common.actions')}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {status.data.jobs.map((job) => (
                    <TableRow key={job.name}>
                      <TableCell>
                        <div className="font-medium">{t(`system.job.${job.name}`)}</div>
                        <div className="text-muted-foreground font-mono text-xs">{job.name}</div>
                      </TableCell>
                      <TableCell className="tabular-nums">{t('system.jobs.minutes', { count: Math.round(job.intervalMinutes) })}</TableCell>
                      <TableCell className="text-muted-foreground text-xs">
                        {job.lastStartedAtUtc ? formatRelative(job.lastStartedAtUtc) : t('system.jobs.never')}
                        {job.lastDurationMs !== null && ` · ${Math.round(job.lastDurationMs)} ms`}
                      </TableCell>
                      <TableCell>
                        {job.lastSucceeded === null ? (
                          <span className="text-muted-foreground text-xs">—</span>
                        ) : job.lastSucceeded ? (
                          <span className="flex items-center gap-1 text-xs">
                            <CheckCircle2 className="text-success size-4" />
                            {t('system.jobs.result', { count: job.lastResult ?? 0 })}
                          </span>
                        ) : (
                          <span className="text-destructive flex items-center gap-1 text-xs" title={job.lastError ?? undefined}>
                            <XCircle className="size-4" />
                            {job.lastError}
                          </span>
                        )}
                      </TableCell>
                      <TableCell className="text-right">
                        <Button size="sm" variant="outline" disabled={run.isPending} onClick={() => run.mutate(job.name)}>
                          {run.isPending && run.variables === job.name ? <Loader2 className="animate-spin" /> : <Play />}
                          {t('system.jobs.run')}
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
            <p className="text-muted-foreground mt-3 text-xs">
              {t('system.outbox', {
                pending: status.data.outbox.pending,
                failed: status.data.outbox.failed,
                delivered: status.data.outbox.deliveredLastDay,
              })}
            </p>
          </Section>

          {status.data.signingKeys.length > 0 && (
            <Section
              title={
                <span className="flex items-center gap-2">
                  <KeyRound className="size-5" />
                  {t('system.keys.title')}
                </span>
              }
              description={t('system.keys.description')}
            >
              <ul className="divide-y rounded-lg border">
                {status.data.signingKeys.map((key) => (
                  <li key={key.thumbprint} className="flex flex-wrap items-center gap-3 p-3 text-sm">
                    <span className="font-mono text-xs">{key.thumbprint.slice(0, 16)}…</span>
                    {key.inUse && <Badge variant="success">{t('system.keys.inUse')}</Badge>}
                    <span className="text-muted-foreground ml-auto text-xs">
                      {t('system.keys.valid', {
                        from: formatDate(key.notBeforeUtc, { dateStyle: 'medium' }),
                        to: formatDate(key.notAfterUtc, { dateStyle: 'medium' }),
                      })}
                    </span>
                  </li>
                ))}
              </ul>
            </Section>
          )}
        </>
      )}
    </div>
  )
}
