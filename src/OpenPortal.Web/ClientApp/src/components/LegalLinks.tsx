import { Link } from 'react-router-dom'
import { useSession } from '@/hooks/useSession'
import { useI18n } from '@/i18n/useI18n'

/** Links to the privacy notice and the terms of use, for the ones that are published. */
export function LegalLinks() {
  const { t } = useI18n()
  const { session } = useSession()

  return (
    <>
      {session.legal.privacy && (
        <Link to="/privacy" className="hover:text-foreground underline-offset-4 hover:underline">
          {t('legal.privacy.link')}
        </Link>
      )}
      {session.legal.terms && (
        <Link to="/terms" className="hover:text-foreground underline-offset-4 hover:underline">
          {t('legal.terms.link')}
        </Link>
      )}
    </>
  )
}
