import { z } from 'zod'
import type { PasswordPolicy } from '../api/types'
import { formatList, type TFunction } from '../i18n/store'

/**
 * The stand-in used before the session has loaded, or when the session request failed.
 *
 * It is deliberately not a plausible-looking policy: `requiredLength: 0` with every requirement off would be
 * indistinguishable from a genuinely permissive configuration, and a form that "helpfully" enforced it could
 * reject a password the server would have accepted. Reference comparison against this exact object is how
 * the code below tells "unknown" from "known to be unrestricted".
 *
 * Frozen and module-level so the comparison stays a cheap identity check that survives re-renders.
 */
export const UNAVAILABLE_PASSWORD_POLICY: PasswordPolicy = Object.freeze({
  requiredLength: 0,
  requiredUniqueChars: 0,
  requireLowercase: false,
  requireUppercase: false,
  requireDigit: false,
  requireNonAlphanumeric: false,
})

/** Whether the server has actually published its policy, as opposed to this placeholder standing in for it. */
export function isPasswordPolicyKnown(policy: PasswordPolicy): boolean {
  return policy !== UNAVAILABLE_PASSWORD_POLICY
}

/**
 * A Zod schema for a *new* password, built from the policy the server published.
 *
 * The point of taking the policy as input rather than hardcoding rules is that there is then no second copy
 * to drift: these checks are a courtesy that saves a round trip, and the user store remains the only thing
 * that actually decides.
 *
 * The character-class regexes are a deliberate approximation of Identity's Unicode-aware checks. Tightening
 * them to match exactly would be pointless work, since anything they wrongly reject is still accepted by the
 * server on the next attempt — whereas a check the server would have rejected is caught here for free.
 */
export function newPasswordSchema(policy: PasswordPolicy, t: TFunction, label: string) {
  let schema = z.string().min(1, label)

  if (!isPasswordPolicyKnown(policy)) {
    // Unknown policy: presence is all that can honestly be checked. The server answers with the real
    // requirement, and the form renders that in its error summary.
    return schema
  }

  if (policy.requiredLength > 1) {
    schema = schema.min(policy.requiredLength, t('password.min', { count: policy.requiredLength }))
  }

  if (policy.requireLowercase) {
    schema = schema.regex(/[a-z]/, t('password.lowercase'))
  }

  if (policy.requireUppercase) {
    schema = schema.regex(/[A-Z]/, t('password.uppercase'))
  }

  if (policy.requireDigit) {
    schema = schema.regex(/[0-9]/, t('password.digit'))
  }

  if (policy.requireNonAlphanumeric) {
    schema = schema.regex(/[^A-Za-z0-9]/, t('password.symbol'))
  }

  if (policy.requiredUniqueChars > 1) {
    schema = schema.refine(
      (value) => new Set(value).size >= policy.requiredUniqueChars,
      t('password.unique', { count: policy.requiredUniqueChars }),
    )
  }

  return schema
}

/** The same policy in a sentence, for the field hint. */
export function describePasswordPolicy(policy: PasswordPolicy, t: TFunction): string {
  if (!isPasswordPolicyKnown(policy)) {
    return t('password.policyUnknown')
  }

  const parts: string[] = []

  if (policy.requiredLength > 1) {
    parts.push(t('password.part.length', { count: policy.requiredLength }))
  }

  if (policy.requireLowercase) {
    parts.push(t('password.part.lowercase'))
  }

  if (policy.requireUppercase) {
    parts.push(t('password.part.uppercase'))
  }

  if (policy.requireDigit) {
    parts.push(t('password.part.digit'))
  }

  if (policy.requireNonAlphanumeric) {
    parts.push(t('password.part.symbol'))
  }

  if (policy.requiredUniqueChars > 1) {
    parts.push(t('password.part.unique', { count: policy.requiredUniqueChars }))
  }

  // Joined with Intl.ListFormat, so "and" is the active language's own conjunction.
  return parts.length === 0 ? t('password.policyUnknown') : t('password.requires', { list: formatList(parts) })
}
