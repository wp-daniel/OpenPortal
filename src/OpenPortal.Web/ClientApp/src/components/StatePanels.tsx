import type { ReactNode } from 'react'
import { AlertCircle } from 'lucide-react'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { useI18n } from '@/i18n/useI18n'

/** Placeholder shown while a query is pending. */
export function LoadingState({ label }: { label?: string }) {
  const { t } = useI18n()

  return (
    <div role="status" aria-live="polite" className="space-y-3">
      <span className="sr-only">{label ?? t('common.loading')}</span>
      <Skeleton className="h-6 w-1/3" />
      <Skeleton className="h-24 w-full" />
    </div>
  )
}

/** A page-level failure with an optional retry, and the trace id when the server supplied one. */
export function ErrorPanel({
  title,
  message,
  traceId,
  onRetry,
}: {
  title?: string
  message: string
  traceId?: string | undefined
  onRetry?: (() => void) | undefined
}) {
  const { t } = useI18n()

  return (
    <Alert variant="destructive">
      <AlertCircle />
      <AlertTitle>{title ?? t('common.somethingWrong')}</AlertTitle>
      <AlertDescription>
        <p>{message}</p>
        {traceId && (
          <p className="font-mono text-xs">
            {t('common.reference')} <code>{traceId}</code>
          </p>
        )}
        {onRetry && (
          <Button variant="outline" size="sm" className="mt-2" onClick={onRetry}>
            {t('common.tryAgain')}
          </Button>
        )}
      </AlertDescription>
    </Alert>
  )
}

export function EmptyState({ title, description, children }: { title: string; description: string; children?: ReactNode }) {
  return (
    <div className="rounded-lg border border-dashed p-10 text-center">
      <p className="text-sm font-medium">{title}</p>
      <p className="text-muted-foreground mt-1 text-sm">{description}</p>
      {children && <div className="mt-4">{children}</div>}
    </div>
  )
}
