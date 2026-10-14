import {
  AlertTriangle,
  AppWindow,
  ArrowDownRight,
  ArrowRight,
  ArrowUpRight,
  KeyRound,
  LogIn,
  ShieldAlert,
  UserCheck,
  UserPlus,
  Users,
  type LucideIcon,
} from 'lucide-react'
import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Area, AreaChart, CartesianGrid, XAxis, YAxis } from 'recharts'
import type { ActivityTotals, CountedValue, PortalStatistics } from '@/api/types'
import { Section } from '@/components/Section'
import { ErrorPanel } from '@/components/StatePanels'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from '@/components/ui/chart'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { formatDate, formatNumber, formatRelative } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'
import { STATISTICS_PERIODS, useStatistics, type StatisticsPeriod } from '@/lib/statistics'
import { cn } from '@/lib/utils'


/** 7 / 30 / 90 days, as line tabs. */
export function PeriodPicker({ value, onChange }: { value: StatisticsPeriod; onChange: (days: StatisticsPeriod) => void }) {
  const { t } = useI18n()

  return (
    <Tabs value={String(value)} onValueChange={(next) => onChange(Number(next) as StatisticsPeriod)}>
      <TabsList>
        {STATISTICS_PERIODS.map((days) => (
          <TabsTrigger key={days} value={String(days)}>
            {t('statistics.days', { count: days })}
          </TabsTrigger>
        ))}
      </TabsList>
    </Tabs>
  )
}

/**
 * The administrators' overview: key figures against the previous period, sign-ins per day, and what needs
 * attention. `compact` (the dashboard) stops there; the statistics page adds the rankings below it.
 */
export function StatisticsOverview({ days, compact = false }: { days: StatisticsPeriod; compact?: boolean }) {
  const { t } = useI18n()
  const statistics = useStatistics(days)

  if (statistics.isError) {
    return (
      <ErrorPanel
        message={describeError(statistics.error)}
        traceId={traceIdOf(statistics.error)}
        onRetry={() => void statistics.refetch()}
      />
    )
  }

  if (!statistics.data) {
    return <OverviewSkeleton />
  }

  const data = statistics.data

  return (
    <div className={cn('grid gap-4 transition-opacity', statistics.isFetching && 'opacity-70')}>
      {!data.activity.isComplete && (
        <Alert>
          <AlertTriangle />
          <AlertDescription>{t('statistics.incomplete')}</AlertDescription>
        </Alert>
      )}

      <KeyFigures statistics={data} />

      <div className="grid gap-4 lg:grid-cols-3">
        <Section
          className="lg:col-span-2"
          title={t('statistics.activity.title')}
          description={t('statistics.activity.description', {
            from: formatDate(`${data.activity.from}T12:00:00`, { dateStyle: 'medium' }),
            to: formatDate(`${data.activity.to}T12:00:00`, { dateStyle: 'medium' }),
          })}
        >
          <ActivityChart statistics={data} />
        </Section>

        <NeedsAttention statistics={data} />
      </div>

      {!compact && (
        <div className="grid gap-4 lg:grid-cols-2">
          <Section title={t('statistics.topApplications.title')} description={t('statistics.topApplications.description')}>
            <Ranking
              empty={t('statistics.topApplications.empty')}
              items={data.activity.topApplications.map((usage) => ({
                key: usage.clientId,
                label: usage.name ?? usage.clientId,
                detail: t('statistics.topApplications.users', { count: usage.users }),
                value: usage.signIns,
              }))}
            />
          </Section>

          <Section title={t('statistics.failures.title')} description={t('statistics.failures.description')}>
            <Ranking
              empty={t('statistics.failures.empty')}
              items={data.activity.failureReasons.map((reason) => ({
                key: reason.value,
                label: t(`statistics.reason.${reason.value}`),
                detail: formatRelative(reason.lastAtUtc),
                value: reason.count,
              }))}
              tone="destructive"
            />
          </Section>

          <Section title={t('statistics.failedAccounts.title')} description={t('statistics.failedAccounts.description')}>
            <FailureList items={data.activity.topFailedAccounts} empty={t('statistics.failures.empty')} mono />
          </Section>

          <Section title={t('statistics.failedAddresses.title')} description={t('statistics.failedAddresses.description')}>
            <FailureList items={data.activity.topFailedAddresses} empty={t('statistics.failures.empty')} mono />
          </Section>
        </div>
      )}
    </div>
  )
}

function OverviewSkeleton() {
  return (
    <div className="grid gap-4" role="status" aria-busy="true">
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {[0, 1, 2, 3].map((index) => (
          <Skeleton key={index} className="h-28 rounded-xl" />
        ))}
      </div>
      <Skeleton className="h-80 rounded-xl" />
    </div>
  )
}

// ---------------------------------------------------------------------------
// Key figures.
// ---------------------------------------------------------------------------

