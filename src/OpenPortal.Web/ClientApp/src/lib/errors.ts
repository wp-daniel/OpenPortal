import { ApiError } from '@/api/client'

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