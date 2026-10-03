import { z } from 'zod'
import type { PasswordPolicy } from '../api/types'

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
export function newPasswordSchema(policy: PasswordPolicy, label = 'Enter a password.') {
  let schema = z.string().min(1, label)

  if (!isPasswordPolicyKnown(policy)) {
    // Unknown policy: presence is all that can honestly be checked. The server answers with the real
    // requirement, and the form renders that in its error summary.
    return schema
  }

  if (policy.requiredLength > 1) {
    schema = schema.min(policy.requiredLength, `Use at least ${policy.requiredLength} characters.`)
  }

  if (policy.requireLowercase) {
    schema = schema.regex(/[a-z]/, 'Add a lowercase letter.')
  }

  if (policy.requireUppercase) {
    schema = schema.regex(/[A-Z]/, 'Add an uppercase letter.')
  }

  if (policy.requireDigit) {
    schema = schema.regex(/[0-9]/, 'Add a digit.')
  }

  if (policy.requireNonAlphanumeric) {
    schema = schema.regex(/[^A-Za-z0-9]/, 'Add a symbol.')
  }

  if (policy.requiredUniqueChars > 1) {
    schema = schema.refine(
      (value) => new Set(value).size >= policy.requiredUniqueChars,
      `Use at least ${policy.requiredUniqueChars} different characters.`,
    )
  }

  return schema
}

/** Joins items as "a, b and c" so the generated hint reads like a sentence. */
function joinWithAnd(parts: readonly string[]): string {
  if (parts.length === 0) {
    return ''
  }

  if (parts.length === 1) {
    return parts[0]
  }

  return `${parts.slice(0, -1).join(', ')} and ${parts[parts.length - 1]}`
}

/** The same policy in a sentence, for the field hint. */
export function describePasswordPolicy(policy: PasswordPolicy): string {
  if (!isPasswordPolicyKnown(policy)) {
    return 'The server publishes the complexity policy.'
  }

  const parts: string[] = []

  if (policy.requiredLength > 1) {
    parts.push(`at least ${policy.requiredLength} characters`)
  }

  if (policy.requireLowercase) {
    parts.push('a lowercase letter')
  }

  if (policy.requireUppercase) {
    parts.push('an uppercase letter')
  }

  if (policy.requireDigit) {
    parts.push('a digit')
  }

  if (policy.requireNonAlphanumeric) {
    parts.push('a symbol')
  }

  if (policy.requiredUniqueChars > 1) {
    parts.push(`${policy.requiredUniqueChars} different characters`)
  }

  return parts.length === 0 ? 'The server publishes the complexity policy.' : `Requires ${joinWithAnd(parts)}.`
}
