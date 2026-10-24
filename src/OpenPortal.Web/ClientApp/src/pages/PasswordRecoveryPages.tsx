import { useMutation, useQueryClient } from '@tanstack/react-query'
import { CircleCheck, KeyRound, Loader2, MailCheck } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { z } from 'zod'
import { ApiError } from '@/api/client'
import { registrationApi } from '@/api/registration'
import { FormField } from '@/components/FormField'
import { Section } from '@/components/Section'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { SESSION_QUERY_KEY, useSession } from '@/hooks/useSession'
import { useI18n } from '@/i18n/useI18n'
import { reportFormError, serverFieldErrors, zodFieldErrors } from '@/lib/forms'
import { describePasswordPolicy, newPasswordSchema } from '@/lib/passwordPolicy'

const CARD = 'bg-card/80 shadow-lg backdrop-blur'
const PASSWORD_RESET_INVALID = 'identity.password_reset_invalid'

/**
 * "Forgot your password?": asks for the address and says a link is on its way, whatever the address (the portal
 * never reveals whether an account exists).
 */
export function ForgotPasswordPage() {
  const { t } = useI18n()
  const [email, setEmail] = useState('')
  const [error, setError] = useState<string>()

  const request = useMutation({
    mutationFn: () => registrationApi.forgotPassword(email.trim()),
    meta: { handlesErrors: true },
    onError: (failure) => reportFormError(failure, t('passwordReset.requestFailed')),
  })

  function submit(event: FormEvent) {
    event.preventDefault()

    if (!z.email().safeParse(email.trim()).success) {
      setError(t('validation.emailInvalid'))
      return
    }

    setError(undefined)
    request.mutate()
  }

  if (request.isSuccess) {
    return (
      <Section className={CARD}>
        <div className="grid justify-items-center gap-4 py-2 text-center">
          <MailCheck className="text-success size-10" />
          <h1 className="text-xl font-semibold tracking-tight">{t('passwordReset.sent.title')}</h1>
          <p className="text-muted-foreground text-sm">{t('passwordReset.sent.description', { email: email.trim() })}</p>
          <Button asChild variant="outline">
            <Link to="/sign-in">{t('register.backToSignIn')}</Link>
          </Button>
        </div>
      </Section>
    )
  }

  return (
    <Section title={t('passwordReset.forgot.title')} description={t('passwordReset.forgot.description')} className={CARD}>
      <form onSubmit={submit} noValidate className="grid gap-5">
        <FormField label={t('common.email')} htmlFor="forgot-email" error={error ?? serverFieldErrors(request.error).email?.[0]}>
          <Input type="email" autoComplete="username" autoFocus value={email} onChange={(event) => setEmail(event.target.value)} />
        </FormField>
        <Button type="submit" className="w-full" disabled={request.isPending}>
          {request.isPending && <Loader2 className="animate-spin" />}
          {t('passwordReset.forgot.submit')}
        </Button>
        <Button asChild variant="link" className="justify-self-center">
          <Link to="/sign-in">{t('register.backToSignIn')}</Link>
        </Button>
      </form>
    </Section>
  )
}

/**
 * The link of the reset email lands here: a new password, twice. Reachable signed in or not, since the link may
 * be opened on any device; a successful reset signs the account out everywhere, this browser included.
 */
export function ResetPasswordPage() {
  const { t } = useI18n()
  const { session } = useSession()
  const queryClient = useQueryClient()
  const [params] = useSearchParams()
  const userId = params.get('user') ?? ''
  const token = params.get('token') ?? ''
  const policy = session.passwordPolicy
  const [password, setPassword] = useState('')
  const [confirmation, setConfirmation] = useState('')
  const [errors, setErrors] = useState<Partial<Record<'password' | 'confirmation', string>>>({})

  const schema = useMemo(
    () =>
      z
        .object({ password: newPasswordSchema(policy, t, t('validation.newPasswordRequired')), confirmation: z.string() })
        .refine((value) => value.password === value.confirmation, {
          path: ['confirmation'],
          message: t('passwordReset.reset.mismatch'),
        }),
    [policy, t],
  )

  const reset = useMutation({
    mutationFn: (newPassword: string) => registrationApi.resetPassword(userId, token, newPassword),
    meta: { handlesErrors: true },
    onSuccess: async () => {
      // The reset ended every session of the account; a session in this browser is gone too.
      await queryClient.invalidateQueries({ queryKey: SESSION_QUERY_KEY })
    },
    onError: (failure) => {
      if (!(failure instanceof ApiError && failure.problem?.errorCode === PASSWORD_RESET_INVALID)) {
        reportFormError(failure, t('passwordReset.reset.failed'))
      }
    },
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    const parsed = schema.safeParse({ password, confirmation })

    if (!parsed.success) {
      setErrors(zodFieldErrors<'password' | 'confirmation'>(parsed.error))
      return
    }

    setErrors({})
    reset.mutate(parsed.data.password)
  }

  const invalidLink =
    !userId || !token || (reset.error instanceof ApiError && reset.error.problem?.errorCode === PASSWORD_RESET_INVALID)

  if (invalidLink) {
    return (
      <Section className={CARD}>
        <div className="grid justify-items-center gap-4 py-2 text-center">
          <KeyRound className="text-muted-foreground size-10" />
          <h1 className="text-xl font-semibold tracking-tight">{t('passwordReset.invalid.title')}</h1>
          <p className="text-muted-foreground text-sm">{t('passwordReset.invalid.description')}</p>
          <Button asChild>
            <Link to="/forgot-password">{t('passwordReset.invalid.again')}</Link>
          </Button>
        </div>
      </Section>
    )
  }

  if (reset.isSuccess) {
    return (
      <Section className={CARD}>
        <div className="grid justify-items-center gap-4 py-2 text-center">
          <CircleCheck className="text-success size-10" />
          <h1 className="text-xl font-semibold tracking-tight">{t('passwordReset.done.title')}</h1>
          <p className="text-muted-foreground text-sm">{t('passwordReset.done.description')}</p>
          <Button asChild>
            <Link to="/sign-in">{t('passwordReset.done.signIn')}</Link>
          </Button>
        </div>
      </Section>
    )
  }

  const server = serverFieldErrors(reset.error)

  return (
    <Section title={t('passwordReset.reset.title')} description={t('passwordReset.reset.description')} className={CARD}>
      <form onSubmit={submit} noValidate className="grid gap-5">
        <FormField
          label={t('passwordReset.reset.password')}
          htmlFor="reset-password"
          hint={describePasswordPolicy(policy, t)}
          error={errors.password ?? server.newPassword?.[0]}
        >
          <Input type="password" autoComplete="new-password" autoFocus value={password} onChange={(event) => setPassword(event.target.value)} />
        </FormField>
        <FormField label={t('passwordReset.reset.confirmation')} htmlFor="reset-confirmation" error={errors.confirmation}>
          <Input type="password" autoComplete="new-password" value={confirmation} onChange={(event) => setConfirmation(event.target.value)} />
        </FormField>
        <Button type="submit" className="w-full" disabled={reset.isPending}>
          {reset.isPending && <Loader2 className="animate-spin" />}
          {t('passwordReset.reset.submit')}
        </Button>
      </form>
    </Section>
  )
}
