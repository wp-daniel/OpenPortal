import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { ErrorPanel, LoadingState } from '@/components/StatePanels'
import { useSession } from '@/hooks/useSession'

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

  if (isPending) {
    return (
      <FullPage>
        <LoadingState label="Checking your session…" />
      </FullPage>
    )
  }

  if (isError) {
    return (
      <FullPage>
        <ErrorPanel
          title="Could not check your session"
          message="The server could not be reached. Try again in a moment."
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

/** Nested inside {@link ProtectedRoute}: only administrators pass, everyone else returns to the dashboard. */
export function AdminRoute() {
  const { isAdministrator } = useSession()

  return isAdministrator ? <Outlet /> : <Navigate to="/" replace />
}

/** The sign-in page is for anonymous visitors only; a signed-in user is sent where they were going. */
export function PublicOnlyRoute() {
  const { session, isPending } = useSession()
  const location = useLocation()

  if (isPending) {
    return (
      <FullPage>
        <LoadingState label="Checking your session…" />
      </FullPage>
    )
  }

  if (session.isAuthenticated) {
    const from = (location.state as RedirectState | null)?.from

    return <Navigate to={from && from !== '/sign-in' ? from : '/'} replace />
  }

  return <Outlet />
}
