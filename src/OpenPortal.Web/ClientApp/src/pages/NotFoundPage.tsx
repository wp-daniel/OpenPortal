import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { useI18n } from '@/i18n/useI18n'

export function NotFoundPage() {
  const { t } = useI18n()

  return (
    <div className="mx-auto max-w-lg py-16 text-center">
      <h1 className="text-2xl font-semibold tracking-tight">{t('notFound.title')}</h1>
      <p className="text-muted-foreground mt-2 text-sm">
        {t('notFound.description')}
      </p>
      <Button asChild className="mt-6">
        <Link to="/">{t('notFound.back')}</Link>
      </Button>
    </div>
  )
}
