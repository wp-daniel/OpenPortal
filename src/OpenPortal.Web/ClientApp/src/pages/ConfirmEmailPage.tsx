import { useMutation } from '@tanstack/react-query'
import { CircleCheck, Clock, Loader2, MailWarning } from 'lucide-react'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { registrationApi } from '@/api/registration'
import { FormField } from '@/components/FormField'
import { Section } from '@/components/Section'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { notify } from '@/hooks/useToast'
import { useI18n } from '@/i18n/useI18n'

const CARD = 'bg-card/80 shadow-lg backdrop-blur'

/**
 * The link of a confirmation email lands here: the address is confirmed as soon as the page opens. Reachable
 * whether or not someone is signed in, since the link may be opened on any device.
 */
export function ConfirmEmailPage() {
  const { t } = useI18n()
  const [params] = useSearchParams()
  const userId = params.get('user') ?? ''
  const token = params.get('token') ?? ''
  const started = useRef(false)

  const confirm = useMutation({
    mutationFn: () => registrationApi.confirmEmail(userId, token),
    meta: { handlesErrors: true },
  })

  // Once, even under StrictMode's double effects: a token is single-use in spirit.
  useEffect(() => {
    if (!started.current && userId && token) {
      started.current = true
      confirm.mutate()
    }
  }, [confirm, token, userId])

  if (!userId || !token || confirm.isError) {
    return <ConfirmationFailed />
  }

  if (!confirm.data) {
    return (
      <Section className={CARD}>
        <div className="text-muted-foreground flex items-center justify-center gap-2 py-6 text-sm">
          <Loader2 className="size-4 animate-spin" />
          {t('confirmEmail.confirming')}
        </div>
      </Section>
    )
  }

  const waiting = confirm.data.outcome === 'approval_pending'
  const Icon = waiting ? Clock : CircleCheck

  return (
    <Section className={CARD}>
      <div className="grid justify-items-center gap-4 py-2 text-center">
        <Icon className={waiting ? 'text-muted-foreground size-10' : 'text-success size-10'} />
        <h1 className="text-xl font-semibold tracking-tight">{t('confirmEmail.done.title')}</h1>
        <p className="text-muted-foreground text-sm">
          {waiting ? t('confirmEmail.done.approval') : t('confirmEmail.done.description')}
        </p>
        <Button asChild>
          <Link to="/sign-in">{t('register.backToSignIn')}</Link>
        </Button>
      </div>
    </Section>
  )
}

/** An invalid or expired link: say so, and offer to send a new one. */
function ConfirmationFailed() {
  const { t } = useI18n()
  const [email, setEmail] = useState('')

  const resend = useMutation({
    mutationFn: () => registrationApi.resendConfirmation(email.trim()),
    onSuccess: () => notify.success(t('confirmEmail.resent'), t('confirmEmail.resentDescription')),
  })

  function submit(event: FormEvent) {
    event.preventDefault()

    if (email.trim() !== '') {
      resend.mutate()
    }
  }

  return (
    <Section className={CARD}>
      <div className="grid gap-4">
        <div className="grid justify-items-center gap-3 text-center">
          <MailWarning className="text-muted-foreground size-10" />
          <h1 className="text-xl font-semibold tracking-tight">{t('confirmEmail.invalid.title')}</h1>
          <p className="text-muted-foreground text-sm">{t('confirmEmail.invalid.description')}</p>
        </div>
        <form onSubmit={submit} noValidate className="grid gap-3">
          <FormField label={t('common.email')} htmlFor="resend-email">
            <Input type="email" autoComplete="email" value={email} onChange={(event) => setEmail(event.target.value)} />
          </FormField>
          <Button type="submit" variant="outline" disabled={resend.isPending || email.trim() === ''}>
            {resend.isPending && <Loader2 className="animate-spin" />}
            {t('confirmEmail.resend')}
          </Button>
        </form>
        <Button asChild variant="link" className="justify-self-center">
          <Link to="/sign-in">{t('register.backToSignIn')}</Link>
        </Button>
      </div>
    </Section>
  )
}
