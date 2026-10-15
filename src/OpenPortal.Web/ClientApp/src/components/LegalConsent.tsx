import { useQueries } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { legalApi, legalKeys } from '@/api/legal'
import type { LegalDocumentKind, PublishedLegalDocument } from '@/api/types'
import { Checkbox } from '@/components/ui/checkbox'
import { useI18n } from '@/i18n/useI18n'

const KINDS: readonly LegalDocumentKind[] = ['privacy', 'terms']

/**
 * The versions of the privacy notice and the terms of use a new account must accept (those in force that require
 * it), and whether the person ticked the box. The form sends `documentIds` with the registration.
 */
export function useLegalConsent() {
  const { language } = useI18n()
  const [accepted, setAccepted] = useState(false)

  const results = useQueries({
    queries: KINDS.map((kind) => ({
      queryKey: legalKeys.current(kind, language),
      queryFn: ({ signal }: { signal: AbortSignal }) => legalApi.current(kind, signal),
      staleTime: 60_000,
    })),
  })

  const required = results
    .map((result) => result.data)
    .filter((document): document is PublishedLegalDocument => !!document && document.requiresAcceptance)

  return {
    required,
    documentIds: required.map((document) => document.id),
    accepted,
    setAccepted,
    /** True when nothing needs accepting, or the box is ticked. */
    satisfied: required.length === 0 || accepted,
    isPending: results.some((result) => result.isPending),
  }
}

/** The checkbox, linking each document (in a new tab, so the form keeps what was typed). */
export function LegalConsent({
  consent,
  error,
}: {
  consent: ReturnType<typeof useLegalConsent>
  error?: string | undefined
}) {
  const { t } = useI18n()

  if (consent.required.length === 0) {
    return null
  }

  return (
    <div className="grid gap-1.5">
      <div className="flex items-start gap-3">
        <Checkbox
          id="legal-consent"
          className="mt-0.5"
          checked={consent.accepted}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? 'legal-consent-error' : undefined}
          onCheckedChange={(checked) => consent.setAccepted(checked === true)}
        />
        <label htmlFor="legal-consent" className="text-sm leading-snug">
          {t('legal.consent.prefix')}{' '}
          {consent.required.map((document, index) => (
            <span key={document.id}>
              {index > 0 && ` ${t('legal.consent.and')} `}
              <Link to={`/${document.kind}`} target="_blank" className="underline underline-offset-2">
                {document.title}
              </Link>
            </span>
          ))}
        </label>
      </div>
      {error && (
        <p id="legal-consent-error" className="text-destructive text-xs font-medium">
          {error}
        </p>
      )}
    </div>
  )
}
