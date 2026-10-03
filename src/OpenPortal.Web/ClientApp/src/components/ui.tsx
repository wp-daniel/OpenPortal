import type { ReactNode } from 'react'

/**
 * The primitives every screen is built from.
 *
 * Hand-written rather than pulled from the shadcn CLI on purpose: the handful of shapes the portal needs are
 * listed here, each with the accessibility decisions already settled, so adding a component is a reviewable
 * diff instead of a generated one. The class names and theme tokens match shadcn conventions, so a shadcn
 * component dropped in later will look identical.
 */

/** Fetches data with React Query and renders the three states it can be in. */
export function AsyncContent<T>({
  isPending,
  error,
  data,
  children,
  empty,
}: {
  isPending: boolean
  error: unknown
  data: T | undefined
  children: (data: T) => ReactNode
  empty?: ReactNode
}) {
  if (isPending) {
    return (
      <p role="status" className="text-muted-foreground py-8 text-center text-sm">
        Loading…
      </p>
    )
  }

  if (error) {
    return null
  }

  if (data === undefined) {
    return <>{empty ?? null}</>
  }

  return <>{children(data)}</>
}

export function Card({
  title,
  description,
  actions,
  children,
}: {
  title?: string
  description?: string
  actions?: ReactNode
  children: ReactNode
}) {
  return (
    <section className="border-border bg-card text-card-foreground relative rounded-lg border p-6 shadow-sm">
      {(title || actions) && (
        <header className="mb-4 flex flex-wrap items-start justify-between gap-3">
          <div>
            {title && <h2 className="text-lg font-semibold tracking-tight">{title}</h2>}
            {description && <p className="text-muted-foreground mt-1 text-sm">{description}</p>}
          </div>
          {actions}
        </header>
      )}
      {children}
    </section>
  )
}

export function Field({
  label,
  htmlFor,
  error,
  hint,
  children,
}: {
  label: string
  htmlFor: string
  error?: string | undefined
  hint?: string | undefined
  children: ReactNode
}) {
  return (
    <div className="space-y-1.5">
      <label htmlFor={htmlFor} className="text-sm font-medium">
        {label}
      </label>
      {children}
      {hint && (
        <p id={`${htmlFor}-hint`} className="text-muted-foreground text-xs">
          {hint}
        </p>
      )}
      {/*
        role="alert" so a screen reader announces the message when it appears: without it the error is
        visually present but silent, and a keyboard user would submit again with no explanation.
      */}
      {error && (
        <p id={`${htmlFor}-error`} role="alert" className="text-destructive text-xs font-medium">
          {error}
        </p>
      )}
    </div>
  )
}

export function Button({
  variant = 'default',
  type = 'button',
  children,
  ...rest
}: React.ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: 'default' | 'outline' | 'ghost' | 'destructive'
}) {
  const variants: Record<string, string> = {
    default: 'bg-primary text-primary-foreground hover:opacity-90',
    outline: 'border border-border bg-transparent hover:bg-accent hover:text-accent-foreground',
    ghost: 'bg-transparent hover:bg-accent hover:text-accent-foreground',
    destructive: 'bg-destructive text-destructive-foreground hover:opacity-90',
  }

  return (
    <button
      type={type}
      className={`inline-flex items-center justify-center gap-2 rounded-md px-4 py-2 text-sm font-medium transition-colors disabled:pointer-events-none disabled:opacity-50 ${variants[variant]}`}
      {...rest}
    >
      {children}
    </button>
  )
}

export function Badge({
  tone = 'neutral',
  children,
}: {
  tone?: 'neutral' | 'success' | 'warning'
  children: ReactNode
}) {
  const tones: Record<string, string> = {
    neutral: 'bg-muted text-muted-foreground',
    success: 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-300',
    warning: 'bg-amber-500/15 text-amber-700 dark:text-amber-300',
  }

  return (
    <span
      className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${tones[tone]}`}
    >
      {children}
    </span>
  )
}

/** A page-level failure with an optional retry, and the trace id when the server supplied one. */
export function ErrorPanel({
  title = 'Something went wrong',
  message,
  traceId,
  onRetry,
  children,
}: {
  title?: string
  message: string
  traceId?: string | undefined
  onRetry?: (() => void) | undefined
  children?: ReactNode
}) {
  return (
    <div role="alert" className="border-destructive/40 bg-destructive/5 rounded-lg border p-6">
      <h2 className="text-destructive text-base font-semibold">{title}</h2>
      <p className="text-muted-foreground mt-2 text-sm">{message}</p>
      {traceId && (
        <p className="text-muted-foreground mt-2 font-mono text-xs">
          Reference: <code>{traceId}</code>
        </p>
      )}
      {onRetry && (
        <div className="mt-4">
          <Button variant="outline" onClick={onRetry}>
            Try again
          </Button>
        </div>
      )}
      {children && <div className="mt-4">{children}</div>}
    </div>
  )
}

export function EmptyState({ title, description }: { title: string; description: string }) {
  return (
    <div className="border-border rounded-lg border border-dashed p-10 text-center">
      <p className="text-sm font-medium">{title}</p>
      <p className="text-muted-foreground mt-1 text-sm">{description}</p>
    </div>
  )
}