function KeyFigures({ statistics }: { statistics: PortalStatistics }) {
  const { t } = useI18n()
  const { current, previous } = statistics.activity

  const figure = (key: keyof ActivityTotals) => ({ value: current[key], previous: previous[key] })

  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      <KeyFigure icon={LogIn} label={t('statistics.kpi.signIns')} {...figure('signIns')} />
      <KeyFigure icon={UserCheck} label={t('statistics.kpi.activeUsers')} {...figure('activeUsers')} />
      <KeyFigure icon={AppWindow} label={t('statistics.kpi.applicationSignIns')} {...figure('applicationSignIns')} />
      <KeyFigure icon={ShieldAlert} label={t('statistics.kpi.failedSignIns')} {...figure('failedSignIns')} higherIsWorse />
    </div>
  )
}

/** One figure with its change against the previous period of the same length. */
function KeyFigure({
  icon: Icon,
  label,
  value,
  previous,
  higherIsWorse = false,
}: {
  icon: LucideIcon
  label: string
  value: number
  previous: number
  higherIsWorse?: boolean
}) {
  const { t } = useI18n()
  const change = previous === 0 ? null : (value - previous) / previous
  const up = value > previous
  const good = value === previous ? null : up !== higherIsWorse

  return (
    <Card className="gap-2 py-5">
      <CardContent className="grid gap-2">
        <div className="text-muted-foreground flex items-center justify-between text-sm">
          <span>{label}</span>
          <Icon className="size-4" />
        </div>
        <div className="text-3xl font-semibold tracking-tight tabular-nums">{formatNumber(value)}</div>
        <div className="flex items-center gap-1 text-xs">
          {value !== previous && (
            <span className={cn('flex items-center font-medium', good ? 'text-success' : 'text-destructive')}>
              {up ? <ArrowUpRight className="size-3.5" /> : <ArrowDownRight className="size-3.5" />}
              {change === null
                ? t('statistics.kpi.new')
                : formatNumber(Math.abs(change), { style: 'percent', maximumFractionDigits: 0 })}
            </span>
          )}
          <span className="text-muted-foreground">{t('statistics.kpi.previous', { value: formatNumber(previous) })}</span>
        </div>
      </CardContent>
    </Card>
  )
}

// ---------------------------------------------------------------------------
// Activity per day.
// ---------------------------------------------------------------------------

function ActivityChart({ statistics }: { statistics: PortalStatistics }) {
  const { t } = useI18n()

  const config = {
    signIns: { label: t('statistics.series.signIns'), color: 'var(--chart-2)' },
    applicationSignIns: { label: t('statistics.series.applicationSignIns'), color: 'var(--chart-1)' },
    failedSignIns: { label: t('statistics.series.failedSignIns'), color: 'var(--destructive)' },
  } satisfies ChartConfig

  const days = statistics.activity.days
  const long = days.length > 31

  if (days.every((day) => day.signIns + day.applicationSignIns + day.failedSignIns === 0)) {
    return <p className="text-muted-foreground py-16 text-center text-sm">{t('statistics.activity.empty')}</p>
  }

  return (
    <ChartContainer config={config} className="aspect-auto h-72 w-full">
      <AreaChart data={[...days]} margin={{ left: 0, right: 8, top: 8 }}>
        <defs>
          {Object.keys(config).map((key) => (
            <linearGradient key={key} id={`fill-${key}`} x1="0" y1="0" x2="0" y2="1">
              <stop offset="5%" stopColor={`var(--color-${key})`} stopOpacity={0.35} />
              <stop offset="95%" stopColor={`var(--color-${key})`} stopOpacity={0.02} />
            </linearGradient>
          ))}
        </defs>
        <CartesianGrid vertical={false} />
        <XAxis
          dataKey="date"
          tickLine={false}
          axisLine={false}
          tickMargin={8}
          minTickGap={long ? 32 : 16}
          tickFormatter={(value: string) => formatDate(`${value}T12:00:00`, { day: 'numeric', month: 'short' })}
        />
        <YAxis allowDecimals={false} tickLine={false} axisLine={false} width={32} />
        <ChartTooltip
          cursor={false}
          content={
            <ChartTooltipContent
              indicator="dot"
              labelFormatter={(value) => formatDate(`${String(value)}T12:00:00`, { dateStyle: 'full' })}
            />
          }
        />
        {(['signIns', 'applicationSignIns', 'failedSignIns'] as const).map((key) => (
          <Area
            key={key}
            dataKey={key}
            type="monotone"
            fill={`url(#fill-${key})`}
            stroke={`var(--color-${key})`}
            strokeWidth={2}
            isAnimationActive={false}
          />
        ))}
        <ChartLegend content={<ChartLegendContent />} />
      </AreaChart>
    </ChartContainer>
  )
}

// ---------------------------------------------------------------------------
// What needs attention.
// ---------------------------------------------------------------------------

