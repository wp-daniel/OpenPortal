import { useMutation, useQuery } from '@tanstack/react-query'
import { Ban, CheckCircle2, FileDiff, KeyRound, MoreHorizontal, Pencil, Play, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { accessKeys, applicationsApi } from '@/api/access'
import type { ApplicationSecret, ApplicationSummary } from '@/api/types'
import { ApplicationFormDialog } from '@/components/access/ApplicationFormDialog'
import { SecretDialog } from '@/components/access/SecretDialog'
import { ApplicationStatusBadge, OnlineIndicator } from '@/components/access/shared'
import { useAccessRefresh } from '@/lib/access'
import { PageHeader } from '@/components/PageHeader'
import { Section } from '@/components/Section'
import { EmptyState, ErrorPanel, LoadingState } from '@/components/StatePanels'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Table, TableBody, TableCaption, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { notify } from '@/hooks/useToast'
import { formatDate } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'

type Confirmation =
  | { readonly kind: 'secret'; readonly application: ApplicationSummary }
  | { readonly kind: 'disable'; readonly application: ApplicationSummary }
  | { readonly kind: 'delete'; readonly application: ApplicationSummary }
  | { readonly kind: 'manifest'; readonly application: ApplicationSummary }

/**
 * The applications that sign in through the portal: approve the ones that announced themselves, register
 * others by hand, and manage their secrets and lifecycle. Who may open each one is managed on the Access page.
 */
export function AdminApplicationsPage() {
  const { t } = useI18n()
  const refresh = useAccessRefresh()

  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<ApplicationSummary | null>(null)
  const [secret, setSecret] = useState<ApplicationSecret | null>(null)
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null)

  const applications = useQuery({
    queryKey: accessKeys.applications,
    queryFn: ({ signal }) => applicationsApi.list(signal),
  })

  // Failures are toasted by the global MutationCache handler.
  const approve = useMutation({
    mutationFn: (application: ApplicationSummary) => applicationsApi.approve(application.id),
    onSuccess: async (issued) => {
      setSecret(issued)
      await refresh()
    },
  })

  const confirm = useMutation({
    mutationFn: async (pending: Confirmation): Promise<ApplicationSecret | null> => {
      switch (pending.kind) {
        case 'secret':
          return applicationsApi.regenerateSecret(pending.application.id)
        case 'disable':
          await applicationsApi.disable(pending.application.id)
          return null
        case 'manifest':
          await applicationsApi.applyManifest(pending.application.id)
          return null
        case 'delete':
          await applicationsApi.remove(pending.application.id)
          return null
      }
    },
    onSuccess: async (issued, pending) => {
      setConfirmation(null)

      if (issued) {
        setSecret(issued)
      } else {
        notify.success(t(`applications.done.${pending.kind}`), pending.application.displayName)
      }

      await refresh()
    },
  })

  const enable = useMutation({
    mutationFn: (application: ApplicationSummary) => applicationsApi.enable(application.id),
    onSuccess: async (_result, application) => {
      notify.success(t('applications.done.enable'), application.displayName)
      await refresh()
    },
  })

  const pendingCount = applications.data?.filter((application) => application.status === 'pending').length ?? 0

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('applications.title')}
        description={t('applications.description')}
        actions={
          <Button
            onClick={() => {
              setEditing(null)
              setFormOpen(true)
            }}
          >
            <Plus />
            {t('applications.register')}
          </Button>
        }
      />

      <Section
        title={t('applications.listTitle')}
        description={pendingCount > 0 ? t('applications.pendingCount', { count: pendingCount }) : t('applications.listDescription')}
      >
        {applications.isError && (
          <ErrorPanel
            message={describeError(applications.error)}
            traceId={traceIdOf(applications.error)}
            onRetry={() => void applications.refetch()}
          />
        )}

        {applications.isPending && <LoadingState />}

        {applications.data?.length === 0 && (
          <EmptyState title={t('applications.empty.title')} description={t('applications.empty.description')} />
        )}

        {applications.data && applications.data.length > 0 && (
          <div className="overflow-x-auto">
            <Table>
              <TableCaption className="sr-only">{t('applications.listTitle')}</TableCaption>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('applications.column.application')}</TableHead>
                  <TableHead>{t('common.status')}</TableHead>
                  <TableHead>{t('applications.column.access')}</TableHead>
                  <TableHead>{t('applications.column.lastSeen')}</TableHead>
                  <TableHead className="text-right">{t('common.actions')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {applications.data.map((application) => (
                  <TableRow key={application.id}>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        <OnlineIndicator lastSeenAtUtc={application.lastSeenAtUtc} />
                        <span className="font-medium">{application.displayName}</span>
                        {application.hasManifestChanges && (
                          <Badge variant="warning">{t('applications.manifestChanged')}</Badge>
                        )}
                      </div>
                      <div className="text-muted-foreground max-w-64 truncate font-mono text-xs" title={application.version ?? undefined}>
                        {application.clientId}
                        {application.version && ` · v${application.version}`}
                      </div>
                    </TableCell>
                    <TableCell>
                      <ApplicationStatusBadge status={application.status} />
                    </TableCell>
                    <TableCell className="text-muted-foreground text-sm">
                      {t('applications.accessSummary', {
                        groups: application.groupCount,
                        users: application.userCount,
                      })}
                    </TableCell>
                    <TableCell className="text-muted-foreground text-sm">
                      {application.lastSeenAtUtc
                        ? formatDate(application.lastSeenAtUtc, { dateStyle: 'short', timeStyle: 'short' })
                        : t('applications.neverSeen')}
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex items-center justify-end gap-2">
                        {application.status === 'pending' && (
                          <Button size="sm" disabled={approve.isPending} onClick={() => approve.mutate(application)}>
                            <CheckCircle2 />
                            {t('applications.approve')}
                          </Button>
                        )}

                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button variant="ghost" size="sm" aria-label={t('common.actions')}>
                              <MoreHorizontal />
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            <DropdownMenuItem
                              onSelect={() => {
                                setEditing(application)
                                setFormOpen(true)
                              }}
                            >
                              <Pencil />
                              {t('common.edit')}
                            </DropdownMenuItem>

                            {application.hasManifestChanges && (
                              <DropdownMenuItem onSelect={() => setConfirmation({ kind: 'manifest', application })}>
                                <FileDiff />
                                {t('applications.applyManifest')}
                              </DropdownMenuItem>
                            )}

                            {application.status !== 'pending' && (
                              <DropdownMenuItem onSelect={() => setConfirmation({ kind: 'secret', application })}>
                                <KeyRound />
                                {t('applications.regenerateSecret')}
                              </DropdownMenuItem>
                            )}

                            {application.status === 'active' && (
                              <DropdownMenuItem onSelect={() => setConfirmation({ kind: 'disable', application })}>
                                <Ban />
                                {t('applications.disable')}
                              </DropdownMenuItem>
                            )}

                            {application.status === 'disabled' && (
                              <DropdownMenuItem onSelect={() => enable.mutate(application)}>
                                <Play />
                                {t('applications.enable')}
                              </DropdownMenuItem>
                            )}

                            <DropdownMenuSeparator />
                            <DropdownMenuItem variant="destructive" onSelect={() => setConfirmation({ kind: 'delete', application })}>
                              <Trash2 />
                              {application.status === 'pending' ? t('applications.reject') : t('common.delete')}
                            </DropdownMenuItem>
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </Section>

      <ApplicationFormDialog
        open={formOpen}
        application={editing}
        onClose={() => setFormOpen(false)}
        onSaved={() => void refresh()}
        onCreated={setSecret}
      />

      <SecretDialog secret={secret} onClose={() => setSecret(null)} />

      <AlertDialog open={confirmation !== null} onOpenChange={(open) => !open && setConfirmation(null)}>
        <AlertDialogContent>
          {confirmation && (
            <>
              <AlertDialogHeader>
                <AlertDialogTitle>
                  {t(`applications.confirm.${confirmation.kind}.title`, { name: confirmation.application.displayName })}
                </AlertDialogTitle>
                <AlertDialogDescription asChild>
                  <div className="grid gap-3">
                    <p>{t(`applications.confirm.${confirmation.kind}.description`)}</p>
                    {confirmation.kind === 'manifest' && (
                      <ManifestDiff application={confirmation.application} />
                    )}
                  </div>
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>{t('common.cancel')}</AlertDialogCancel>
                <AlertDialogAction
                  className={confirmation.kind === 'delete' ? 'bg-destructive text-white hover:bg-destructive/90' : undefined}
                  disabled={confirm.isPending}
                  onClick={(event) => {
                    // Keep the dialog open until the request finishes; it closes on success.
                    event.preventDefault()
                    confirm.mutate(confirmation)
                  }}
                >
                  {t(`applications.confirm.${confirmation.kind}.action`)}
                </AlertDialogAction>
              </AlertDialogFooter>
            </>
          )}
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}

/** Redirect URIs in force versus the ones the application last announced. */
function ManifestDiff({ application }: { application: ApplicationSummary }) {
  const { t } = useI18n()

  return (
    <div className="grid gap-2 text-left text-xs">
      <div>
        <div className="text-foreground font-medium">{t('applications.manifest.current')}</div>
        <ul className="font-mono">
          {application.redirectUris.map((uri) => (
            <li key={uri}>{uri}</li>
          ))}
        </ul>
      </div>
      <div>
        <div className="text-foreground font-medium">{t('applications.manifest.announced')}</div>
        <ul className="font-mono">
          {application.announcedRedirectUris.map((uri) => (
            <li key={uri}>{uri}</li>
          ))}
        </ul>
      </div>
    </div>
  )
}
