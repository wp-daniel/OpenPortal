import { Fragment } from 'react'
import { Link, useMatches } from 'react-router-dom'
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '@/components/ui/breadcrumb'
import { useI18n } from '@/i18n/useI18n'
import { cn } from '@/lib/utils'

/** One step of the trail. Without `to` it is plain text (a grouping such as "Administration"). */
export interface Crumb {
  /** Translation key of the label. */
  readonly labelKey: string
  readonly to?: string
}

/** What a route declares in its `handle` to appear in the trail; routes without one add nothing. */
export interface RouteHandle {
  readonly crumbs?: readonly Crumb[]
}

const home: Crumb = { labelKey: 'nav.dashboard', to: '/' }

/**
 * The trail shown in the header, built from the `handle.crumbs` of every matched route, so the route tree in
 * `App.tsx` is the one place that says where a page sits. The last step is the current page and is never a
 * link. On small screens only the current page is shown.
 */
export function AppBreadcrumbs() {
  const { t } = useI18n()
  const matches = useMatches()

  const trail = matches.flatMap((match) => (match.handle as RouteHandle | undefined)?.crumbs ?? [])
  const crumbs = trail.length > 0 && trail[0].to === home.to ? trail : [home, ...trail]

  return (
    <Breadcrumb className="min-w-0">
      <BreadcrumbList className="flex-nowrap">
        {crumbs.map((crumb, index) => {
          const last = index === crumbs.length - 1

          return (
            <Fragment key={`${crumb.labelKey}-${index}`}>
              {index > 0 && <BreadcrumbSeparator className="hidden md:block" />}
              <BreadcrumbItem className={cn('min-w-0', !last && 'hidden md:inline-flex')}>
                {last ? (
                  <BreadcrumbPage className="truncate">{t(crumb.labelKey)}</BreadcrumbPage>
                ) : crumb.to ? (
                  <BreadcrumbLink asChild>
                    <Link to={crumb.to}>{t(crumb.labelKey)}</Link>
                  </BreadcrumbLink>
                ) : (
                  <span>{t(crumb.labelKey)}</span>
                )}
              </BreadcrumbItem>
            </Fragment>
          )
        })}
      </BreadcrumbList>
    </Breadcrumb>
  )
}
