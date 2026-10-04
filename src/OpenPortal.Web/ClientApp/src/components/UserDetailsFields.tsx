import type { ReactNode } from 'react'
import { FormField } from '@/components/FormField'
import { Input } from '@/components/ui/input'
import { useI18n } from '@/i18n/useI18n'
import type { UserDetailsField, UserDetailsForm } from '@/lib/userDetails'

type FieldErrors = Partial<Record<UserDetailsField, string | undefined>>

/**
 * The personal, work and address fields of an account, in three titled groups. Used by the administrator's
 * create/edit dialog and by the user's own account page so both ask for exactly the same things.
 */
export function UserDetailsFields({
  idPrefix,
  values,
  onChange,
  errors,
}: {
  idPrefix: string
  values: UserDetailsForm
  onChange: (field: UserDetailsField, value: string) => void
  errors: FieldErrors
}) {
  const { t } = useI18n()

  const field = (
    name: UserDetailsField,
    label: string,
    props: { autoComplete?: string; type?: string; required?: boolean } = {},
  ) => (
    <FormField label={label} htmlFor={`${idPrefix}-${name}`} error={errors[name]}>
      <Input
        type={props.type}
        autoComplete={props.autoComplete}
        required={props.required}
        value={values[name]}
        onChange={(event) => onChange(name, event.target.value)}
      />
    </FormField>
  )

  return (
    <>
      <FieldGroup title={t('profile.section.personal')}>
        {field('firstName', t('common.firstName'), { autoComplete: 'given-name', required: true })}
        {field('lastName', t('common.lastName'), { autoComplete: 'family-name', required: true })}
        {field('phoneNumber', t('common.phone'), { autoComplete: 'tel', type: 'tel' })}
      </FieldGroup>

      <FieldGroup title={t('profile.section.work')}>
        {field('company', t('common.company'), { autoComplete: 'organization' })}
        {field('department', t('common.department'))}
        {field('jobTitle', t('common.jobTitle'), { autoComplete: 'organization-title' })}
      </FieldGroup>

      <FieldGroup title={t('profile.section.address')}>
        <div className="sm:col-span-2">{field('addressLine', t('common.address'), { autoComplete: 'street-address' })}</div>
        {field('city', t('common.city'), { autoComplete: 'address-level2' })}
        {field('postalCode', t('common.postalCode'), { autoComplete: 'postal-code' })}
        {field('country', t('common.country'), { autoComplete: 'country-name' })}
      </FieldGroup>
    </>
  )
}

function FieldGroup({ title, children }: { title: string; children: ReactNode }) {
  return (
    <fieldset className="grid gap-3">
      <legend className="text-sm font-medium">{title}</legend>
      <div className="grid items-start gap-5 sm:grid-cols-2">{children}</div>
    </fieldset>
  )
}
