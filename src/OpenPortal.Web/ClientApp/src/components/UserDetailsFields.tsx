import { useQuery } from '@tanstack/react-query'
import { useEffect, useRef, type ReactNode } from 'react'
import { geoApi, geoKeys } from '@/api/geo'
import { CityCombobox } from '@/components/CityCombobox'
import { CountryCombobox } from '@/components/CountryCombobox'
import { FormField } from '@/components/FormField'
import { GeoAttribution } from '@/components/GeoAttribution'
import { Input } from '@/components/ui/input'
import { Separator } from '@/components/ui/separator'
import { useDebounced } from '@/hooks/useDebounced'
import { useI18n } from '@/i18n/useI18n'
import { isCountryCode } from '@/lib/countries'
import type { UserDetailsField, UserDetailsForm, UserDetailsGroup } from '@/lib/userDetails'

const ALL_GROUPS: readonly UserDetailsGroup[] = ['personal', 'work', 'address']

type FieldErrors = Partial<Record<UserDetailsField, string | undefined>>

/**
 * The personal, work and address fields of an account, in three titled groups. Used by the administrator's
 * create/edit dialog and by the user's own account page so both ask for exactly the same things. `groups`
 * limits it to some of them, for forms that spread the groups over tabs.
 */
export function UserDetailsFields({
  idPrefix,
  values,
  onChange,
  errors,
  groups = ALL_GROUPS,
}: {
  idPrefix: string
  values: UserDetailsForm
  onChange: (field: UserDetailsField, value: string) => void
  errors: FieldErrors
  groups?: readonly UserDetailsGroup[]
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
      {groups.includes('personal') && (
        <FieldGroup title={t('profile.section.personal')}>
          {field('firstName', t('common.firstName'), { autoComplete: 'given-name', required: true })}
          {field('lastName', t('common.lastName'), { autoComplete: 'family-name', required: true })}
          {field('phoneNumber', t('common.phone'), { autoComplete: 'tel', type: 'tel' })}
        </FieldGroup>
      )}

      {groups.includes('work') && (
        <FieldGroup title={t('profile.section.work')}>
          {field('company', t('common.company'), { autoComplete: 'organization' })}
          {field('department', t('common.department'))}
          {field('jobTitle', t('common.jobTitle'), { autoComplete: 'organization-title' })}
        </FieldGroup>
      )}

      {groups.includes('address') && (
        <FieldGroup title={t('profile.section.address')}>
          <AddressFields idPrefix={idPrefix} values={values} onChange={onChange} errors={errors} />
        </FieldGroup>
      )}
    </>
  )
}

/**
 * Country first, because it scopes the rest: the postal code is looked up within it (filling the city when
 * the code names exactly one place, and listing the places when it names several), and the city picker
 * searches only its towns. Nothing typed is ever overwritten: the city is only filled while it is empty.
 */
function AddressFields({
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
  const country = isCountryCode(values.country) ? values.country.toUpperCase() : null
  const postalCode = useDebounced(values.postalCode.trim().toUpperCase(), 400)
  // The lookup a city was filled from, so clearing the city afterwards is respected.
  const filledFrom = useRef<string | null>(null)

  const lookup = useQuery({
    queryKey: geoKeys.postalCode(country ?? '', postalCode),
    queryFn: ({ signal }) => geoApi.postalCode(country!, postalCode, signal),
    enabled: country !== null && postalCode.length >= 3,
    staleTime: Infinity,
    retry: false,
  })

  const lookedUp = country !== null && postalCode.length >= 3 ? lookup.data : undefined
  const places = lookedUp?.places ?? []
  const lookupKey = `${country}:${postalCode}`

  const onlyCity = places.length === 1 ? places[0].city : null

  useEffect(() => {
    if (onlyCity !== null && values.city.trim() === '' && filledFrom.current !== lookupKey) {
      filledFrom.current = lookupKey
      onChange('city', onlyCity)
    }
  }, [onlyCity, values.city, lookupKey, onChange])

  let postalHint: string | undefined
  if (lookup.isFetching) {
    postalHint = t('address.postalCode.looking')
  } else if (places.length === 1) {
    postalHint = [places[0].city, places[0].region].filter(Boolean).join(' · ')
  } else if (places.length > 1) {
    postalHint = t('address.postalCode.many', { count: places.length })
  } else if (lookedUp?.available && postalCode.length >= 3) {
    postalHint = t('address.postalCode.unknown')
  }

  return (
    <>
      <FormField label={t('common.country')} htmlFor={`${idPrefix}-country`} error={errors.country} className="sm:col-span-2">
        <CountryCombobox value={values.country} onChange={(code) => onChange('country', code)} />
      </FormField>

      <FormField label={t('common.address')} htmlFor={`${idPrefix}-addressLine`} error={errors.addressLine} className="sm:col-span-2">
        <Input
          autoComplete="street-address"
          value={values.addressLine}
          onChange={(event) => onChange('addressLine', event.target.value)}
        />
      </FormField>

      <FormField
        label={t('common.postalCode')}
        htmlFor={`${idPrefix}-postalCode`}
        error={errors.postalCode}
        hint={postalHint}
      >
        <Input
          autoComplete="postal-code"
          value={values.postalCode}
          onChange={(event) => onChange('postalCode', event.target.value)}
        />
      </FormField>

      <FormField label={t('common.city')} htmlFor={`${idPrefix}-city`} error={errors.city}>
        <CityCombobox
          value={values.city}
          country={values.country}
          postalCode={postalCode}
          postalPlaces={places}
          onChange={(city, place) => {
            onChange('city', city)

            if (place?.postalCode && values.postalCode.trim() === '') {
              onChange('postalCode', place.postalCode)
            }
          }}
        />
      </FormField>

      <GeoAttribution sources={places.length > 0 ? ['geonames'] : []} className="-mt-2 sm:col-span-2" />
    </>
  )
}

/**
 * A titled group of fields. The legend is floated: a rendered legend sits in the fieldset's border and is
 * not a grid item, so `gap` would never separate it from the first row of fields.
 */
function FieldGroup({ title, children }: { title: string; children: ReactNode }) {
  return (
    <fieldset className="grid gap-4">
      <legend className="float-left w-full text-sm font-semibold">{title}</legend>
      <Separator />
      <div className="grid items-start gap-5 sm:grid-cols-2">{children}</div>
    </fieldset>
  )
}
