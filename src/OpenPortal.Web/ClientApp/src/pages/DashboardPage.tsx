import { Link } from 'react-router-dom'
import { PageHeader } from '@/components/PageHeader'
import { Section } from '@/components/Section'
import { Button } from '@/components/ui/button'
import { useSession } from '@/hooks/useSession'

/**
 * The landing page after sign-in. It is intentionally a thin launchpad: an application built on this shell
 * replaces the cards with its own entry points.
 */
export function DashboardPage() {
  const { user, isAdministrator } = useSession()

  return (
    <>
      <PageHeader title={`Welcome, ${user?.displayName ?? user?.email ?? ''}`} description="Signed in to OpenPortal." />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Section title="Your account" description="Update your display name and change your password.">
          <Button asChild variant="outline" size="sm">
            <Link to="/account">Open account</Link>
          </Button>
        </Section>

        {isAdministrator && (
          <>
            <Section title="Users" description="Create accounts, assign roles and review sign-in status.">
              <Button asChild variant="outline" size="sm">
                <Link to="/admin/users">Manage users</Link>
              </Button>
            </Section>

            <Section title="Content" description="Sample module: profile and projects.">
              <div className="flex flex-wrap gap-2">
                <Button asChild variant="outline" size="sm">
                  <Link to="/admin/content/profile">Profile</Link>
                </Button>
                <Button asChild variant="outline" size="sm">
                  <Link to="/admin/content/projects">Projects</Link>
                </Button>
              </div>
            </Section>
          </>
        )}
      </div>
    </>
  )
}
