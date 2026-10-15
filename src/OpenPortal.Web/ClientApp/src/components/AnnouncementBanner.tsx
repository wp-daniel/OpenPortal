import { useQuery } from '@tanstack/react-query'
import { Info, Megaphone, TriangleAlert, Wrench, X } from 'lucide-react'
import { useState } from 'react'
import { announcementKeys, announcementsApi } from '@/api/announcements'
import type { ActiveAnnouncement, AnnouncementSeverity } from '@/api/types'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { useSession } from '@/hooks/useSession'
import { formatDate } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { cn } from '@/lib/utils'

const DISMISSED_KEY = 'openportal-dismissed-announcements'
const REFRESH_MS = 5 * 60_000

/** Hidden banners live in this browser only: `id:version`, so an edited announcement shows again. */
function readDismissed(): string[] {
  try {
    const raw = localStorage.getItem(DISMISSED_KEY)
    const parsed: unknown = raw ? JSON.parse(raw) : []
    return Array.isArray(parsed) ? parsed.filter((value): value is string => typeof value === 'string') : []
  } catch {
    return []
  }
}

function writeDismissed(keys: string[]) {
  try {
    // Only the most recent ones: ended announcements never come back, so their keys are dead weight.
    localStorage.setItem(DISMISSED_KEY, JSON.stringify(keys.slice(-50)))
  } catch {
    // Private mode or blocked storage: the banner just comes back on the next visit.
  }
}

const dismissKey = (announcement: ActiveAnnouncement) => `${announcement.id}:${announcement.version}`

const ICONS: Record<AnnouncementSeverity, typeof Info> = {
  info: Info,
  warning: TriangleAlert,
  maintenance: Wrench,
}

const STYLES: Record<AnnouncementSeverity, string> = {
  info: 'border-primary/20 bg-primary/5 text-foreground',
  warning: 'border-warning/40 bg-warning/10 text-foreground [&>svg]:text-warning',
  maintenance: 'border-destructive/40 bg-destructive/10 text-foreground [&>svg]:text-destructive',
}

/**
 * The announcements showing now (news, warnings, planned maintenance), stacked above the page. Refreshed every few
 * minutes and on a language change; a dismissible one can be hidden in this browser until it is edited.
 */
export function AnnouncementBanner({ className }: { className?: string }) {
  const { t, language } = useI18n()
  const { session, isPending } = useSession()
  const [dismissed, setDismissed] = useState(readDismissed)

  const { data } = useQuery({
    queryKey: announcementKeys.active(language, session.isAuthenticated),
    queryFn: ({ signal }) => announcementsApi.active(signal),
    enabled: !isPending,
    refetchInterval: REFRESH_MS,
    staleTime: 60_000,
  })

  const visible = (data ?? []).filter((announcement) => !dismissed.includes(dismissKey(announcement)))
  if (visible.length === 0) {
    return null
  }

  const dismiss = (announcement: ActiveAnnouncement) => {
    const next = [...dismissed, dismissKey(announcement)]
    setDismissed(next)
    writeDismissed(next)
  }

  return (
    <section aria-label={t('announcements.region')} className={cn('space-y-2', className)}>
      {visible.map((announcement) => {
        const Icon = ICONS[announcement.severity] ?? Megaphone

        return (
          <Alert key={announcement.id} role="status" className={cn('pr-10', STYLES[announcement.severity])}>
            <Icon />
            <AlertTitle className="line-clamp-none">{announcement.title}</AlertTitle>
            {(announcement.body || announcement.endsAtUtc) && (
              <AlertDescription className="text-foreground/80">
                {announcement.body && <p className="whitespace-pre-line">{announcement.body}</p>}
                {announcement.endsAtUtc && (
                  <p className="text-muted-foreground text-xs">
                    {t('announcements.until', {
                      date: formatDate(announcement.endsAtUtc, { dateStyle: 'medium', timeStyle: 'short' }),
                    })}
                  </p>
                )}
              </AlertDescription>
            )}
            {announcement.dismissible && (
              <Button
                variant="ghost"
                size="icon"
                className="absolute top-1.5 right-1.5 size-7"
                aria-label={t('announcements.dismiss')}
                onClick={() => dismiss(announcement)}
              >
                <X />
              </Button>
            )}
          </Alert>
        )
      })}
    </section>
  )
}
