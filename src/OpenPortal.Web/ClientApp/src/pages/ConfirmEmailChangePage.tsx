import { useMutation, useQueryClient } from '@tanstack/react-query'
import { CircleCheck, Loader2, MailWarning } from 'lucide-react'
import { useEffect, useRef } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ApiError } from '@/api/client'
import { registrationApi } from '@/api/registration'
import { Section } from '@/components/Section'
import { Button } from '@/components/ui/button'
import { useI18n } from '@/i18n/useI18n'

const CARD = 'bg-card/80 shadow-lg backdrop-blur'

/**
 * The link sent to a new address lands here: the address becomes the account's sign-in address as soon as the page
 * opens. Reachable signed in or not, since the link may be opened on another device.
 */
export function ConfirmEmailChangePage() {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const [params] = useSearchParams()
  const userId = params.get('user') ?? ''
  const token = params.get('token') ?? ''
  const started = useRef(false)

  const confirm = useMutation({
    mutationFn: () => registrationApi.confirmEmailChange(userId, token),
    meta: { handlesErrors: true },
    // The session (the address shown in the menu, the profile) is stale either way.
    onSettled: () => queryClient.invalidateQueries(),
  })

  // Once, even under StrictMode's double effects: the link works once.
  useEffect(() => {
    if (!started.current && userId && token) {
      started.current = true
      confirm.mutate()
    }
  }, [confirm, token, userId])

  if (!userId || !token || confirm.isError) {
    const taken = confirm.error instanceof ApiError && confirm.error.problem?.errorCode === 'identity.duplicate_email'

    return (
      <Section className={CARD}>
        <div className="grid justify-items-center gap-3 py-2 text-center">
          <MailWarning className="text-muted-foreground size-10" />
          <h1 className="text-xl font-semibold tracking-tight">{t('emailChange.invalid.title')}</h1>
          <p className="text-muted-foreground text-sm">
            {taken ? t('emailChange.invalid.taken') : t('emailChange.invalid.description')}
          </p>
          <Button asChild variant="link">
            <Link to="/account?tab=security">{t('emailChange.toAccount')}</Link>
          </Button>
        </div>
      </Section>
    )
  }

  if (!confirm.data) {
    return (
      <Section className={CARD}>
        <div className="text-muted-foreground flex items-center justify-center gap-2 py-6 text-sm">
          <Loader2 className="size-4 animate-spin" />
          {t('emailChange.confirming')}
        </div>
      </Section>
    )
  }

  return (
    <Section className={CARD}>
      <div className="grid justify-items-center gap-4 py-2 text-center">
        <CircleCheck className="text-success size-10" />
        <h1 className="text-xl font-semibold tracking-tight">{t('emailChange.done.title')}</h1>
        <p className="text-muted-foreground text-sm">{t('emailChange.done.description', { email: confirm.data.email })}</p>
        <Button asChild>
          {confirm.data.signedIn ? (
            <Link to="/account?tab=security">{t('emailChange.toAccount')}</Link>
          ) : (
            <Link to="/sign-in">{t('register.backToSignIn')}</Link>
          )}
        </Button>
      </div>
    </Section>
  )
}
