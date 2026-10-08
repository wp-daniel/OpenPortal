import { useMutation, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Loader2, Smartphone } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { z } from 'zod'
import { authApi, TWO_FACTOR_REQUIRED, TWO_FACTOR_SESSION_EXPIRED } from '@/api/auth'
import { ApiError } from '@/api/client'
import { FormField } from '@/components/FormField'
import { OTP_LENGTH, OtpCodeInput } from '@/components/OtpCodeInput'
import { Section } from '@/components/Section'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { SESSION_QUERY_KEY } from '@/hooks/useSession'
import { notify } from '@/hooks/useToast'
import { useI18n } from '@/i18n/useI18n'
import type { TFunction } from '@/i18n/store'
import { zodFieldErrors } from '@/lib/forms'

/**
 * Sign-in.
 *
 * The schema checks only what the client can judge. Password complexity is deliberately not enforced here:
 * the authoritative policy is published by the server and enforced by the user store, and a client copy would
 * either drift or reject passwords the server would have accepted.
 *
 * An account with an authenticator app gets a second step: the server answers the password with
 * `identity.two_factor_required` and remembers the half-done sign-in in a short-lived cookie, so the code step
 * only sends the code.
 *
 * Navigation after success is not done here: invalidating the session makes <PublicOnlyRoute> redirect to
 * the page the user originally asked for (or the dashboard).
 */
const makeSchema = (t: TFunction) =>
  z.object({
    email: z.string().min(1, t('validation.emailRequired')).email(t('validation.emailInvalid')),
    password: z.string().min(1, t('validation.passwordRequired')),
    rememberMe: z.boolean(),
  })

type Fields = z.infer<ReturnType<typeof makeSchema>>

export function SignInPage() {
  // Set once the password was accepted and the account still needs its code.
  const [codeStep, setCodeStep] = useState<{ rememberMe: boolean } | null>(null)

  return codeStep ? (
    <TwoFactorStep rememberMe={codeStep.rememberMe} onBack={() => setCodeStep(null)} />
  ) : (
    <PasswordStep onTwoFactorRequired={(rememberMe) => setCodeStep({ rememberMe })} />
  )
}

function PasswordStep({ onTwoFactorRequired }: { onTwoFactorRequired: (rememberMe: boolean) => void }) {
  const [fields, setFields] = useState<Fields>({ email: '', password: '', rememberMe: false })
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<keyof Fields, string>>>({})
  const queryClient = useQueryClient()
  const { t } = useI18n()
  const schema = useMemo(() => makeSchema(t), [t])

  const signIn = useMutation({
    mutationFn: (values: Fields) => authApi.login(values.email, values.password, values.rememberMe),
    meta: { handlesErrors: true },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: SESSION_QUERY_KEY }),
    onError: (error, values) => {
      if (error instanceof ApiError && error.problem?.errorCode === TWO_FACTOR_REQUIRED) {
        onTwoFactorRequired(values.rememberMe)

        return
      }

      // The server deliberately does not say which of the two fields was wrong, so this text must not imply
      // that either one was. Echoing "unknown email" would confirm which addresses exist.
      if (error instanceof ApiError && error.isUnauthenticated) {
        notify.error(t('signIn.failed'), t('signIn.rejected'))
      } else {
        notify.fromError(error, t('signIn.failed'))
      }
    },
  })

  function submit(event: FormEvent) {
    event.preventDefault()

    const parsed = schema.safeParse(fields)

    if (!parsed.success) {
      setFieldErrors(zodFieldErrors<keyof Fields>(parsed.error))

      return
    }

    setFieldErrors({})
    signIn.mutate(parsed.data)
  }

  const serverErrors = signIn.error instanceof ApiError ? signIn.error.fieldErrors : {}

  return (
    <Section
      title={t('signIn.title')}
      description={t('signIn.description')}
      className="bg-card/80 shadow-lg backdrop-blur"
    >
      <form onSubmit={submit} noValidate className="grid gap-5">
        <FormField label={t('common.email')} htmlFor="email" error={fieldErrors.email ?? serverErrors.email?.[0]}>
          <Input
            name="email"
            type="email"
            autoComplete="username"
            required
            value={fields.email}
            onChange={(event) => setFields({ ...fields, email: event.target.value })}
          />
        </FormField>

        <FormField label={t('common.password')} htmlFor="password" error={fieldErrors.password ?? serverErrors.password?.[0]}>
          <Input
            name="password"
            type="password"
            autoComplete="current-password"
            required
            value={fields.password}
            onChange={(event) => setFields({ ...fields, password: event.target.value })}
          />
        </FormField>

        <div className="flex items-center gap-2">
          <Checkbox
            id="rememberMe"
            checked={fields.rememberMe}
            onCheckedChange={(checked) => setFields({ ...fields, rememberMe: checked === true })}
          />
          <Label htmlFor="rememberMe" className="font-normal">
            {t('signIn.remember')}
          </Label>
        </div>

        <Button type="submit" disabled={signIn.isPending} className="w-full">
          {signIn.isPending && <Loader2 className="animate-spin" />}
          {signIn.isPending ? t('signIn.submitting') : t('signIn.submit')}
        </Button>
      </form>
    </Section>
  )
}

