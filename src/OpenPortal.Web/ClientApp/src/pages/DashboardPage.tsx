import { useQuery } from '@tanstack/react-query'
import { AppWindow, ExternalLink } from 'lucide-react'
import { Link } from 'react-router-dom'
import { accessKeys, launchpadApi } from '@/api/access'
import { PageHeader } from '@/components/PageHeader'
import { Section } from '@/components/Section'
import { ErrorPanel, LoadingState } from '@/components/StatePanels'
import { Button } from '@/components/ui/button'
import { Card, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useSession } from '@/hooks/useSession'
import { useI18n } from '@/i18n/useI18n'
import { describeError } from '@/lib/errors'

/**
 * The landing page after sign-in: the applications the user can open (their launchpad), then the portal's
 * own areas.
 */
export function DashboardPage() {
  const { user, isAdministrator } = useSession()
  const { t } = useI18n()

  const launchpad = useQuery({
    queryKey: accessKeys.launchpad,
    queryFn: ({ signal }) => launchpadApi.list(signal),
  })

  return (
    <>
      <PageHeader
        title={t('dashboard.welcome', { name: user?.displayName ?? user?.email ?? '' })}
        description={t('dashboard.signedIn')}
      />

      <section aria-labelledby="launchpad-title" className="mb-8 grid gap-3">
        <h2 id="launchpad-title" className="text-muted-foreground text-sm font-medium">
          {t('launchpad.title')}
        </h2>

        {launchpad.isError && <ErrorPanel message={describeError(launchpad.error)} onRetry={() => void launchpad.refetch()} />}
        {launchpad.isPending && <LoadingState />}

        {launchpad.data?.length === 0 && (
          <p className="text-muted-foreground rounded-lg border border-dashed p-6 text-center text-sm">{t('launchpad.empty')}</p>
        )}

        {launchpad.data && launchpad.data.length > 0 && (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {launchpad.data.map((application) => (
              <a
                key={application.id}
                href={application.baseUrl}
                className="group focus-visible:ring-ring/50 rounded-xl outline-none focus-visible:ring-[3px]"
              >
                <Card className="group-hover:border-foreground/20 h-full transition-colors">
                  <CardHeader>
                    <CardTitle className="flex items-center gap-2">
                      <AppWindow className="text-muted-foreground size-4" />
                      {application.displayName}
                      <ExternalLink className="text-muted-foreground ml-auto size-4 opacity-0 transition-opacity group-hover:opacity-100" />
                    </CardTitle>
                    {application.description && <CardDescription>{application.description}</CardDescription>}
                  </CardHeader>
                </Card>
              </a>
            ))}
          </div>
        )}
      </section>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Section title={t('dashboard.account.title')} description={t('dashboard.account.description')}>
          <Button asChild variant="outline" size="sm">
            <Link to="/account">{t('dashboard.account.open')}</Link>
          </Button>
        </Section>

        {isAdministrator && (
          <>
            <Section title={t('dashboard.users.title')} description={t('dashboard.users.description')}>
              <Button asChild variant="outline" size="sm">
                <Link to="/admin/users">{t('dashboard.users.manage')}</Link>
              </Button>
            </Section>

            <Section title={t('dashboard.access.title')} description={t('dashboard.access.description')}>
              <div className="flex flex-wrap gap-2">
                <Button asChild variant="outline" size="sm">
                  <Link to="/admin/access">{t('nav.access')}</Link>
                </Button>
                <Button asChild variant="outline" size="sm">
                  <Link to="/admin/applications">{t('nav.applications')}</Link>
                </Button>
                <Button asChild variant="outline" size="sm">
                  <Link to="/admin/groups">{t('nav.groups')}</Link>
                </Button>
              </div>
            </Section>

            <Section title={t('dashboard.content.title')} description={t('dashboard.content.description')}>
              <div className="flex flex-wrap gap-2">
                <Button asChild variant="outline" size="sm">
                  <Link to="/admin/content/profile">{t('nav.profile')}</Link>
                </Button>
                <Button asChild variant="outline" size="sm">
                  <Link to="/admin/content/projects">{t('nav.projects')}</Link>
                </Button>
              </div>
            </Section>
          </>
        )}
      </div>
    </>
  )
}
