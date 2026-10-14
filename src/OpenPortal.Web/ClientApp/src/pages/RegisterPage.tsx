import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Clock, Loader2, MailCheck } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { z } from 'zod'
import { ApiError } from '@/api/client'
import { registrationApi } from '@/api/registration'
import type { RegistrationOutcome } from '@/api/types'
import { FormField } from '@/components/FormField'
import { Section } from '@/components/Section'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { SESSION_QUERY_KEY, useSession } from '@/hooks/useSession'
import { useI18n } from '@/i18n/useI18n'
import { reportFormError, serverFieldErrors, zodFieldErrors } from '@/lib/forms'
import { describePasswordPolicy, newPasswordSchema } from '@/lib/passwordPolicy'

type Field = 'firstName' | 'lastName' | 'email' | 'password'

const CARD = 'bg-card/80 shadow-lg backdrop-blur'

/**
 * Creating an account, when the portal allows it. What happens next is the server's call (its settings): the new
 * account is signed in at once, waits for its address to be confirmed, or waits for an administrator.
 */
export function RegisterPage() {
  const { session } = useSession()
  const [outcome, setOutcome] = useState<RegistrationOutcome | null>(null)

  if (!session.registration.enabled && outcome === null) {
    return <Navigate to="/sign-in" replace />
  }

  return outcome === null || outcome === 'signed_in' ? (
    <RegisterForm onRegistered={setOutcome} />
  ) : (
    <RegistrationOutcomePanel outcome={outcome} />
  )
}

function RegisterForm({ onRegistered }: { onRegistered: (outcome: RegistrationOutcome) => void }) {
  const { t } = useI18n()
  const { session } = useSession()
  const queryClient = useQueryClient()
  const policy = session.passwordPolicy
  const [fields, setFields] = useState<Record<Field, string>>({ firstName: '', lastName: '', email: '', password: '' })
  const [errors, setErrors] = useState<Partial<Record<Field, string>>>({})

  const schema = useMemo(
    () =>
      z.object({
        firstName: z.string().trim().min(1, t('validation.firstNameRequired')).max(60, t('validation.maxLength', { max: 60 })),
        lastName: z.string().trim().min(1, t('validation.lastNameRequired')).max(60, t('validation.maxLength', { max: 60 })),
        email: z.string().trim().min(1, t('validation.emailRequired')).email(t('validation.emailInvalid')),
        password: newPasswordSchema(policy, t, t('validation.passwordRequired')),
      }),
    [policy, t],
  )

  const register = useMutation({
    mutationFn: () =>
      registrationApi.register({
        firstName: fields.firstName.trim(),
        lastName: fields.lastName.trim(),
        email: fields.email.trim(),
        password: fields.password,
      }),
    meta: { handlesErrors: true },
    onSuccess: async (result) => {
      onRegistered(result.outcome)

      // Signed in at once: the session changes and the public-only route takes the user in.
      if (result.outcome === 'signed_in') {
        await queryClient.invalidateQueries({ queryKey: SESSION_QUERY_KEY })
      }
    },
    onError: (failure) => reportFormError(failure, t('register.failed')),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    const parsed = schema.safeParse(fields)

    if (!parsed.success) {
      setErrors(zodFieldErrors<Field>(parsed.error))
      return
    }

    setErrors({})
    register.mutate()
  }

  const server = serverFieldErrors(register.error)
  const set = (field: Field) => (value: string) => setFields((current) => ({ ...current, [field]: value }))

  return (
    <Section
      title={t('register.title')}
      description={session.registration.requiresApproval ? t('register.descriptionApproval') : t('register.description')}
      className={CARD}
    >
      <form onSubmit={submit} noValidate className="grid items-start gap-5">
        <div className="grid items-start gap-5 sm:grid-cols-2">
          <FormField label={t('common.firstName')} htmlFor="firstName" error={errors.firstName ?? server.firstName?.[0]}>
            <Input autoComplete="given-name" value={fields.firstName} onChange={(event) => set('firstName')(event.target.value)} />
          </FormField>
          <FormField label={t('common.lastName')} htmlFor="lastName" error={errors.lastName ?? server.lastName?.[0]}>
            <Input autoComplete="family-name" value={fields.lastName} onChange={(event) => set('lastName')(event.target.value)} />
          </FormField>
        </div>

        <FormField label={t('common.email')} htmlFor="email" error={errors.email ?? server.email?.[0]}>
          <Input type="email" autoComplete="email" value={fields.email} onChange={(event) => set('email')(event.target.value)} />
        </FormField>

        <FormField
          label={t('common.password')}
          htmlFor="password"
          hint={describePasswordPolicy(policy, t)}
          error={errors.password ?? server.password?.[0]}
        >
          <Input
            type="password"
            autoComplete="new-password"
            value={fields.password}
            onChange={(event) => set('password')(event.target.value)}
          />
        </FormField>

        <Button type="submit" className="w-full" disabled={register.isPending}>
          {register.isPending && <Loader2 className="animate-spin" />}
          {t('register.submit')}
        </Button>

        {register.error instanceof ApiError && register.error.problem?.errorCode === 'identity.registration_closed' && (
          <p className="text-destructive text-sm">{t('register.closed')}</p>
        )}

        <p className="text-muted-foreground text-center text-sm">
          {t('register.haveAccount')}{' '}
          <Link to="/sign-in" className="text-foreground underline underline-offset-2">
            {t('register.signIn')}
          </Link>
        </p>
      </form>
    </Section>
  )
}

/** What happens next, after a registration that did not sign the user in. */
function RegistrationOutcomePanel({ outcome }: { outcome: RegistrationOutcome }) {
  const { t } = useI18n()
  const confirmation = outcome === 'confirmation_sent'
  const Icon = confirmation ? MailCheck : Clock

  return (
    <Section className={CARD}>
      <div className="grid justify-items-center gap-4 py-2 text-center">
        <Icon className="text-muted-foreground size-10" />
        <h1 className="text-xl font-semibold tracking-tight">
          {confirmation ? t('register.checkEmail.title') : t('register.pending.title')}
        </h1>
        <p className="text-muted-foreground text-sm">
          {confirmation ? t('register.checkEmail.description') : t('register.pending.description')}
        </p>
        <Button asChild variant="outline">
          <Link to="/sign-in">{t('register.backToSignIn')}</Link>
        </Button>
      </div>
    </Section>
  )
}
