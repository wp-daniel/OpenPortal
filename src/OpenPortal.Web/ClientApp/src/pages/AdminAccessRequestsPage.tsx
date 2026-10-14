import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, ChevronLeft, ChevronRight, Info, Loader2, X } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { accessRequestsApi, requestKeys } from '@/api/requests'
import type { AccessRequest, AccessRequestStatus } from '@/api/types'
import { RoleBadges, RoleChecklist } from '@/components/access/RoleAssignment'
import { FormField } from '@/components/FormField'
import { PageHeader } from '@/components/PageHeader'
import { Section } from '@/components/Section'
import { EmptyState, ErrorPanel, LoadingState } from '@/components/StatePanels'
import { FilterSelect, TableToolbar } from '@/components/TableToolbar'
import { UserAvatar } from '@/components/UserAvatar'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { Table, TableBody, TableCaption, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Textarea } from '@/components/ui/textarea'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { notify } from '@/hooks/useToast'
import { formatDate, formatRelative } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { useAccessRefresh } from '@/lib/access'
import { describeError, traceIdOf } from '@/lib/errors'
import { reportFormError } from '@/lib/forms'
import { matchesSearch } from '@/lib/tableFilter'

const PAGE_SIZE = 25
const NOTE_MAX_LENGTH = 500
const STATUSES: readonly AccessRequestStatus[] = ['pending', 'approved', 'rejected', 'cancelled']

type Decision = { readonly kind: 'approve' | 'reject'; readonly request: AccessRequest }

/**
 * Access requests for reviewers: the waiting ones first (the longest-waiting at the top), each approved with the
 * roles the reviewer picks, or rejected with a note the requester will read.
 */
