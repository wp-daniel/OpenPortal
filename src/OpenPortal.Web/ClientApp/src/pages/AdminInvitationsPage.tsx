import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, ChevronLeft, ChevronRight, Copy, Loader2, MailPlus, RotateCw, Send, Undo2 } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { z } from 'zod'
import { accessKeys, groupsApi } from '@/api/access'
import { organizationKeys, organizationsApi } from '@/api/organizations'
import { invitationKeys, invitationsApi } from '@/api/registration'
import type { Invitation, InvitationIssued, InvitationStatus } from '@/api/types'
import { FormField } from '@/components/FormField'
import { PageHeader } from '@/components/PageHeader'
import { Section } from '@/components/Section'
import { EmptyState, ErrorPanel, LoadingState } from '@/components/StatePanels'
import { FilterSelect, TableToolbar } from '@/components/TableToolbar'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Table, TableBody, TableCaption, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Textarea } from '@/components/ui/textarea'
import { useDebounced } from '@/hooks/useDebounced'
import { useSession } from '@/hooks/useSession'
import { notify } from '@/hooks/useToast'
import { formatDate, formatRelative } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'
import { reportFormError, serverFieldErrors, zodFieldErrors } from '@/lib/forms'

const PAGE_SIZE = 25
const STATUSES: readonly InvitationStatus[] = ['pending', 'accepted', 'expired', 'revoked']

const statusVariant: Record<InvitationStatus, 'warning' | 'success' | 'secondary' | 'destructive'> = {
  pending: 'warning',
  accepted: 'success',
  expired: 'secondary',
  revoked: 'destructive',
}

/**
 * Invitations by email: whom the portal invited, and what became of it. Inviting issues a link (emailed when the
 * portal can send email, shown here once either way) that lets the person create their account, already in the
 * groups and organizations chosen.
 */
