import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Building2, Undo2 } from 'lucide-react'
import { useState } from 'react'
import { myOrganizationsApi, organizationKeys } from '@/api/organizations'
import { myAccessRequestsApi, requestKeys } from '@/api/requests'
import type { AccessRequestStatus } from '@/api/types'
import { OrganizationMembersDialog } from '@/components/OrganizationMembersDialog'
import { Section } from '@/components/Section'
import { ErrorPanel, LoadingState } from '@/components/StatePanels'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { notify } from '@/hooks/useToast'
import { formatDate, formatRelative } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { describeError } from '@/lib/errors'

/** The organizations the signed-in user belongs to, as applications that work per organization see them. */
export function MyOrganizationsSection() {
  const { t } = useI18n()
  const [managing, setManaging] = useState<string | null>(null)

  const organizations = useQuery({
    queryKey: organizationKeys.mine,
    queryFn: ({ signal }) => myOrganizationsApi.list(signal),
  })

  return (
    <Section title={t('account.organizations.title')} description={t('account.organizations.description')}>
      {organizations.isPending && <LoadingState />}
      {organizations.isError && (
        <ErrorPanel message={describeError(organizations.error)} onRetry={() => void organizations.refetch()} />
      )}
      {organizations.data?.length === 0 && <p className="text-muted-foreground text-sm">{t('account.organizations.empty')}</p>}

      {organizations.data && organizations.data.length > 0 && (
        <ul className="divide-y rounded-md border">
          {organizations.data.map((organization) => (
            <li key={organization.id} className="flex items-center gap-3 px-3 py-2">
              <Building2 className="text-muted-foreground size-4 shrink-0" />
              <div className="grid min-w-0 flex-1">
                <span className="truncate text-sm font-medium">{organization.name}</span>
                <span className="text-muted-foreground font-mono text-xs">{organization.slug}</span>
              </div>
              <Badge variant={organization.role === 'admin' ? 'default' : 'secondary'}>
                {t(`organizations.roleName.${organization.role}`)}
              </Badge>
              {organization.role === 'admin' && (
                <Button size="sm" variant="outline" onClick={() => setManaging(organization.id)}>
                  {t('organizations.manage.open')}
                </Button>
              )}
            </li>
          ))}
        </ul>
      )}
      <OrganizationMembersDialog organizationId={managing} onClose={() => setManaging(null)} />
    </Section>
  )
}

const statusVariant: Record<AccessRequestStatus, 'success' | 'warning' | 'secondary' | 'destructive'> = {
  pending: 'warning',
  approved: 'success',
  rejected: 'destructive',
  cancelled: 'secondary',
}

/** The signed-in user's access requests and how each ended; a waiting one can be withdrawn. */
export function MyAccessRequestsSection() {
  const { t } = useI18n()
  const queryClient = useQueryClient()

  const requests = useQuery({
    queryKey: requestKeys.mine,
    queryFn: ({ signal }) => myAccessRequestsApi.list(signal),
  })

  const cancel = useMutation({
    mutationFn: (id: string) => myAccessRequestsApi.cancel(id),
    onSuccess: async () => {
      notify.success(t('requests.cancelled'))
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: requestKeys.mine }),
        queryClient.invalidateQueries({ queryKey: requestKeys.catalog }),
      ])
    },
  })

  return (
    <Section title={t('account.requests.title')} description={t('account.requests.description')}>
      {requests.isPending && <LoadingState />}
      {requests.isError && <ErrorPanel message={describeError(requests.error)} onRetry={() => void requests.refetch()} />}
      {requests.data?.length === 0 && <p className="text-muted-foreground text-sm">{t('account.requests.empty')}</p>}

      {requests.data && requests.data.length > 0 && (
        <ul className="divide-y rounded-md border">
          {requests.data.map((request) => (
            <li key={request.id} className="grid gap-1 px-3 py-3 sm:grid-cols-[1fr_auto] sm:items-center">
              <div className="grid min-w-0 gap-0.5">
                <span className="flex flex-wrap items-center gap-2">
                  <span className="text-sm font-medium">{request.applicationName}</span>
                  <Badge variant={statusVariant[request.status]}>{t(`requests.status.${request.status}`)}</Badge>
                </span>
                <span className="text-muted-foreground text-xs" title={formatDate(request.createdAtUtc, { dateStyle: 'full', timeStyle: 'short' })}>
                  {t('account.requests.asked', { when: formatRelative(request.createdAtUtc) })}
                  {request.decidedAtUtc && ` · ${t('account.requests.decided', { when: formatRelative(request.decidedAtUtc) })}`}
                </span>
                {request.reason && <span className="text-muted-foreground line-clamp-2 text-xs">{request.reason}</span>}
                {request.decisionNote && <span className="text-xs italic">“{request.decisionNote}”</span>}
              </div>
              {request.status === 'pending' && (
                <Button variant="ghost" size="sm" disabled={cancel.isPending} onClick={() => cancel.mutate(request.id)}>
                  <Undo2 />
                  {t('requests.cancel')}
                </Button>
              )}
            </li>
          ))}
        </ul>
      )}
    </Section>
  )
}