function NeedsAttention({ statistics }: { statistics: PortalStatistics }) {
  const { t } = useI18n()
  const { users, access } = statistics

  const items: { icon: LucideIcon; label: string; count: number; to: string }[] = [
    { icon: KeyRound, label: t('statistics.attention.accessRequests'), count: access.pendingAccessRequests, to: '/admin/access-requests' },
    { icon: UserPlus, label: t('statistics.attention.registrations'), count: users.pendingApproval, to: '/admin/users?status=pending' },
    { icon: AppWindow, label: t('statistics.attention.applications'), count: access.pendingApplications, to: '/admin/applications' },
    { icon: ShieldAlert, label: t('statistics.attention.locked'), count: users.locked, to: '/admin/users?status=locked' },
  ]

  const twoFactorShare = users.total === 0 ? 0 : users.twoFactorEnabled / users.total

  return (
    <Section title={t('statistics.attention.title')} description={t('statistics.attention.description')}>
      <ul className="grid gap-1">
        {items.map((item) => (
          <li key={item.to}>
            <Link
              to={item.to}
              className="hover:bg-accent focus-visible:ring-ring/50 flex items-center gap-3 rounded-md px-2 py-2 text-sm outline-none focus-visible:ring-[3px]"
            >
              <item.icon className="text-muted-foreground size-4" />
              <span className="flex-1">{item.label}</span>
              <span
                className={cn(
                  'rounded-full px-2 text-xs font-semibold tabular-nums',
                  item.count > 0 ? 'bg-warning/15 text-warning' : 'bg-muted text-muted-foreground',
                )}
              >
                {formatNumber(item.count)}
              </span>
            </Link>
          </li>
        ))}
      </ul>

      <dl className="mt-4 grid grid-cols-2 gap-3 border-t pt-4 text-sm">
        <Fact icon={Users} label={t('statistics.facts.users')} value={formatNumber(users.total)} />
        <Fact icon={UserPlus} label={t('statistics.facts.newUsers')} value={formatNumber(users.newInPeriod)} />
        <Fact
          icon={ShieldAlert}
          label={t('statistics.facts.twoFactor')}
          value={formatNumber(twoFactorShare, { style: 'percent', maximumFractionDigits: 0 })}
        />
        <Fact icon={AppWindow} label={t('statistics.facts.applications')} value={formatNumber(access.activeApplications)} />
      </dl>
    </Section>
  )
}

function Fact({ icon: Icon, label, value }: { icon: LucideIcon; label: string; value: ReactNode }) {
  return (
    <div className="grid gap-0.5">
      <dt className="text-muted-foreground flex items-center gap-1.5 text-xs">
        <Icon className="size-3.5" />
        {label}
      </dt>
      <dd className="text-lg font-semibold tabular-nums">{value}</dd>
    </div>
  )
}

// ---------------------------------------------------------------------------
// Rankings.
// ---------------------------------------------------------------------------

interface RankedItem {
  readonly key: string
  readonly label: string
  readonly detail?: string
  readonly value: number
}

/** A ranked list with a bar proportional to the first (largest) value. */
function Ranking({ items, empty, tone = 'default' }: { items: readonly RankedItem[]; empty: string; tone?: 'default' | 'destructive' }) {
  if (items.length === 0) {
    return <p className="text-muted-foreground py-6 text-center text-sm">{empty}</p>
  }

  const max = Math.max(...items.map((item) => item.value), 1)

  return (
    <ol className="grid gap-3">
      {items.map((item) => (
        <li key={item.key} className="grid gap-1">
          <div className="flex items-baseline justify-between gap-3 text-sm">
            <span className="truncate font-medium">{item.label}</span>
            <span className="tabular-nums">{formatNumber(item.value)}</span>
          </div>
          <div className="bg-muted h-1.5 overflow-hidden rounded-full">
            <div
              className={cn('h-full rounded-full', tone === 'destructive' ? 'bg-destructive/70' : 'bg-chart-2')}
              style={{ width: `${Math.max(4, (item.value / max) * 100)}%` }}
            />
          </div>
          {item.detail && <span className="text-muted-foreground text-xs">{item.detail}</span>}
        </li>
      ))}
    </ol>
  )
}

/** Accounts or addresses with failed sign-ins: the value, how many, and when last. */
function FailureList({ items, empty, mono = false }: { items: readonly CountedValue[]; empty: string; mono?: boolean }) {
  const { t } = useI18n()

  if (items.length === 0) {
    return <p className="text-muted-foreground py-6 text-center text-sm">{empty}</p>
  }

  return (
    <ul className="divide-y rounded-md border">
      {items.map((item) => (
        <li key={item.value} className="flex items-center gap-3 px-3 py-2 text-sm">
          <span className={cn('min-w-0 flex-1 truncate', mono && 'font-mono text-xs')}>{item.value}</span>
          <span className="text-muted-foreground shrink-0 text-xs">{formatRelative(item.lastAtUtc)}</span>
          <span className="text-destructive shrink-0 font-semibold tabular-nums">
            {t('statistics.attempts', { count: item.count })}
          </span>
        </li>
      ))}
    </ul>
  )
}

/** The dashboard's link to the full statistics page. */
export function StatisticsLink() {
  const { t } = useI18n()

  return (
    <Button asChild variant="ghost" size="sm">
      <Link to="/admin/statistics">
        {t('statistics.more')}
        <ArrowRight />
      </Link>
    </Button>
  )
}
