import { ShieldX } from 'lucide-react'
import { Link, useSearchParams } from 'react-router-dom'
import { Section } from '@/components/Section'
import { Button } from '@/components/ui/button'
import { useI18n } from '@/i18n/useI18n'

/**
 * Where the portal sends a user who tried to sign in to an application they have not been given. The
 * application name comes from the query string and is only ever rendered as text.
 */
export function AccessDeniedPage() {
  const { t } = useI18n()
  const [params] = useSearchParams()
  const application = params.get('app')?.slice(0, 120) || t('accessDenied.unknownApplication')

  return (
    <div className="mx-auto max-w-lg py-10">
      <Section>
        <div className="grid justify-items-center gap-4 text-center">
          <ShieldX className="text-muted-foreground size-10" />
          <h1 className="text-xl font-semibold tracking-tight">{t('accessDenied.title', { application })}</h1>
          <p className="text-muted-foreground text-sm">{t('accessDenied.description')}</p>
          <Button asChild variant="outline">
            <Link to="/">{t('accessDenied.back')}</Link>
          </Button>
        </div>
      </Section>
    </div>
  )
}
