import { useQuery } from '@tanstack/react-query'
import { ArrowUpRight } from 'lucide-react'
import { accessKeys, launchpadApi } from '@/api/access'
import { PageHeader } from '@/components/PageHeader'
import { ErrorPanel, LoadingState } from '@/components/StatePanels'
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar'
import { Card, CardDescription, CardTitle } from '@/components/ui/card'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'

export function HomePage() {
  const { t } = useI18n()
  const apps = useQuery({
    queryKey: accessKeys.launchpad,
    queryFn: ({ signal }) => launchpadApi.list(signal),
  })

  return (
    <>
      <PageHeader title={t('launchpad.title')} />
      {apps.isPending ? (
        <LoadingState />
      ) : apps.isError ? (
        <ErrorPanel message={describeError(apps.error)} traceId={traceIdOf(apps.error)} onRetry={() => void apps.refetch()} />
      ) : apps.data.length === 0 ? (
        <p className="text-muted-foreground rounded-xl border border-dashed p-8 text-center text-sm">{t('launchpad.empty')}</p>
      ) : (
        <ul className="grid grid-cols-1 gap-3 sm:grid-cols-2 sm:gap-4 xl:grid-cols-3">
          {apps.data.map((app) => (
            <li key={app.id}>
              <a
                href={app.baseUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="group focus-visible:ring-ring block h-full rounded-xl outline-none focus-visible:ring-2 focus-visible:ring-offset-2"
              >
                <Card className="group-hover:border-foreground/30 group-hover:bg-accent/40 grid h-full min-h-28 grid-cols-[auto_minmax(0,1fr)_auto] items-center gap-3 p-4 transition-colors sm:min-h-44 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-start sm:gap-5 sm:p-6">
                  <Avatar aria-hidden="true" className="size-12 rounded-xl">
                    {app.iconUrl && <AvatarImage src={app.iconUrl} alt="" className="object-contain" />}
                    <AvatarFallback className="bg-primary text-primary-foreground rounded-xl text-xl font-semibold">
                      {Array.from(app.displayName)[0]}
                    </AvatarFallback>
                  </Avatar>
                  <div className="space-y-1 wrap-anywhere sm:col-span-2 sm:row-start-2">
                    <CardTitle className="text-lg leading-tight">{app.displayName}</CardTitle>
                    {app.description && <CardDescription>{app.description}</CardDescription>}
                  </div>
                  <ArrowUpRight aria-hidden="true" className="text-muted-foreground group-hover:text-foreground size-5 sm:col-start-2 sm:row-start-1" />
                  <span className="sr-only">{t('launchpad.newTab')}</span>
                </Card>
              </a>
            </li>
          ))}
        </ul>
      )}
    </>
  )
}
