import { isRouteErrorResponse, useNavigate, useRouteError } from 'react-router-dom'
import { ErrorPanel } from '@/components/StatePanels'
import { Button } from '@/components/ui/button'
import { useI18n } from '@/i18n/useI18n'
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
  const { t } = useI18n()

  const message = isRouteErrorResponse(error) ? `${error.status} ${error.statusText}` : describeError(error)
  const traceId = isRouteErrorResponse(error) ? undefined : traceIdOf(error)

  return (
    <main className="mx-auto flex min-h-screen max-w-2xl flex-col justify-center gap-4 px-4">
      <ErrorPanel title={t('errorBoundary.title')} message={message} traceId={traceId} />
      <div>
        <Button onClick={() => void navigate('/')}>{t('errorBoundary.back')}</Button>
      </div>
    </main>
  )
}
