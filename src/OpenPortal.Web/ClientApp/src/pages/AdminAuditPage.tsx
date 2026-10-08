import { useQuery } from '@tanstack/react-query'
import { Download, History, X } from 'lucide-react'
import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { AUDIT_CATEGORIES, auditApi } from '@/api/audit'
import type { AuditEntry, AuditFilter, AuditSubject } from '@/api/types'
import { PageHeader } from '@/components/PageHeader'
import { Section } from '@/components/Section'
import { ErrorPanel, LoadingState } from '@/components/StatePanels'
import { FilterSelect, TableToolbar } from '@/components/TableToolbar'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Separator } from '@/components/ui/separator'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { Table, TableBody, TableCaption, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { useDebounced } from '@/hooks/useDebounced'
import type { TFunction } from '@/i18n/store'
import { formatDate } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'

const PERIODS = { '24h': 1, '7d': 7, '30d': 30, '90d': 90 } as const

type Period = keyof typeof PERIODS

const PAGE_SIZE = 25

/** The translated name of an action, or its code when this version has no label for it. */
function auditActionLabel(t: TFunction, action: string): string {
  const key = `audit.action.${action}`
  const label = t(key)

  return label === key ? action : label
}

/**
 * The security audit log: who did what, to what, from where and with what outcome. Read-only. A user's own
 * history is reached from the users page (`?subject=<id>`), which narrows the log to entries naming them.
 */
export function AdminAuditPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const subject = searchParams.get('subject') ?? ''

  // Keyed by the subject, so following a link to someone else's history starts from page one and no filters.
  return (
    <AuditLog
      key={subject}
      subject={subject}
      subjectLabel={searchParams.get('label') ?? subject}
      onClearSubject={() => setSearchParams({})}
    />
  )
}

