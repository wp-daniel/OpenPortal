import { useQuery } from '@tanstack/react-query'
import { Building2, ShieldX } from 'lucide-react'
import { Link, useSearchParams } from 'react-router-dom'
import { myAccessRequestsApi, requestKeys } from '@/api/requests'
import { RequestAccessControl } from '@/components/RequestAccess'
import { Section } from '@/components/Section'
import { Button } from '@/components/ui/button'
import { useI18n } from '@/i18n/useI18n'

/**
 * Where the portal sends a user who tried to sign in to an application they have not been given, or that needs an
 * organization they do not belong to. The application name comes from the query string and is only ever rendered
 * as text; when the application accepts requests (it is in the user's catalog), the page offers to ask for it.
 */
export function AccessDeniedPage() {
  const { t } = useI18n()
  const [params] = useSearchParams()
  const application = params.get('app')?.slice(0, 120) || t('accessDenied.unknownApplication')
  const clientId = params.get('client')
  const organization = params.get('reason') === 'organization'

  const catalog = useQuery({
    queryKey: requestKeys.catalog,
    queryFn: ({ signal }) => myAccessRequestsApi.catalog(signal),
    enabled: clientId !== null && !organization,
  })

  const requestable = clientId ? catalog.data?.find((item) => item.clientId === clientId) : undefined

  return (
    <div className="mx-auto max-w-lg py-10">
      <Section>
        <div className="grid justify-items-center gap-4 text-center">
          {organization ? (
            <Building2 className="text-muted-foreground size-10" />
          ) : (
            <ShieldX className="text-muted-foreground size-10" />
          )}
          <h1 className="text-xl font-semibold tracking-tight">{t('accessDenied.title', { application })}</h1>
          <p className="text-muted-foreground text-sm">
            {organization ? t('accessDenied.organization') : t('accessDenied.description')}
          </p>

          {requestable && (
            <div className="bg-muted/40 grid w-full justify-items-center gap-2 rounded-lg border p-4">
              <p className="text-sm">{t('accessDenied.canRequest')}</p>
              <RequestAccessControl application={requestable} />
            </div>
          )}

          <Button asChild variant="outline">
            <Link to="/">{t('accessDenied.back')}</Link>
          </Button>
        </div>
      </Section>
    </div>
  )
}
