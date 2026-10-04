import type { ApplicationStatus } from '@/api/types'
import { Badge } from '@/components/ui/badge'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { formatDate } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { cn } from '@/lib/utils'

/**
 * An application that announces itself does so every few minutes (see OpenPortal.Client), so one that has
 * not been heard from for three intervals is shown as offline.
 */
const ONLINE_WINDOW_MS = 15 * 60 * 1000

/** Read at render time on purpose: the indicator reflects "now" whenever the list is drawn or refetched. */
function seenRecently(lastSeenAtUtc: string): boolean {
  return Date.now() - new Date(lastSeenAtUtc).getTime() < ONLINE_WINDOW_MS
}

export function ApplicationStatusBadge({ status }: { status: ApplicationStatus }) {
  const { t } = useI18n()
  const variant = status === 'active' ? 'success' : status === 'pending' ? 'warning' : 'secondary'

  return <Badge variant={variant}>{t(`applications.status.${status}`)}</Badge>
}

/** A dot showing whether the application has announced itself recently, with the time on hover. */
export function OnlineIndicator({ lastSeenAtUtc }: { lastSeenAtUtc: string | null }) {
  const { t } = useI18n()

  if (!lastSeenAtUtc) {
    return null
  }

  const online = seenRecently(lastSeenAtUtc)
  const label = online ? t('applications.online') : t('applications.offline')

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <span className="inline-flex items-center" aria-label={label}>
          <span className={cn('size-2 rounded-full', online ? 'bg-success' : 'bg-muted-foreground/40')} />
        </span>
      </TooltipTrigger>
      <TooltipContent>
        {label} · {t('applications.lastSeen', { date: formatDate(lastSeenAtUtc, { dateStyle: 'medium', timeStyle: 'short' }) })}
      </TooltipContent>
    </Tooltip>
  )
}
