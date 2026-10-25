import { useEffect, useState } from 'react'
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

/**
 * Nested inside {@link ProtectedRoute}, around the portal's own pages: what the account must settle first. An
 * account waiting to be deleted can only keep itself or sign out; a new version of the privacy notice or the terms
 * of use is accepted before anything else.
 */
export function AccountGate() {
  const { user } = useSession()
  const location = useLocation()

  if (user?.deletionScheduledAtUtc) {
    return <Navigate to="/account/deletion" replace />
  }

  // A support session browses as the account but never accepts documents on its owner's behalf.
  if (user && !user.impersonator && user.pendingLegalDocuments.length > 0) {
    const state: RedirectState = { from: `${location.pathname}${location.search}` }

    return <Navigate to="/legal/accept" state={state} replace />
  }

  // The portal requires a second factor and the account has none yet: that comes before anything else.
  if (user && !user.impersonator && user.twoFactorSetupRequired) {
    const state: RedirectState = { from: `${location.pathname}${location.search}` }

    return <Navigate to="/account/security-setup" state={state} replace />
  }

  return <Outlet />
}

/**
 * The sign-in page is for anonymous visitors only; a signed-in user is sent where they were going. The exception is
 * `?reauth=1`: an application asked the portal for a new sign-in (`prompt=login`, `max_age`), so the form stays until
 * the person has signed in again, which shows as a new authentication time in the session.
 */
export function PublicOnlyRoute() {
  const { session, isPending } = useSession()
  const location = useLocation()
  const { t } = useI18n()
  const returnUrl = serverReturnUrl(location.search)
  const reauth = returnUrl !== null && new URLSearchParams(location.search).get('reauth') === '1'
  const authenticatedAt = session.user?.authenticatedAtUtc ?? null

  // The authentication time the page opened with (noted while rendering, once the session is known); a different one
  // means the person just signed in again.
  const [openedWith, setOpenedWith] = useState<string | null | undefined>(undefined)
  if (reauth && session.isAuthenticated && openedWith === undefined) {
    setOpenedWith(authenticatedAt)
  }

  if (isPending) {
    return (
      <FullPage>
        <LoadingState label={t('guards.checking')} />
      </FullPage>
    )
  }

  if (session.isAuthenticated) {
    if (reauth && (openedWith === undefined || openedWith === authenticatedAt)) {
      return <Outlet />
    }

    // Another application sent the user here through the portal's OpenID Connect endpoint: go back there.
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
