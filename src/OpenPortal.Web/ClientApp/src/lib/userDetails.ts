import { z } from 'zod'
import type { UserDetails } from '@/api/types'
import type { TFunction } from '@/i18n/store'

/** Limits mirrored from the server's `ApplicationUser`; the server stays authoritative. */
export const NAME_MAX = 60
export const DETAIL_MAX = 100
export const PHONE_MAX = 30

/** The text-box state of the details fields: every value is a string, blank meaning "not given". */
export interface UserDetailsForm {
  firstName: string
  lastName: string
  phoneNumber: string
  jobTitle: string
  company: string
  department: string
  addressLine: string
  city: string
  postalCode: string
  country: string
}

export type UserDetailsField = keyof UserDetailsForm

export const EMPTY_DETAILS: UserDetailsForm = {
  firstName: '',
  lastName: '',
  phoneNumber: '',
  jobTitle: '',
  company: '',
  department: '',
  addressLine: '',
  city: '',
  postalCode: '',
  country: '',
}

export function detailsToForm(details: UserDetails): UserDetailsForm {
  return {
    firstName: details.firstName,
    lastName: details.lastName,
    phoneNumber: details.phoneNumber ?? '',
    jobTitle: details.jobTitle ?? '',
    company: details.company ?? '',
    department: details.department ?? '',
    addressLine: details.addressLine ?? '',
    city: details.city ?? '',
    postalCode: details.postalCode ?? '',
    country: details.country ?? '',
  }
}

/** Trims every value and sends blank optional fields as null, as the server expects. */
export function formToDetails(form: UserDetailsForm): UserDetails {
  const blankToNull = (value: string) => (value.trim() === '' ? null : value.trim())

  return {
    firstName: form.firstName.trim(),
    lastName: form.lastName.trim(),
    phoneNumber: blankToNull(form.phoneNumber),
    jobTitle: blankToNull(form.jobTitle),
    company: blankToNull(form.company),
    department: blankToNull(form.department),
    addressLine: blankToNull(form.addressLine),
    city: blankToNull(form.city),
    postalCode: blankToNull(form.postalCode),
    country: blankToNull(form.country),
  }
}

/** Digits with an optional leading +; spaces, dots, dashes and brackets are tolerated. Same rule as the server. */
export function isWellFormedPhone(value: string): boolean {
  if (value.length > PHONE_MAX || !/^\+?[\d\s().-]+$/.test(value)) {
    return false
  }

  const digits = value.replace(/\D/g, '').length

  return digits >= 6 && digits <= 15
}

/** The zod shape for the details fields, to spread into a form's own `z.object`. */
export function userDetailsShape(t: TFunction) {
  const optional = (max: number) => z.string().trim().max(max, t('validation.maxLength', { max }))

  return {
    firstName: z
      .string()
      .trim()
      .min(1, t('validation.firstNameRequired'))
      .max(NAME_MAX, t('validation.maxLength', { max: NAME_MAX })),
    lastName: z
      .string()
      .trim()
      .min(1, t('validation.lastNameRequired'))
      .max(NAME_MAX, t('validation.maxLength', { max: NAME_MAX })),
    phoneNumber: z
      .string()
      .trim()
      .refine((value) => value === '' || isWellFormedPhone(value), t('validation.phoneInvalid')),
    jobTitle: optional(DETAIL_MAX),
    company: optional(DETAIL_MAX),
    department: optional(DETAIL_MAX),
    addressLine: optional(DETAIL_MAX),
    city: optional(DETAIL_MAX),
    postalCode: optional(DETAIL_MAX),
    country: optional(DETAIL_MAX),
  }
}