export function AdminInvitationsPage() {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const [page, setPage] = useState(1)
  const [status, setStatus] = useState('')
  const [search, setSearch] = useState('')
  const term = useDebounced(search.trim())
  const [inviting, setInviting] = useState(false)
  const [issued, setIssued] = useState<InvitationIssued | null>(null)

  const invitations = useQuery({
    queryKey: [...invitationKeys.all, { page, status, term }],
    queryFn: ({ signal }) => invitationsApi.list({ page, pageSize: PAGE_SIZE, status: status || undefined, search: term }, signal),
    placeholderData: (previous) => previous,
  })

  const refresh = () => queryClient.invalidateQueries({ queryKey: invitationKeys.all })

  const resend = useMutation({
    mutationFn: (invitation: Invitation) => invitationsApi.resend(invitation.id),
    onSuccess: async (result) => {
      setIssued(result)
      await refresh()
    },
  })

  const revoke = useMutation({
    mutationFn: (invitation: Invitation) => invitationsApi.revoke(invitation.id),
    onSuccess: async (_result, invitation) => {
      notify.success(t('invitations.revoked'), invitation.email)
      await refresh()
    },
  })

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('invitations.title')}
        description={t('invitations.description')}
        actions={
          <Button onClick={() => setInviting(true)}>
            <MailPlus />
            {t('invitations.invite')}
          </Button>
        }
      />

      <Section title={t('invitations.listTitle')}>
        <TableToolbar
          search={search}
          onSearchChange={(value) => {
            setSearch(value)
            setPage(1)
          }}
          searchPlaceholder={t('invitations.search')}
          filtersActive={search !== '' || status !== ''}
          onReset={() => {
            setSearch('')
            setStatus('')
            setPage(1)
          }}
          resultCount={invitations.data?.totalCount}
        >
          <FilterSelect
            label={t('common.status')}
            value={status}
            onChange={(value) => {
              setStatus(value)
              setPage(1)
            }}
            options={STATUSES.map((value) => ({ value, label: t(`invitations.status.${value}`) }))}
          />
        </TableToolbar>

        {invitations.isError && (
          <ErrorPanel
            message={describeError(invitations.error)}
            traceId={traceIdOf(invitations.error)}
            onRetry={() => void invitations.refetch()}
          />
        )}
        {invitations.isPending && <LoadingState />}

        {invitations.data?.items.length === 0 && (
          <EmptyState title={t('invitations.empty.title')} description={t('invitations.empty.description')}>
            <Button variant="outline" onClick={() => setInviting(true)}>
              <MailPlus />
              {t('invitations.invite')}
            </Button>
          </EmptyState>
        )}

        {invitations.data && invitations.data.items.length > 0 && (
          <div className="overflow-x-auto">
            <Table>
              <TableCaption className="sr-only">{t('invitations.listTitle')}</TableCaption>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('invitations.column.email')}</TableHead>
                  <TableHead>{t('common.status')}</TableHead>
                  <TableHead>{t('invitations.column.invitedBy')}</TableHead>
                  <TableHead>{t('invitations.column.expires')}</TableHead>
                  <TableHead className="text-right">{t('common.actions')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {invitations.data.items.map((invitation) => {
                  const open = invitation.status === 'pending' || invitation.status === 'expired'

                  return (
                    <TableRow key={invitation.id}>
                      <TableCell>
                        <div className="font-mono text-sm">{invitation.email}</div>
                        {(invitation.firstName || invitation.lastName) && (
                          <div className="text-muted-foreground text-xs">
                            {[invitation.firstName, invitation.lastName].filter(Boolean).join(' ')}
                          </div>
                        )}
                        {(invitation.groupIds.length > 0 || invitation.organizationIds.length > 0) && (
                          <div className="text-muted-foreground text-xs">
                            {t('invitations.memberships', {
                              groups: invitation.groupIds.length,
                              organizations: invitation.organizationIds.length,
                            })}
                          </div>
                        )}
                      </TableCell>
                      <TableCell>
                        <Badge variant={statusVariant[invitation.status]}>{t(`invitations.status.${invitation.status}`)}</Badge>
                        {invitation.acceptedAtUtc && (
                          <div className="text-muted-foreground mt-1 text-xs">{formatRelative(invitation.acceptedAtUtc)}</div>
                        )}
                      </TableCell>
                      <TableCell>
                        <div className="text-sm">{invitation.invitedByName ?? '—'}</div>
                        <div className="text-muted-foreground text-xs">{formatRelative(invitation.lastSentAtUtc)}</div>
                      </TableCell>
                      <TableCell className="text-sm">
                        {invitation.status === 'pending' || invitation.status === 'expired'
                          ? formatDate(invitation.expiresAtUtc, { dateStyle: 'medium', timeStyle: 'short' })
                          : '—'}
                      </TableCell>
                      <TableCell className="text-right">
                        {open && (
                          <div className="flex justify-end gap-2">
                            <Button
                              variant="ghost"
                              size="sm"
                              disabled={resend.isPending}
                              onClick={() => resend.mutate(invitation)}
                            >
                              <RotateCw />
                              {t('invitations.resend')}
                            </Button>
                            <Button
                              variant="ghost"
                              size="sm"
                              disabled={revoke.isPending}
                              onClick={() => revoke.mutate(invitation)}
                            >
                              <Undo2 />
                              {t('invitations.revoke')}
                            </Button>
                          </div>
                        )}
                      </TableCell>
                    </TableRow>
                  )
                })}
              </TableBody>
            </Table>
          </div>
        )}

        {invitations.data && invitations.data.totalPages > 1 && (
          <div className="mt-4 flex items-center justify-between gap-2 text-sm">
            <span className="text-muted-foreground">
              {t('common.pageOf', { page: invitations.data.page, pages: invitations.data.totalPages })}
            </span>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" disabled={!invitations.data.hasPrevious} onClick={() => setPage(page - 1)}>
                <ChevronLeft />
                {t('common.previous')}
              </Button>
              <Button variant="outline" size="sm" disabled={!invitations.data.hasNext} onClick={() => setPage(page + 1)}>
                {t('common.next')}
                <ChevronRight />
              </Button>
            </div>
          </div>
        )}
      </Section>

      <Dialog open={inviting} onOpenChange={setInviting}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-xl">
          {inviting && (
            <InviteForm
              onClose={() => setInviting(false)}
              onIssued={async (result) => {
                setInviting(false)
                setIssued(result)
                await refresh()
              }}
            />
          )}
        </DialogContent>
      </Dialog>

      <IssuedDialog issued={issued} onClose={() => setIssued(null)} />
    </div>
  )
}