function AuditLog({
  subject,
  subjectLabel,
  onClearSubject,
}: {
  subject: string
  subjectLabel: string
  onClearSubject: () => void
}) {
  const { t } = useI18n()
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [category, setCategory] = useState('')
  const [outcome, setOutcome] = useState('')
  const [period, setPeriod] = useState<Period | ''>('')
  // The lower bound is fixed when the period is chosen, so paging does not shift under the reader.
  const [from, setFrom] = useState<string | undefined>(undefined)
  const [selected, setSelected] = useState<AuditEntry | null>(null)
  const appliedSearch = useDebounced(search.trim())

  const filter: AuditFilter = {
    search: appliedSearch,
    category,
    outcome,
    subject,
    from,
  }

  const entries = useQuery({
    queryKey: ['admin-audit', page, appliedSearch, category, outcome, subject, from],
    queryFn: ({ signal }) => auditApi.list({ ...filter, page, pageSize: PAGE_SIZE }, signal),
    placeholderData: (previous) => previous,
  })

  const filtersActive = search !== '' || category !== '' || outcome !== '' || period !== ''

  function changeFilter(apply: () => void) {
    apply()
    setPage(1)
  }

  function choosePeriod(value: Period | '') {
    setPeriod(value)
    setFrom(value === '' ? undefined : new Date(Date.now() - PERIODS[value] * 24 * 60 * 60 * 1000).toISOString())
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('audit.title')}
        description={t('audit.description')}
        actions={
          <Button variant="outline" asChild>
            <a href={auditApi.exportUrl(filter)} download>
              <Download />
              {t('audit.export')}
            </a>
          </Button>
        }
      />

      <Section title={t('audit.entries')}>
        {subject !== '' && (
          <div className="mb-4 flex flex-wrap items-center gap-2 text-sm">
            <History className="text-muted-foreground size-4" />
            <span>{t('audit.subjectFilter', { name: subjectLabel })}</span>
            <Button variant="ghost" size="sm" onClick={onClearSubject}>
              <X />
              {t('audit.subjectClear')}
            </Button>
          </div>
        )}

        <TableToolbar
          search={search}
          onSearchChange={(value) => changeFilter(() => setSearch(value))}
          searchPlaceholder={t('audit.searchPlaceholder')}
          filtersActive={filtersActive}
          onReset={() =>
            changeFilter(() => {
              setSearch('')
              setCategory('')
              setOutcome('')
              choosePeriod('')
            })
          }
          resultCount={entries.data?.totalCount}
        >
          <FilterSelect
            label={t('audit.category.label')}
            value={category}
            onChange={(value) => changeFilter(() => setCategory(value))}
            options={AUDIT_CATEGORIES.map((entry) => ({ value: entry, label: t(`audit.category.${entry}`) }))}
          />
          <FilterSelect
            label={t('audit.outcome.label')}
            value={outcome}
            onChange={(value) => changeFilter(() => setOutcome(value))}
            options={[
              { value: 'success', label: t('audit.outcome.success') },
              { value: 'failure', label: t('audit.outcome.failure') },
            ]}
          />
          <FilterSelect
            label={t('audit.period.label')}
            value={period}
            onChange={(value) => changeFilter(() => choosePeriod(value as Period | ''))}
            options={(Object.keys(PERIODS) as Period[]).map((entry) => ({ value: entry, label: t(`audit.period.${entry}`) }))}
          />
        </TableToolbar>

        {entries.isError && (
          <ErrorPanel
            message={describeError(entries.error)}
            traceId={traceIdOf(entries.error)}
            onRetry={() => void entries.refetch()}
          />
        )}

        {entries.isPending && <LoadingState />}

        {entries.data && entries.data.items.length === 0 && (
          <p className="text-muted-foreground py-6 text-center text-sm">{t('audit.noMatch')}</p>
        )}

        {entries.data && entries.data.items.length > 0 && (
          <>
            <div className="overflow-x-auto">
              <Table>
                <TableCaption className="sr-only">{t('audit.caption')}</TableCaption>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t('audit.column.when')}</TableHead>
                    <TableHead>{t('audit.column.actor')}</TableHead>
                    <TableHead>{t('audit.column.action')}</TableHead>
                    <TableHead>{t('audit.column.target')}</TableHead>
                    <TableHead>{t('audit.column.address')}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {entries.data.items.map((entry) => (
                    <TableRow
                      key={entry.id}
                      className="cursor-pointer"
                      tabIndex={0}
                      onClick={() => setSelected(entry)}
                      onKeyDown={(event) => {
                        if (event.key === 'Enter' || event.key === ' ') {
                          event.preventDefault()
                          setSelected(entry)
                        }
                      }}
                    >
                      <TableCell className="whitespace-nowrap">
                        {formatDate(entry.occurredAtUtc, { dateStyle: 'short', timeStyle: 'medium' })}
                      </TableCell>
                      <TableCell>
                        <SubjectText subject={entry.actor} fallback={t('audit.anonymous')} />
                      </TableCell>
                      <TableCell>
                        <div className="flex flex-wrap items-center gap-2">
                          <span>{auditActionLabel(t, entry.action)}</span>
                          {entry.outcome === 'failure' && <Badge variant="destructive">{t('audit.outcome.failure')}</Badge>}
                        </div>
                      </TableCell>
                      <TableCell>
                        <SubjectText subject={entry.target} fallback="—" />
                      </TableCell>
                      <TableCell className="text-muted-foreground font-mono text-xs">{entry.ipAddress ?? '—'}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>

            <nav aria-label={t('audit.pagination')} className="mt-4 flex items-center justify-between gap-2 text-sm">
              <Button variant="outline" size="sm" disabled={!entries.data.hasPrevious} onClick={() => setPage(page - 1)}>
                {t('common.previous')}
              </Button>
              <span className="text-muted-foreground text-center">
                {t('audit.pageOf', {
                  page: entries.data.page,
                  total: entries.data.totalPages,
                  count: entries.data.totalCount,
                })}
              </span>
              <Button variant="outline" size="sm" disabled={!entries.data.hasNext} onClick={() => setPage(page + 1)}>
                {t('common.next')}
              </Button>
            </nav>
          </>
        )}
      </Section>

      <AuditEntrySheet entry={selected} onClose={() => setSelected(null)} />
    </div>
  )
}

function SubjectText({ subject, fallback }: { subject: AuditSubject | null; fallback: string }) {
  const { t } = useI18n()

  if (!subject) {
    return <span className="text-muted-foreground">{fallback}</span>
  }

  return (
    <div className="min-w-0">
      <div className="truncate">{subject.label ?? subject.id}</div>
      <div className="text-muted-foreground text-xs">{t(`audit.subject.${subject.type}`)}</div>
    </div>
  )
}

/** Everything recorded about one entry, including the request it came from. */
function AuditEntrySheet({ entry, onClose }: { entry: AuditEntry | null; onClose: () => void }) {
  const { t } = useI18n()

  const rows: [label: string, value: string | null][] = entry
    ? [
        [t('audit.column.when'), formatDate(entry.occurredAtUtc, { dateStyle: 'full', timeStyle: 'long' })],
        [t('audit.field.code'), entry.action],
        [t('audit.column.address'), entry.ipAddress],
        [t('audit.field.userAgent'), entry.userAgent],
        [t('audit.field.correlationId'), entry.correlationId],
      ]
    : []

  const details = entry ? Object.entries(entry.details) : []

  return (
    <Sheet open={entry !== null} onOpenChange={(open) => !open && onClose()}>
      <SheetContent className="w-full gap-0 sm:max-w-md">
        <SheetHeader>
          <SheetTitle>{entry ? auditActionLabel(t, entry.action) : ''}</SheetTitle>
          <SheetDescription>
            {entry && (
              <Badge variant={entry.outcome === 'success' ? 'success' : 'destructive'}>{t(`audit.outcome.${entry.outcome}`)}</Badge>
            )}
          </SheetDescription>
        </SheetHeader>

        <ScrollArea className="min-h-0 flex-1">
          {entry && (
            <div className="grid gap-6 px-4 pb-6 text-sm">
              <SubjectBlock title={t('audit.column.actor')} subject={entry.actor} fallback={t('audit.anonymous')} onNavigate={onClose} />
              <SubjectBlock title={t('audit.column.target')} subject={entry.target} fallback="—" onNavigate={onClose} />

              {details.length > 0 && (
                <section className="grid gap-2">
                  <h3 className="font-medium">{t('audit.field.details')}</h3>
                  <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1">
                    {details.map(([key, value]) => (
                      <div key={key} className="contents">
                        <dt className="text-muted-foreground">{key}</dt>
                        <dd className="break-words">{value ?? '—'}</dd>
                      </div>
                    ))}
                  </dl>
                </section>
              )}

              <Separator />

              <dl className="grid gap-3">
                {rows.map(([label, value]) => (
                  <div key={label} className="grid gap-0.5">
                    <dt className="text-muted-foreground text-xs">{label}</dt>
                    <dd className="font-mono text-xs break-all">{value ?? '—'}</dd>
                  </div>
                ))}
              </dl>
            </div>
          )}
        </ScrollArea>
      </SheetContent>
    </Sheet>
  )
}

function SubjectBlock({
  title,
  subject,
  fallback,
  onNavigate,
}: {
  title: string
  subject: AuditSubject | null
  fallback: string
  onNavigate: () => void
}) {
  const { t } = useI18n()

  return (
    <section className="grid gap-1">
      <h3 className="text-muted-foreground text-xs">{title}</h3>
      {subject ? (
        <>
          <div className="font-medium break-all">{subject.label ?? subject.id}</div>
          <div className="text-muted-foreground text-xs">
            {t(`audit.subject.${subject.type}`)}
            {subject.id && subject.label && <span className="font-mono"> · {subject.id}</span>}
          </div>
          {subject.type === 'user' && subject.id && (
            <Link
              className="text-xs underline underline-offset-4"
              to={`/admin/audit?subject=${encodeURIComponent(subject.id)}&label=${encodeURIComponent(subject.label ?? subject.id)}`}
              onClick={onNavigate}
            >
              {t('audit.subjectHistory')}
            </Link>
          )}
        </>
      ) : (
        <div className="text-muted-foreground">{fallback}</div>
      )}
    </section>
  )
}
