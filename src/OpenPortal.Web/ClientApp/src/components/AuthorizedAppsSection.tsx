import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AppWindow, Unplug } from 'lucide-react'
import { accountApi } from '@/api/auth'
import type { AuthorizedApplication } from '@/api/types'
import { Section } from '@/components/Section'
import { ErrorPanel, LoadingState } from '@/components/StatePanels'
import { Button } from '@/components/ui/button'
import { notify } from '@/hooks/useToast'
import { formatDate } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'

const AUTHORIZED_KEY = ['account-authorized-applications'] as const

/**
 * The applications this account let sign it in, with what each receives. Taking the permission back ends what the
 * application holds (it must sign the person in again, and asks for consent again if it does that).
 */
export function AuthorizedAppsSection() {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const applications = useQuery({ queryKey: AUTHORIZED_KEY, queryFn: ({ signal }) => accountApi.authorizedApplications(signal) })

  const revoke = useMutation({
    mutationFn: (application: AuthorizedApplication) => accountApi.revokeAuthorizedApplication(application.clientId),
    onSuccess: async (_result, application) => {
      notify.success(t('authorizedApps.revoked', { app: application.displayName }))
      await queryClient.invalidateQueries({ queryKey: AUTHORIZED_KEY })
    },
  })

  return (
    <Section title={t('authorizedApps.title')} description={t('authorizedApps.description')}>
      {applications.isPending && <LoadingState />}
      {applications.isError && (
        <ErrorPanel
          message={describeError(applications.error)}
          traceId={traceIdOf(applications.error)}
          onRetry={() => void applications.refetch()}
        />
      )}
      {applications.data?.length === 0 && <p className="text-muted-foreground text-sm">{t('authorizedApps.none')}</p>}
      {applications.data && applications.data.length > 0 && (
        <ul className="divide-y rounded-lg border">
          {applications.data.map((application) => (
            <li key={application.clientId} className="flex items-center gap-3 p-3">
              <AppWindow className="text-muted-foreground size-5 shrink-0" />
              <div className="grid min-w-0 flex-1 gap-0.5">
                <span className="truncate text-sm font-medium">{application.displayName}</span>
                <span className="text-muted-foreground truncate text-xs">
                  {[
                    application.scopes.join(' '),
                    application.authorizedAtUtc
                      ? t('authorizedApps.since', { date: formatDate(application.authorizedAtUtc, { dateStyle: 'medium' }) })
                      : null,
                  ]
                    .filter(Boolean)
                    .join(' · ')}
                </span>
              </div>
              <Button size="sm" variant="ghost" disabled={revoke.isPending} onClick={() => revoke.mutate(application)}>
                <Unplug />
                <span className="hidden sm:inline">{t('authorizedApps.revoke')}</span>
              </Button>
            </li>
          ))}
        </ul>
      )}
    </Section>
  )
}
