import { LogOut } from 'lucide-react'
import { useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { postForm } from '@/lib/formPost'
import { Section } from '@/components/Section'
import { Button } from '@/components/ui/button'
import { useSession } from '@/hooks/useSession'
import { useI18n } from '@/i18n/useI18n'

const CARD = 'bg-card/80 shadow-lg backdrop-blur'

/**
 * An application asked the portal to end the session without proving who it was signing out (no id_token_hint), so
 * the person confirms here: a link on another site cannot sign them out. Confirming posts the original request back
 * to the end-session endpoint with the antiforgery token, as a classic form, so the portal's redirect is followed.
 */
export function SignOutPage() {
  const { t } = useI18n()
  const { session } = useSession()
  const location = useLocation()
  const navigate = useNavigate()
  const [busy, setBusy] = useState(false)

  async function confirm() {
    setBusy(true)

    const fields = new URLSearchParams(location.search)
    fields.set('confirm', 'true')

    await postForm('/connect/endsession', fields)
  }

  return (
    <Section className={CARD}>
      <div className="grid justify-items-center gap-4 py-2 text-center">
        <LogOut className="text-muted-foreground size-10" />
        <h1 className="text-xl font-semibold tracking-tight">{t('signOut.title')}</h1>
        <p className="text-muted-foreground text-sm">
          {session.isAuthenticated ? t('signOut.description', { email: session.user?.email ?? '' }) : t('signOut.notSignedIn')}
        </p>
        <div className="flex flex-wrap justify-center gap-2">
          {session.isAuthenticated && (
            <Button onClick={() => void confirm()} disabled={busy}>
              {t('signOut.confirm')}
            </Button>
          )}
          <Button variant="outline" onClick={() => navigate('/', { replace: true })}>
            {t('signOut.cancel')}
          </Button>
        </div>
      </div>
    </Section>
  )
}
