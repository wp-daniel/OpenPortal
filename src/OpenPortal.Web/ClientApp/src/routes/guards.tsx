import { useEffect } from 'react'
import { Navigate, Outlet, useLocation, useMatches } from 'react-router-dom'
import type { RouteHandle } from '@/components/AppBreadcrumbs'
import { ErrorPanel, LoadingState } from '@/components/StatePanels'
import { useSession } from '@/hooks/useSession'
import { useI18n } from '@/i18n/useI18n'
import { serverReturnUrl } from '@/lib/returnUrl'

/** Where the user was heading before being sent to sign in, so sign-in can return them there. */
export interface RedirectState {
  readonly from?: string
}

function FullPage({ children }: { children: React.ReactNode }) {
  return <div className="mx-auto flex min-h-screen max-w-md flex-col justify-center px-4">{children}</div>
}

/**
 * Everything inside requires a signed-in user. The app has no anonymous pages: while the session is loading
 * it renders a placeholder, and once it is known an anonymous visitor is redirected to /sign-in.
 */
export function ProtectedRoute() {
  const { session, isPending, isError, refetch } = useSession()
  const location = useLocation()
  const { t } = useI18n()

  if (isPending) {
    return (
      <FullPage>
        <LoadingState label={t('guards.checking')} />
      </FullPage>
    )
  }

  if (isError) {
    return (
      <FullPage>
        <ErrorPanel
          title={t('guards.errorTitle')}
          message={t('guards.errorMessage')}
          onRetry={() => void refetch()}
        />
      </FullPage>
    )
  }

  if (!session.isAuthenticated) {
    const state: RedirectState = { from: `${location.pathname}${location.search}` }

    return <Navigate to="/sign-in" state={state} replace />
  }

  return <Outlet />
}

/**
 * Nested inside {@link ProtectedRoute}: lets through holders of the page the matched route declares in
 * `handle.page` (administrators hold every page); a route without one is for administrators only. Everyone
 * else returns to the dashboard. A new route is protected by declaring its page, with no change here.
 */
export function PageRoute() {
  const { isAdministrator, canOpen } = useSession()
  const matches = useMatches()

  // The deepest route that names a page decides, so a child can belong to a different page than its parent.
  const page = matches
    .map((match) => (match.handle as RouteHandle | undefined)?.page)
    .findLast((key) => key !== undefined)

  const allowed = page === undefined ? isAdministrator : canOpen(page)

  return allowed ? <Outlet /> : <Navigate to="/" replace />
}

/** The sign-in page is for anonymous visitors only; a signed-in user is sent where they were going. */
export function PublicOnlyRoute() {
  const { session, isPending } = useSession()
  const location = useLocation()
  const { t } = useI18n()

  if (isPending) {
    return (
      <FullPage>
        <LoadingState label={t('guards.checking')} />
      </FullPage>
    )
  }

  if (session.isAuthenticated) {
    // Another application sent the user here through the portal's OpenID Connect endpoint: go back there.
    const returnUrl = serverReturnUrl(location.search)
    if (returnUrl) {
      return <ServerRedirect to={returnUrl} />
    }

    const from = (location.state as RedirectState | null)?.from

    return <Navigate to={from && from !== '/sign-in' ? from : '/'} replace />
  }

  return <Outlet />
}

/** A full page load to a server route, with a placeholder while the browser leaves. */
function ServerRedirect({ to }: { to: string }) {
  const { t } = useI18n()

  useEffect(() => {
    window.location.replace(to)
  }, [to])

  return (
    <FullPage>
      <LoadingState label={t('guards.redirecting')} />
    </FullPage>
  )
}