type Field = 'email' | 'firstName' | 'lastName' | 'message'

function InviteForm({ onClose, onIssued }: { onClose: () => void; onIssued: (issued: InvitationIssued) => Promise<void> }) {
  const { t } = useI18n()
  const { canOpen } = useSession()
  const [fields, setFields] = useState<Record<Field, string>>({ email: '', firstName: '', lastName: '', message: '' })
  const [groupIds, setGroupIds] = useState<string[]>([])
  const [organizationIds, setOrganizationIds] = useState<string[]>([])
  const [errors, setErrors] = useState<Partial<Record<Field, string>>>({})

  // Promising memberships needs the matching page: without it the lists are not offered at all.
  const groups = useQuery({
    queryKey: accessKeys.groups,
    queryFn: ({ signal }) => groupsApi.list(signal),
    enabled: canOpen('groups'),
  })

  const organizations = useQuery({
    queryKey: organizationKeys.all,
    queryFn: ({ signal }) => organizationsApi.list(signal),
    enabled: canOpen('organizations'),
  })

  const schema = useMemo(
    () =>
      z.object({
        email: z.string().trim().min(1, t('validation.emailRequired')).email(t('validation.emailInvalid')),
        firstName: z.string().trim().max(60, t('validation.maxLength', { max: 60 })),
        lastName: z.string().trim().max(60, t('validation.maxLength', { max: 60 })),
        message: z.string().trim().max(500, t('validation.maxLength', { max: 500 })),
      }),
    [t],
  )

  const invite = useMutation({
    mutationFn: () => {
      const optional = (value: string) => (value.trim() === '' ? null : value.trim())

      return invitationsApi.create({
        email: fields.email.trim(),
        firstName: optional(fields.firstName),
        lastName: optional(fields.lastName),
        message: optional(fields.message),
        groupIds,
        organizationIds,
      })
    },
    meta: { handlesErrors: true },
    onSuccess: onIssued,
    onError: (failure) => reportFormError(failure, t('invitations.failed')),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    const parsed = schema.safeParse(fields)

    if (!parsed.success) {
      setErrors(zodFieldErrors<Field>(parsed.error))
      return
    }

    setErrors({})
    invite.mutate()
  }

  const server = serverFieldErrors(invite.error)
  const set = (field: Field) => (value: string) => setFields((current) => ({ ...current, [field]: value }))
  const toggle = (list: string[], id: string, checked: boolean) => (checked ? [...list, id] : list.filter((entry) => entry !== id))

  return (
    <>
      <DialogHeader>
        <DialogTitle>{t('invitations.form.title')}</DialogTitle>
        <DialogDescription>{t('invitations.form.description')}</DialogDescription>
      </DialogHeader>

      <form id="invitation-form" onSubmit={submit} noValidate className="grid items-start gap-5">
        <FormField label={t('common.email')} htmlFor="invitation-email" error={errors.email ?? server.email?.[0]}>
          <Input type="email" autoComplete="off" value={fields.email} onChange={(event) => set('email')(event.target.value)} />
        </FormField>

        <div className="grid items-start gap-5 sm:grid-cols-2">
          <FormField label={t('common.firstName')} htmlFor="invitation-first" hint={t('invitations.form.optional')} error={errors.firstName}>
            <Input value={fields.firstName} onChange={(event) => set('firstName')(event.target.value)} />
          </FormField>
          <FormField label={t('common.lastName')} htmlFor="invitation-last" hint={t('invitations.form.optional')} error={errors.lastName}>
            <Input value={fields.lastName} onChange={(event) => set('lastName')(event.target.value)} />
          </FormField>
        </div>

        <FormField label={t('invitations.form.message')} htmlFor="invitation-message" hint={t('invitations.form.messageHint')} error={errors.message}>
          <Textarea rows={3} maxLength={500} value={fields.message} onChange={(event) => set('message')(event.target.value)} />
        </FormField>

        {groups.data && groups.data.length > 0 && (
          <fieldset className="grid gap-2">
            <legend className="mb-1 text-sm font-medium">{t('invitations.form.groups')}</legend>
            <ul className="grid max-h-40 gap-2 overflow-y-auto rounded-md border p-3 sm:grid-cols-2">
              {groups.data.map((group) => (
                <li key={group.id} className="flex items-center gap-2">
                  <Checkbox
                    id={`invite-group-${group.id}`}
                    checked={groupIds.includes(group.id)}
                    onCheckedChange={(checked) => setGroupIds((current) => toggle(current, group.id, checked === true))}
                  />
                  <Label htmlFor={`invite-group-${group.id}`} className="truncate font-normal">
                    {group.name}
                  </Label>
                </li>
              ))}
            </ul>
          </fieldset>
        )}

        {organizations.data && organizations.data.length > 0 && (
          <fieldset className="grid gap-2">
            <legend className="mb-1 text-sm font-medium">{t('invitations.form.organizations')}</legend>
            <ul className="grid max-h-40 gap-2 overflow-y-auto rounded-md border p-3 sm:grid-cols-2">
              {organizations.data.map((organization) => (
                <li key={organization.id} className="flex items-center gap-2">
                  <Checkbox
                    id={`invite-org-${organization.id}`}
                    checked={organizationIds.includes(organization.id)}
                    onCheckedChange={(checked) =>
                      setOrganizationIds((current) => toggle(current, organization.id, checked === true))
                    }
                  />
                  <Label htmlFor={`invite-org-${organization.id}`} className="truncate font-normal">
                    {organization.name}
                  </Label>
                </li>
              ))}
            </ul>
          </fieldset>
        )}
      </form>

      <DialogFooter>
        <Button variant="outline" onClick={onClose}>
          {t('common.cancel')}
        </Button>
        <Button type="submit" form="invitation-form" disabled={invite.isPending}>
          {invite.isPending ? <Loader2 className="animate-spin" /> : <Send />}
          {t('invitations.form.send')}
        </Button>
      </DialogFooter>
    </>
  )
}

/** The link just issued: emailed or not, this is the only time it is shown. */
function IssuedDialog({ issued, onClose }: { issued: InvitationIssued | null; onClose: () => void }) {
  const { t } = useI18n()
  const [copied, setCopied] = useState(false)

  async function copy(value: string) {
    try {
      await navigator.clipboard.writeText(value)
      setCopied(true)
      notify.success(t('invitations.issued.copied'))
    } catch {
      notify.warning(t('applications.secret.copyFailed'))
    }
  }

  return (
    <Dialog
      open={issued !== null}
      onOpenChange={(open) => {
        if (!open) {
          setCopied(false)
          onClose()
        }
      }}
    >
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{t('invitations.issued.title')}</DialogTitle>
          <DialogDescription>{issued?.invitation.email}</DialogDescription>
        </DialogHeader>

        {issued && (
          <div className="grid gap-4">
            <Alert>
              <AlertTitle>{issued.emailSent ? t('invitations.issued.emailed') : t('invitations.issued.notEmailed')}</AlertTitle>
              <AlertDescription>{t('invitations.issued.once')}</AlertDescription>
            </Alert>
            <div className="grid gap-2">
              <Label htmlFor="invitation-link">{t('invitations.issued.link')}</Label>
              <div className="flex gap-2">
                <Input
                  id="invitation-link"
                  readOnly
                  value={issued.link}
                  className="font-mono text-xs"
                  onFocus={(event) => event.target.select()}
                />
                <Button variant="outline" size="icon" aria-label={t('invitations.issued.copy')} onClick={() => void copy(issued.link)}>
                  {copied ? <Check /> : <Copy />}
                </Button>
              </div>
              <p className="text-muted-foreground text-xs">
                {t('invitations.issued.expires', { date: formatDate(issued.invitation.expiresAtUtc, { dateStyle: 'long', timeStyle: 'short' }) })}
              </p>
            </div>
          </div>
        )}

        <DialogFooter>
          <Button onClick={onClose}>{t('applications.secret.done')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
