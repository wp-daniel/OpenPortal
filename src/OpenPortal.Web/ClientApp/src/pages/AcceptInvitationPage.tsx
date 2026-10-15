import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2, MailOpen, MailX } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { z } from 'zod'
import { ApiError } from '@/api/client'
import { registrationApi } from '@/api/registration'
import type { InvitationPreview } from '@/api/types'
import { FormField } from '@/components/FormField'
import { LegalConsent, useLegalConsent } from '@/components/LegalConsent'
import { Section } from '@/components/Section'
import { LoadingState } from '@/components/StatePanels'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { SESSION_QUERY_KEY, useSession } from '@/hooks/useSession'
import { formatDate } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { reportFormError, serverFieldErrors, zodFieldErrors } from '@/lib/forms'
import { describePasswordPolicy, newPasswordSchema } from '@/lib/passwordPolicy'

const CARD = 'bg-card/80 shadow-lg backdrop-blur'

type Field = 'firstName' | 'lastName' | 'password'

/**
 * The link of an invitation lands here: the person sees who invited them, picks their name and a password, and is
 * signed in. The address is the invited one and cannot change; receiving the link confirmed it.
 */
export function AcceptInvitationPage() {
  const { t } = useI18n()
  const [params] = useSearchParams()
  const token = params.get('token') ?? ''

  const preview = useQuery({
    queryKey: ['invitation-preview', token],
    queryFn: () => registrationApi.previewInvitation(token),
    enabled: token !== '',
    retry: false,
    staleTime: Number.POSITIVE_INFINITY,
  })

  if (!token || preview.isError) {
    const expired = preview.error instanceof ApiError && preview.error.problem?.errorCode === 'identity.invitation_expired'

    return (
      <Section className={CARD}>
        <div className="grid justify-items-center gap-4 py-2 text-center">
          <MailX className="text-muted-foreground size-10" />
          <h1 className="text-xl font-semibold tracking-tight">
            {expired ? t('invitation.expired.title') : t('invitation.invalid.title')}
          </h1>
          <p className="text-muted-foreground text-sm">
            {expired ? t('invitation.expired.description') : t('invitation.invalid.description')}
          </p>
          <Button asChild variant="outline">
            <Link to="/sign-in">{t('register.backToSignIn')}</Link>
          </Button>
        </div>
      </Section>
    )
  }

  if (!preview.data) {
    return (
      <Section className={CARD}>
        <LoadingState />
      </Section>
    )
  }

  return <AcceptForm token={token} invitation={preview.data} />
}

function AcceptForm({ token, invitation }: { token: string; invitation: InvitationPreview }) {
  const { t } = useI18n()
  const { session } = useSession()
  const queryClient = useQueryClient()
  const policy = session.passwordPolicy
  const [fields, setFields] = useState<Record<Field, string>>({
    firstName: invitation.firstName ?? '',
    lastName: invitation.lastName ?? '',
    password: '',
  })
  const [errors, setErrors] = useState<Partial<Record<Field, string>>>({})
  const consent = useLegalConsent()
  const [consentError, setConsentError] = useState<string>()

  const schema = useMemo(
    () =>
      z.object({
        firstName: z.string().trim().min(1, t('validation.firstNameRequired')).max(60, t('validation.maxLength', { max: 60 })),
        lastName: z.string().trim().min(1, t('validation.lastNameRequired')).max(60, t('validation.maxLength', { max: 60 })),
        password: newPasswordSchema(policy, t, t('validation.passwordRequired')),
      }),
    [policy, t],
  )

  const accept = useMutation({
    mutationFn: () =>
      registrationApi.acceptInvitation({
        token,
        firstName: fields.firstName.trim(),
        lastName: fields.lastName.trim(),
        password: fields.password,
        acceptedLegalDocumentIds: consent.documentIds,
      }),
    meta: { handlesErrors: true },
    // Signed in: the public-only route takes the new account to the dashboard.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: SESSION_QUERY_KEY }),
    onError: (failure) => reportFormError(failure, t('invitation.failed')),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    const parsed = schema.safeParse(fields)

    const consentMissing = !consent.satisfied
    setConsentError(consentMissing ? t('legal.consent.required') : undefined)

    if (!parsed.success) {
      setErrors(zodFieldErrors<Field>(parsed.error))
      return
    }

    setErrors({})
    if (!consentMissing) {
      accept.mutate()
    }
  }

  const server = serverFieldErrors(accept.error)
  const set = (field: Field) => (value: string) => setFields((current) => ({ ...current, [field]: value }))

  return (
    <Section
      title={
        <span className="flex items-center gap-2">
          <MailOpen className="size-5" />
          {t('invitation.title')}
        </span>
      }
      description={
        invitation.invitedByName
          ? t('invitation.descriptionBy', { name: invitation.invitedByName })
          : t('invitation.description')
      }
      className={CARD}
    >
      <form onSubmit={submit} noValidate className="grid items-start gap-5">
        {invitation.message && (
          <blockquote className="text-muted-foreground border-l-2 pl-3 text-sm italic">{invitation.message}</blockquote>
        )}

        <FormField label={t('common.email')} htmlFor="invitation-email" hint={t('invitation.emailHint')}>
          <Input value={invitation.email} readOnly className="font-mono" autoComplete="username" />
        </FormField>

        <div className="grid items-start gap-5 sm:grid-cols-2">
          <FormField label={t('common.firstName')} htmlFor="firstName" error={errors.firstName ?? server.firstName?.[0]}>
            <Input autoComplete="given-name" value={fields.firstName} onChange={(event) => set('firstName')(event.target.value)} />
          </FormField>
          <FormField label={t('common.lastName')} htmlFor="lastName" error={errors.lastName ?? server.lastName?.[0]}>
            <Input autoComplete="family-name" value={fields.lastName} onChange={(event) => set('lastName')(event.target.value)} />
          </FormField>
        </div>

        <FormField
          label={t('common.password')}
          htmlFor="password"
          hint={describePasswordPolicy(policy, t)}
          error={errors.password ?? server.password?.[0]}
        >
          <Input type="password" autoComplete="new-password" value={fields.password} onChange={(event) => set('password')(event.target.value)} />
        </FormField>

        <LegalConsent consent={consent} error={consentError} />

        <Button type="submit" className="w-full" disabled={accept.isPending || consent.isPending}>
          {accept.isPending && <Loader2 className="animate-spin" />}
          {t('invitation.submit')}
        </Button>

        <p className="text-muted-foreground text-center text-xs">
          {t('invitation.expires', { date: formatDate(invitation.expiresAtUtc, { dateStyle: 'long' }) })}
        </p>
      </form>
    </Section>
  )
}