export function AdminAccessRequestsPage() {
  const { t } = useI18n()
  const [params, setParams] = useSearchParams()
  const status = params.get('status') ?? 'pending'
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [decision, setDecision] = useState<Decision | null>(null)

  const requests = useQuery({
    queryKey: [...requestKeys.admin, { status, page }],
    queryFn: ({ signal }) => accessRequestsApi.list({ page, pageSize: PAGE_SIZE, status: status || undefined }, signal),
    placeholderData: (previous) => previous,
  })

  const visible = useMemo(
    () =>
      (requests.data?.items ?? []).filter((request) =>
        matchesSearch(search, request.user.displayName, request.user.email, request.application.displayName, request.reason),
      ),
    [requests.data, search],
  )

  const totalPages = requests.data ? Math.max(1, Math.ceil(requests.data.totalCount / PAGE_SIZE)) : 1

  function setStatus(next: string) {
    setPage(1)
    setParams(
      (previous) => {
        const updated = new URLSearchParams(previous)
        updated.set('status', next)
        return updated
      },
      { replace: true },
    )
  }

  return (
    <div className="space-y-6">
      <PageHeader title={t('requests.admin.title')} description={t('requests.admin.description')} />

      <Section
        title={t('requests.admin.listTitle')}
        description={requests.data ? t('requests.admin.pendingCount', { count: requests.data.pendingCount }) : undefined}
      >
        <TableToolbar
          search={search}
          onSearchChange={setSearch}
          searchPlaceholder={t('requests.admin.search')}
          filtersActive={search !== '' || status !== 'pending'}
          onReset={() => {
            setSearch('')
            setStatus('pending')
          }}
          resultCount={requests.data?.totalCount}
        >
          <FilterSelect
            label={t('common.status')}
            value={status}
            onChange={setStatus}
            options={STATUSES.map((value) => ({ value, label: t(`requests.status.${value}`) }))}
          />
        </TableToolbar>

        {requests.isError && (
          <ErrorPanel message={describeError(requests.error)} traceId={traceIdOf(requests.error)} onRetry={() => void requests.refetch()} />
        )}
        {requests.isPending && <LoadingState />}

        {requests.data && visible.length === 0 && (
          <EmptyState
            title={status === 'pending' ? t('requests.admin.emptyPending.title') : t('requests.admin.empty.title')}
            description={status === 'pending' ? t('requests.admin.emptyPending.description') : t('requests.admin.empty.description')}
          />
        )}

        {visible.length > 0 && (
          <div className="overflow-x-auto">
            <Table>
              <TableCaption className="sr-only">{t('requests.admin.listTitle')}</TableCaption>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('requests.column.user')}</TableHead>
                  <TableHead>{t('requests.column.application')}</TableHead>
                  <TableHead>{t('requests.column.reason')}</TableHead>
                  <TableHead>{t('requests.column.when')}</TableHead>
                  <TableHead>{t('common.status')}</TableHead>
                  <TableHead className="text-right">{t('common.actions')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {visible.map((request) => (
                  <TableRow key={request.id}>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        <UserAvatar user={{ ...request.user, avatarUpdatedAtUtc: null }} className="size-7" />
                        <div className="grid min-w-0">
                          <span className="truncate font-medium">
                            {request.user.isKnown ? request.user.displayName : t('access.unknownUser')}
                          </span>
                          <span className="text-muted-foreground truncate font-mono text-xs">{request.user.email}</span>
                        </div>
                      </div>
                    </TableCell>
                    <TableCell>
                      <div className="font-medium">{request.application.displayName}</div>
                      <div className="text-muted-foreground font-mono text-xs">{request.application.clientId}</div>
                    </TableCell>
                    <TableCell className="max-w-64">
                      {request.reason ? (
                        <span className="line-clamp-2 text-sm whitespace-normal">{request.reason}</span>
                      ) : (
                        <span className="text-muted-foreground text-xs">—</span>
                      )}
                    </TableCell>
                    <TableCell>
                      <Tooltip>
                        <TooltipTrigger asChild>
                          <span className="text-sm">{formatRelative(request.createdAtUtc)}</span>
                        </TooltipTrigger>
                        <TooltipContent>{formatDate(request.createdAtUtc, { dateStyle: 'medium', timeStyle: 'short' })}</TooltipContent>
                      </Tooltip>
                    </TableCell>
                    <TableCell>
                      <RequestStatus request={request} />
                    </TableCell>
                    <TableCell className="text-right">
                      {request.status === 'pending' && (
                        <div className="flex justify-end gap-2">
                          <Button size="sm" onClick={() => setDecision({ kind: 'approve', request })}>
                            <Check />
                            {t('requests.approve')}
                          </Button>
                          <Button size="sm" variant="outline" onClick={() => setDecision({ kind: 'reject', request })}>
                            <X />
                            {t('requests.reject')}
                          </Button>
                        </div>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}

        {requests.data && totalPages > 1 && (
          <div className="mt-4 flex items-center justify-between gap-2 text-sm">
            <span className="text-muted-foreground">{t('common.pageOf', { page, pages: totalPages })}</span>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
                <ChevronLeft />
                {t('common.previous')}
              </Button>
              <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>
                {t('common.next')}
                <ChevronRight />
              </Button>
            </div>
          </div>
        )}
      </Section>

      <Dialog open={decision !== null} onOpenChange={(open) => !open && setDecision(null)}>
        <DialogContent>{decision && <DecisionForm decision={decision} onDone={() => setDecision(null)} />}</DialogContent>
      </Dialog>
    </div>
  )
}

/** The status, and for a decided request who decided, when, with which roles and note. */
function RequestStatus({ request }: { request: AccessRequest }) {
  const { t } = useI18n()
  const variant = request.status === 'approved' ? 'success' : request.status === 'pending' ? 'warning' : 'secondary'
  const badge = <Badge variant={variant}>{t(`requests.status.${request.status}`)}</Badge>

  if (request.status === 'pending') {
    return request.hasAccess ? (
      <div className="grid gap-1">
        {badge}
        <span className="text-muted-foreground text-xs">{t('requests.hasAccess')}</span>
      </div>
    ) : (
      badge
    )
  }

  return (
    <div className="grid gap-1">
      {badge}
      {request.decidedAtUtc && (
        <span className="text-muted-foreground text-xs">
          {request.decidedBy
            ? t('requests.decidedBy', { name: request.decidedBy.displayName, when: formatRelative(request.decidedAtUtc) })
            : formatRelative(request.decidedAtUtc)}
        </span>
      )}
      <RoleBadges roles={request.applicationRoles} keys={request.roles} />
      {request.decisionNote && <span className="text-muted-foreground text-xs italic">“{request.decisionNote}”</span>}
    </div>
  )
}

/** Approving (roles and a note) or rejecting (a note) one request. */
function DecisionForm({ decision, onDone }: { decision: Decision; onDone: () => void }) {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const refreshAccess = useAccessRefresh()
  const { request, kind } = decision
  const [roles, setRoles] = useState<string[]>([])
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | undefined>()

  const decide = useMutation({
    mutationFn: () => {
      const text = note.trim() === '' ? null : note.trim()
      return kind === 'approve' ? accessRequestsApi.approve(request.id, roles, text) : accessRequestsApi.reject(request.id, text)
    },
    meta: { handlesErrors: true },
    onSuccess: async () => {
      notify.success(
        kind === 'approve' ? t('requests.approved') : t('requests.rejected'),
        `${request.user.displayName} · ${request.application.displayName}`,
      )
      onDone()
      await Promise.all([queryClient.invalidateQueries({ queryKey: requestKeys.admin }), refreshAccess()])
    },
    onError: (failure) => reportFormError(failure, t('requests.decisionFailed')),
  })

  function submit(event: FormEvent) {
    event.preventDefault()

    if (note.trim().length > NOTE_MAX_LENGTH) {
      setError(t('requests.noteMax', { max: NOTE_MAX_LENGTH }))
      return
    }

    setError(undefined)
    decide.mutate()
  }

  return (
    <>
      <DialogHeader>
        <DialogTitle>
          {kind === 'approve'
            ? t('requests.approveTitle', { application: request.application.displayName })
            : t('requests.rejectTitle', { application: request.application.displayName })}
        </DialogTitle>
        <DialogDescription>{t('requests.decisionFor', { name: request.user.displayName, email: request.user.email })}</DialogDescription>
      </DialogHeader>

      <form id="decision-form" onSubmit={submit} noValidate className="grid items-start gap-5">
        {request.reason && (
          <blockquote className="text-muted-foreground border-l-2 pl-3 text-sm italic">{request.reason}</blockquote>
        )}

        {kind === 'approve' && request.hasAccess && (
          <Alert>
            <Info />
            <AlertDescription>{t('requests.hasAccessHint')}</AlertDescription>
          </Alert>
        )}

        {kind === 'approve' && request.applicationRoles.length > 0 && (
          <div className="grid gap-2">
            <Label>{t('requests.roles')}</Label>
            <p className="text-muted-foreground text-xs">{t('requests.rolesHint')}</p>
            <RoleChecklist roles={request.applicationRoles} value={roles} onChange={setRoles} idPrefix={`request-${request.id}`} />
          </div>
        )}

        <FormField
          label={kind === 'approve' ? t('requests.noteOptional') : t('requests.reasonForRequester')}
          htmlFor="decision-note"
          hint={t('requests.noteHint')}
          error={error}
        >
          <Textarea rows={3} maxLength={NOTE_MAX_LENGTH} value={note} onChange={(event) => setNote(event.target.value)} />
        </FormField>
      </form>

      <DialogFooter>
        <Button variant="outline" onClick={onDone}>
          {t('common.cancel')}
        </Button>
        <Button
          type="submit"
          form="decision-form"
          variant={kind === 'approve' ? 'default' : 'destructive'}
          disabled={decide.isPending}
        >
          {decide.isPending && <Loader2 className="animate-spin" />}
          {kind === 'approve' ? t('requests.approve') : t('requests.reject')}
        </Button>
      </DialogFooter>
    </>
  )
}
