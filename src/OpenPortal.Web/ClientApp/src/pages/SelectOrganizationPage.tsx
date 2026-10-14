import { useQuery } from '@tanstack/react-query'
import { Building2, ChevronRight, Loader2 } from 'lucide-react'
import { useState } from 'react'
import { Link, useLocation, useSearchParams } from 'react-router-dom'
import { myOrganizationsApi, organizationKeys } from '@/api/organizations'
import { Section } from '@/components/Section'
import { ErrorPanel, LoadingState } from '@/components/StatePanels'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { useI18n } from '@/i18n/useI18n'
import { describeError } from '@/lib/errors'
import { serverReturnUrl } from '@/lib/returnUrl'

/**
 * Where the portal asks a member of several organizations which one they are signing in for, when an application
 * works per organization. Choosing goes back to the authorization request (a full page load) with the choice added;
 * the return address is accepted only if it is the portal's own authorization endpoint.
 */
export function SelectOrganizationPage() {
  const { t } = useI18n()
  const location = useLocation()
  const [params] = useSearchParams()
  const [leaving, setLeaving] = useState<string | null>(null)
  const application = params.get('app')?.slice(0, 120) || t('accessDenied.unknownApplication')
  const returnUrl = serverReturnUrl(location.search)

  const organizations = useQuery({
    queryKey: organizationKeys.mine,
    queryFn: ({ signal }) => myOrganizationsApi.list(signal),
  })

  function choose(slug: string) {
    if (!returnUrl) {
      return
    }

    const target = new URL(returnUrl, window.location.origin)
    target.searchParams.set('organization', slug)
    setLeaving(slug)
    window.location.replace(`${target.pathname}${target.search}`)
  }

  return (
    <div className="mx-auto max-w-lg py-10">
      <Section
        title={
          <span className="flex items-center gap-2">
            <Building2 className="size-5" />
            {t('selectOrganization.title')}
          </span>
        }
        description={t('selectOrganization.description', { application })}
      >
        {!returnUrl && (
          <div className="grid gap-3 text-sm">
            <p className="text-muted-foreground">{t('selectOrganization.invalid')}</p>
            <Button asChild variant="outline" className="justify-self-start">
              <Link to="/">{t('accessDenied.back')}</Link>
            </Button>
          </div>
        )}

        {returnUrl && organizations.isPending && <LoadingState />}
        {returnUrl && organizations.isError && (
          <ErrorPanel message={describeError(organizations.error)} onRetry={() => void organizations.refetch()} />
        )}

        {returnUrl && organizations.data && (
          <ul className="grid gap-2">
            {organizations.data.map((organization) => (
              <li key={organization.id}>
                <button
                  type="button"
                  disabled={leaving !== null}
                  onClick={() => choose(organization.slug)}
                  className="hover:bg-accent focus-visible:ring-ring/50 flex w-full items-center gap-3 rounded-lg border p-3 text-left outline-none focus-visible:ring-[3px] disabled:opacity-60"
                >
                  <span className="bg-muted flex size-9 shrink-0 items-center justify-center rounded-md border">
                    <Building2 className="size-4" />
                  </span>
                  <span className="grid min-w-0 flex-1">
                    <span className="truncate font-medium">{organization.name}</span>
                    <span className="text-muted-foreground font-mono text-xs">{organization.slug}</span>
                  </span>
                  {organization.role === 'admin' && <Badge variant="secondary">{t('organizations.roleName.admin')}</Badge>}
                  {leaving === organization.slug ? (
                    <Loader2 className="size-4 animate-spin" />
                  ) : (
                    <ChevronRight className="text-muted-foreground size-4" />
                  )}
                </button>
              </li>
            ))}
          </ul>
        )}
      </Section>
    </div>
  )
}
