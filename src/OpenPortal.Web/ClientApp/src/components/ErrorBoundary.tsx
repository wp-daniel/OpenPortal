import { isRouteErrorResponse, useRouteError } from 'react-router-dom'
import { describeError, traceIdOf } from '../hooks/useRetryableError'
import { Button } from './ui'

/**
 * The last-resort failure screen for a render or route-loader crash.
 *
 * A thrown error unmounts the whole route, so without this the browser would show whatever stale markup was
 * last painted. The trace id is included because these failures are the ones a user cannot describe
 * usefully and can only report by quoting what is on screen.
 */
export function ErrorBoundary() {
  const error = useRouteError()

  const message = isRouteErrorResponse(error)
    ? `${error.status} ${error.statusText}`
    : describeError(error)

  const traceId = isRouteErrorResponse(error) ? undefined : traceIdOf(error)

  return (
    <main className="mx-auto flex min-h-screen max-w-2xl flex-col justify-center px-4">
      <div role="alert" className="border-destructive/40 bg-destructive/5 rounded-lg border p-6">
        <h1 className="text-destructive text-lg font-semibold">This page could not be displayed</h1>
        <p className="text-muted-foreground mt-2 text-sm">{message}</p>
        {traceId && (
          <p className="text-muted-foreground mt-3 font-mono text-xs">
            Reference: <code>{traceId}</code>
          </p>
        )}
        <div className="mt-5">
          <Button onClick={() => window.location.assign('/')}>Back to the portal</Button>
        </div>
      </div>
    </main>
  )
}