import { useQuery } from '@tanstack/react-query'
import { AppWindow, ExternalLink, Sparkles } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { accessKeys, launchpadApi } from '@/api/access'
import { myAccessRequestsApi, requestKeys } from '@/api/requests'
import { PageHeader } from '@/components/PageHeader'
import { RequestAccessControl } from '@/components/RequestAccess'
import { Section } from '@/components/Section'
import { ErrorPanel, LoadingState } from '@/components/StatePanels'
import { PeriodPicker, StatisticsLink, StatisticsOverview } from '@/components/StatisticsOverview'
import type { StatisticsPeriod } from '@/lib/statistics'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useSession } from '@/hooks/useSession'
import { useI18n } from '@/i18n/useI18n'
import { describeError } from '@/lib/errors'

/**
 * The landing page after sign-in: the overview for whoever holds the statistics page, the applications the user
 * can open (their launchpad), the ones they may ask for, then the portal's own areas.
 */
export function DashboardPage() {
  const { user, canOpen } = useSession()
  const { t } = useI18n()
  const [days, setDays] = useState<StatisticsPeriod>(7)

  return (
    <>
      <PageHeader
        title={t('dashboard.welcome', { name: user?.displayName ?? user?.email ?? '' })}
        description={t('dashboard.signedIn')}
      />

      {canOpen('statistics') && (
        <section aria-labelledby="overview-title" className="mb-8 grid gap-3">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h2 id="overview-title" className="text-muted-foreground text-sm font-medium">
              {t('dashboard.overview')}
            </h2>
            <div className="flex items-center gap-2">
              <PeriodPicker value={days} onChange={setDays} />
              <StatisticsLink />
            </div>
          </div>
          <StatisticsOverview days={days} compact />
        </section>
      )}

      <Launchpad />
      <RequestableApplications />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Section title={t('dashboard.account.title')} description={t('dashboard.account.description')}>
          <Button asChild variant="outline" size="sm">
            <Link to="/account">{t('dashboard.account.open')}</Link>
          </Button>
        </Section>

        {canOpen('users') && (
          <Section title={t('dashboard.users.title')} description={t('dashboard.users.description')}>
            <div className="flex flex-wrap gap-2">
              <Button asChild variant="outline" size="sm">
                <Link to="/admin/users">{t('dashboard.users.manage')}</Link>
              </Button>
              <Button asChild variant="outline" size="sm">
                <Link to="/admin/invitations">{t('nav.invitations')}</Link>
              </Button>
            </div>
          </Section>
        )}

        <ShortcutSection
          title={t('dashboard.access.title')}
          description={t('dashboard.access.description')}
          links={[
            { to: '/admin/access', label: t('nav.access'), page: 'access' },
            { to: '/admin/access-requests', label: t('nav.accessRequests'), page: 'access-requests' },
            { to: '/admin/applications', label: t('nav.applications'), page: 'applications' },
            { to: '/admin/groups', label: t('nav.groups'), page: 'groups' },
            { to: '/admin/organizations', label: t('nav.organizations'), page: 'organizations' },
          ]}
        />

        <ShortcutSection
          title={t('dashboard.content.title')}
          description={t('dashboard.content.description')}
          links={[
            { to: '/admin/content/profile', label: t('nav.profile'), page: 'content.profile' },
            { to: '/admin/content/projects', label: t('nav.projects'), page: 'content.projects' },
          ]}
        />
      </div>
    </>
  )
}

/** The applications the user can open. */
function Launchpad() {
  const { t } = useI18n()

  const launchpad = useQuery({
    queryKey: accessKeys.launchpad,
    queryFn: ({ signal }) => launchpadApi.list(signal),
  })

  return (
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
  )
}

/** Applications the user cannot open but may ask for; the section is left out when there are none. */
function RequestableApplications() {
  const { t } = useI18n()

  const catalog = useQuery({
    queryKey: requestKeys.catalog,
    queryFn: ({ signal }) => myAccessRequestsApi.catalog(signal),
  })

  if (!catalog.data || catalog.data.length === 0) {
    return null
  }

  return (
    <section aria-labelledby="catalog-title" className="mb-8 grid gap-3">
      <div>
        <h2 id="catalog-title" className="text-muted-foreground flex items-center gap-1.5 text-sm font-medium">
          <Sparkles className="size-4" />
          {t('catalog.title')}
        </h2>
        <p className="text-muted-foreground text-xs">{t('catalog.description')}</p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {catalog.data.map((application) => (
          <Card key={application.id} className="border-dashed">
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <AppWindow className="text-muted-foreground size-4" />
                {application.displayName}
              </CardTitle>
              {application.description && <CardDescription>{application.description}</CardDescription>}
            </CardHeader>
            <CardContent>
              <RequestAccessControl application={application} />
            </CardContent>
          </Card>
        ))}
      </div>
    </section>
  )
}

interface Shortcut {
  readonly to: string
  readonly label: string
  /** Portal page key; the link shows only to holders of the page and administrators. */
  readonly page: string
}

/** A card of links to administration pages, keeping only those the user may open (none: no card). */
function ShortcutSection({ title, description, links }: { title: string; description: string; links: Shortcut[] }) {
  const { canOpen } = useSession()
  const visible = links.filter((link) => canOpen(link.page))

  if (visible.length === 0) {
    return null
  }

  return (
    <Section title={title} description={description}>
      <div className="flex flex-wrap gap-2">
        {visible.map((link) => (
          <Button key={link.to} asChild variant="outline" size="sm">
            <Link to={link.to}>{link.label}</Link>
          </Button>
        ))}
      </div>
    </Section>
  )
}
