import { isRouteErrorResponse, useNavigate, useRouteError } from 'react-router-dom'
import { ErrorPanel } from '@/components/StatePanels'
import { Button } from '@/components/ui/button'
import { describeError, traceIdOf } from '@/lib/errors'

/**
 * The last-resort failure screen for a render or route-loader crash.
 *
 * A thrown error unmounts the whole route, so without this the browser would show whatever stale markup was
 * last painted. The trace id is included because these failures are the ones a user cannot describe
 * usefully and can only report by quoting what is on screen.
 */
export function ErrorBoundary() {
  const error = useRouteError()
  const navigate = useNavigate()

  const message = isRouteErrorResponse(error) ? `${error.status} ${error.statusText}` : describeError(error)
  const traceId = isRouteErrorResponse(error) ? undefined : traceIdOf(error)

  return (
    <main className="mx-auto flex min-h-screen max-w-2xl flex-col justify-center gap-4 px-4">
      <ErrorPanel title="This page could not be displayed" message={message} traceId={traceId} />
      <div>
        <Button onClick={() => void navigate('/')}>Back to the portal</Button>
      </div>
    </main>
  )
}
