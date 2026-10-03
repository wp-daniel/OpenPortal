import { ApiError } from '@/api/client'
import { notify } from '@/hooks/useToast'
import { translate } from '@/i18n/store'
import type { ZodError } from 'zod'

/** First message per field from a zod failure, keyed by the field names the form also uses. */
export function zodFieldErrors<T extends string>(error: ZodError): Partial<Record<T, string>> {
  const result: Partial<Record<T, string>> = {}

  for (const issue of error.issues) {
    const key = issue.path[0] as T

    result[key] ??= issue.message
  }

  return result
}

/**
 * Reports a failed form submission. Field-level problems from a 400 are already rendered under their fields,
 * so they only get a short nudge; everything else shows the server's message.
 */
export function reportFormError(error: unknown, fallback: string): void {
  if (error instanceof ApiError && error.status === 400 && Object.keys(error.fieldErrors).length > 0) {
    notify.warning(translate('clientError.fieldsNeedAttention'))

    return
  }

  notify.fromError(error, fallback)
}

/** Server-reported field errors from a failed mutation, keyed by field name. */
export function serverFieldErrors(error: unknown): Record<string, readonly string[]> {
  return error instanceof ApiError ? error.fieldErrors : {}
}
