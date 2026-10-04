import { useMutation, useQuery } from '@tanstack/react-query'
import { Users } from 'lucide-react'
import { accessApi, accessKeys, applicationsApi } from '@/api/access'
import type { UserSummary } from '@/api/types'
import { ErrorPanel, LoadingState } from '@/components/StatePanels'
import { Badge } from '@/components/ui/badge'
import { Label } from '@/components/ui/label'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Separator } from '@/components/ui/separator'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { Switch } from '@/components/ui/switch'
import { useI18n } from '@/i18n/useI18n'
import { describeError } from '@/lib/errors'
import { useAccessRefresh } from '@/lib/access'
import { ApplicationStatusBadge } from './shared'

/**
 * One user's applications, with why they can open each: a personal grant (the switch) and/or the groups
 * that carry it (badges). Turning the switch off removes only the personal grant.
 */
export function UserAccessSheet({ user, onClose }: { user: UserSummary | null; onClose: () => void }) {
  const { t } = useI18n()
  const refresh = useAccessRefresh()

  const access = useQuery({
    queryKey: accessKeys.user(user?.id ?? ''),
    queryFn: ({ signal }) => accessApi.user(user!.id, signal),
    enabled: user !== null,
  })

  const applications = useQuery({
    queryKey: accessKeys.applications,
    queryFn: ({ signal }) => applicationsApi.list(signal),
    enabled: user !== null,
  })

  const toggle = useMutation({
    mutationFn: ({ applicationId, granted }: { applicationId: string; granted: boolean }) =>
      granted ? accessApi.grantUser(applicationId, user!.id) : accessApi.revokeUser(applicationId, user!.id),
    onSuccess: () => refresh(),
  })

  const byApplication = new Map(access.data?.applications.map((entry) => [entry.applicationId, entry]))

  return (
    <Sheet open={user !== null} onOpenChange={(open) => !open && onClose()}>
      <SheetContent className="w-full gap-0 sm:max-w-md">
        <SheetHeader>
          <SheetTitle>{t('access.user.title', { name: user?.displayName ?? '' })}</SheetTitle>
          <SheetDescription>{user?.email}</SheetDescription>
        </SheetHeader>

        <ScrollArea className="min-h-0 flex-1">
          <div className="grid gap-6 px-4 pb-6">
            {(access.isError || applications.isError) && (
              <ErrorPanel
                message={describeError(access.error ?? applications.error)}
                onRetry={() => {
                  void access.refetch()
                  void applications.refetch()
                }}
              />
            )}
            {(access.isPending || applications.isPending) && <LoadingState />}

            {access.data && applications.data && (
              <>
                <section className="grid gap-2">
                  <h3 className="text-sm font-medium">{t('access.user.groups')}</h3>
                  {access.data.groups.length === 0 ? (
                    <p className="text-muted-foreground text-sm">{t('access.user.noGroups')}</p>
                  ) : (
                    <div className="flex flex-wrap gap-1">
                      {access.data.groups.map((group) => (
                        <Badge key={group.id} variant="secondary">
                          <Users />
                          {group.name}
                        </Badge>
                      ))}
                    </div>
                  )}
                </section>

                <Separator />

                <section className="grid gap-3">
                  <h3 className="text-sm font-medium">{t('access.user.applications')}</h3>
                  <p className="text-muted-foreground text-xs">{t('access.user.applicationsHint')}</p>

                  {applications.data.length === 0 && <p className="text-muted-foreground text-sm">{t('applications.empty.title')}</p>}

                  <ul className="divide-y rounded-md border">
                    {applications.data.map((application) => {
                      const entry = byApplication.get(application.id)
                      const switchId = `user-app-${application.id}`

                      return (
                        <li key={application.id} className="flex items-center gap-3 px-3 py-2">
                          <div className="grid min-w-0 flex-1 gap-1">
                            <Label htmlFor={switchId} className="truncate">
                              {application.displayName}
                            </Label>
                            <div className="flex flex-wrap items-center gap-1">
                              {application.status !== 'active' && <ApplicationStatusBadge status={application.status} />}
                              {entry?.viaGroups.map((group) => (
                                <Badge key={group.id} variant="outline">
                                  {t('access.user.viaGroup', { name: group.name })}
                                </Badge>
                              ))}
                              {!entry && <span className="text-muted-foreground text-xs">{t('access.user.noAccess')}</span>}
                            </div>
                          </div>
                          <Switch
                            id={switchId}
                            aria-label={t('access.user.direct')}
                            checked={entry?.direct ?? false}
                            disabled={toggle.isPending}
                            onCheckedChange={(granted) => toggle.mutate({ applicationId: application.id, granted })}
                          />
                        </li>
                      )
                    })}
                  </ul>
                </section>
              </>
            )}
          </div>
        </ScrollArea>
      </SheetContent>
    </Sheet>
  )
}
