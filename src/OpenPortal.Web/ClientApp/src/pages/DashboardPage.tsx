import { Link } from 'react-router-dom'
import { PageHeader } from '@/components/PageHeader'
import { Section } from '@/components/Section'
import { Button } from '@/components/ui/button'
import { useSession } from '@/hooks/useSession'
import { useI18n } from '@/i18n/useI18n'

/**
 * The landing page after sign-in. It is intentionally a thin launchpad: an application built on this shell
 * replaces the cards with its own entry points.
 */
export function DashboardPage() {
  const { user, isAdministrator } = useSession()
  const { t } = useI18n()

  return (
    <>
      <PageHeader
        title={t('dashboard.welcome', { name: user?.displayName ?? user?.email ?? '' })}
        description={t('dashboard.signedIn')}
      />

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