/**
 * The second step: the six digits from the authenticator app (submitted as soon as the sixth is typed), or a
 * recovery code for someone without their phone.
 */
function TwoFactorStep({ rememberMe, onBack }: { rememberMe: boolean; onBack: () => void }) {
  const queryClient = useQueryClient()
  const { t } = useI18n()
  const [useRecoveryCode, setUseRecoveryCode] = useState(false)
  const [code, setCode] = useState('')
  const [rememberBrowser, setRememberBrowser] = useState(false)
  const [error, setError] = useState<string | undefined>()

  const verify = useMutation({
    mutationFn: (value: string) =>
      authApi.loginTwoFactor({ code: value, useRecoveryCode, rememberMe, rememberBrowser }),
    meta: { handlesErrors: true },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: SESSION_QUERY_KEY }),
    onError: (failure) => {
      if (failure instanceof ApiError && failure.problem?.errorCode === TWO_FACTOR_SESSION_EXPIRED) {
        notify.warning(t('signIn.twoFactor.expired'))
        onBack()

        return
      }

      // A wrong code is shown under the boxes, which are cleared for the next try; anything else (a lockout,
      // a network fault) is a toast.
      if (failure instanceof ApiError && failure.problem?.errorCode === 'identity.two_factor_code_invalid') {
        setError(useRecoveryCode ? t('signIn.twoFactor.recoveryInvalid') : t('signIn.twoFactor.invalid'))
        setCode('')

        return
      }

      notify.fromError(failure, t('signIn.failed'))
    },
  })

  function submit(event?: FormEvent) {
    event?.preventDefault()

    const value = code.trim()

    if (useRecoveryCode ? value.length === 0 : value.length !== OTP_LENGTH) {
      setError(useRecoveryCode ? t('signIn.twoFactor.recoveryRequired') : t('signIn.twoFactor.required'))

      return
    }

    setError(undefined)
    verify.mutate(value)
  }

  function switchMode() {
    setUseRecoveryCode(!useRecoveryCode)
    setCode('')
    setError(undefined)
  }

  return (
    <Section
      title={
        <span className="flex items-center gap-2">
          <Smartphone className="size-5" />
          {t('signIn.twoFactor.title')}
        </span>
      }
      description={useRecoveryCode ? t('signIn.twoFactor.recoveryDescription') : t('signIn.twoFactor.description')}
      className="bg-card/80 shadow-lg backdrop-blur"
    >
      <form onSubmit={submit} noValidate className="grid gap-5">
        {useRecoveryCode ? (
          <FormField label={t('signIn.twoFactor.recoveryLabel')} htmlFor="recoveryCode" error={error}>
            <Input
              name="recoveryCode"
              autoComplete="off"
              autoFocus
              spellCheck={false}
              className="font-mono"
              value={code}
              onChange={(event) => setCode(event.target.value)}
            />
          </FormField>
        ) : (
          <div className="grid justify-items-center gap-2">
            <OtpCodeInput
              aria-label={t('signIn.twoFactor.codeLabel')}
              aria-describedby={error ? 'twoFactorCode-error' : undefined}
              autoFocus
              value={code}
              onChange={(value) => {
                setCode(value)
                setError(undefined)
              }}
              onComplete={(value) => verify.mutate(value)}
              disabled={verify.isPending}
              invalid={error !== undefined}
            />
            {error && (
              <p id="twoFactorCode-error" className="text-destructive text-sm">
                {error}
              </p>
            )}
          </div>
        )}

        {!useRecoveryCode && (
          <div className="flex items-center gap-2">
            <Checkbox
              id="rememberBrowser"
              checked={rememberBrowser}
              onCheckedChange={(checked) => setRememberBrowser(checked === true)}
            />
            <Label htmlFor="rememberBrowser" className="font-normal">
              {t('signIn.twoFactor.rememberBrowser')}
            </Label>
          </div>
        )}

        <Button type="submit" disabled={verify.isPending} className="w-full">
          {verify.isPending && <Loader2 className="animate-spin" />}
          {verify.isPending ? t('signIn.twoFactor.verifying') : t('signIn.twoFactor.verify')}
        </Button>

        <div className="flex flex-wrap items-center justify-between gap-2">
          <Button variant="ghost" size="sm" onClick={onBack}>
            <ArrowLeft />
            {t('signIn.twoFactor.back')}
          </Button>
          <Button variant="link" size="sm" className="px-0" onClick={switchMode}>
            {useRecoveryCode ? t('signIn.twoFactor.useApp') : t('signIn.twoFactor.useRecovery')}
          </Button>
        </div>
      </form>
    </Section>
  )
}
