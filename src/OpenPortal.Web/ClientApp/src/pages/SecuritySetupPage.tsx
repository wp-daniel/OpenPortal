import { useMutation, useQueryClient } from '@tanstack/react-query'
import { LogOut, ShieldAlert, ShieldCheck } from 'lucide-react'
import { useLocation, useNavigate } from 'react-router-dom'
import { authApi } from '@/api/auth'
import { PasskeysSection } from '@/components/PasskeysSection'
import { Section } from '@/components/Section'
import { TwoFactorSection } from '@/components/TwoFactorSection'
import { Button } from '@/components/ui/button'
import { useSession } from '@/hooks/useSession'
import { useI18n } from '@/i18n/useI18n'
import { serverReturnUrl } from '@/lib/returnUrl'
import type { RedirectState } from '@/routes/guards'

const CARD = 'bg-card/80 shadow-lg backdrop-blur'

/**
 * The portal requires a second factor of this account and it has none: an authenticator app or a passkey must be
 * set up before anything else (another application's sign-in sends the user here with `returnUrl`).
 */
export function SecuritySetupPage() {
  const { t } = useI18n()
  const { user } = useSession()
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const location = useLocation()

  const leave = () => {
    const back = serverReturnUrl(location.search)
    if (back) {
      window.location.replace(back)
      return
    }

    const from = (location.state as RedirectState | null)?.from
    navigate(from && !from.startsWith('/account/security-setup') ? from : '/', { replace: true })
  }

  const signOut = useMutation({
    mutationFn: () => authApi.logout(),
    onSettled: () => {
      queryClient.clear()
      navigate('/sign-in', { replace: true })
    },
  })

  const done = user !== null && !user.twoFactorSetupRequired

  return (
    <div className="space-y-6">
      <Section className={CARD}>
        <div className="grid justify-items-center gap-3 text-center">
          {done ? <ShieldCheck className="text-success size-10" /> : <ShieldAlert className="text-warning size-10" />}
          <h1 className="text-xl font-semibold tracking-tight">{done ? t('securitySetup.doneTitle') : t('securitySetup.title')}</h1>
          <p className="text-muted-foreground text-sm">{done ? t('securitySetup.doneDescription') : t('securitySetup.description')}</p>
          <div className="flex flex-wrap justify-center gap-2">
            {done && <Button onClick={leave}>{t('securitySetup.continue')}</Button>}
            <Button variant="outline" onClick={() => signOut.mutate()} disabled={signOut.isPending}>
              <LogOut />
              {t('securitySetup.signOut')}
            </Button>
          </div>
        </div>
      </Section>

      <PasskeysSection />
      <TwoFactorSection />
    </div>
  )
}
