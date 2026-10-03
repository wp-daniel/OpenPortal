import { useState } from 'react'
import { ApiError } from '../api/client'

/**
 * An error with an optional retry, for the rare case a component should offer one.
 *
 * Render failures are not always worth retrying: a 404 or a 403 will answer identically every time, so
 * showing a retry button there would be a dead control. Only 5xx and network faults get one.
 */
export function useRetryableError() {
  const [error, setError] = useState<unknown>(null)

  return {
    error,
    /** Replaces any previous failure, because only the latest one describes the current attempt. */
    setError,
    clear: () => setError(null),
    isRetryable: error instanceof ApiError ? error.status >= 500 : error !== null,
  }
}

/** The message to show for a caught error, with a fallback for the non-API cases. */
export function describeError(error: unknown): string {
  if (error instanceof ApiError) {
    return error.summary
  }

  if (error instanceof Error) {
    return error.message
  }

  return 'Something went wrong.'
}

/** The trace id to quote in a bug report, when the server supplied one. */
export function traceIdOf(error: unknown): string | undefined {
  return error instanceof ApiError ? error.traceId : undefined
}