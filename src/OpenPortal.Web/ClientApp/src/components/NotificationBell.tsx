import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Bell, CheckCheck, Loader2 } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { notificationKeys, notificationsApi } from '@/api/notifications'
import type { PortalNotification } from '@/api/types'
import { Button } from '@/components/ui/button'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Separator } from '@/components/ui/separator'
import { notify } from '@/hooks/useToast'
import { formatRelative } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import {
  notificationIcon,
  notificationQuote,
  notificationText,
  useNotificationCount,
  useNotificationRefresh,
} from '@/lib/notifications'
import { cn } from '@/lib/utils'

/** How many notifications the popover lists; the rest are on the notifications page. */
const LATEST = 8

/**
 * The header's bell: the unread count (polled), a popover with the latest notifications, and a toast when a new
 * one arrives while the portal is open. Opening a notification marks it read and follows its link.
 */
export function NotificationBell() {
  const { t } = useI18n()
  const [open, setOpen] = useState(false)
  const queryClient = useQueryClient()
  const refresh = useNotificationRefresh()
  const count = useNotificationCount()

  const latest = useQuery({
    queryKey: notificationKeys.latest,
    queryFn: ({ signal }) => notificationsApi.list({ page: 1, pageSize: LATEST }, signal),
    enabled: open,
  })

  // Undefined until the first answer: only a change after that is news.
  useNewNotificationToast(count.data === undefined ? undefined : count.data.latestAtUtc)

  // Something new arrived: the open list should show it too.
  useEffect(() => {
    void queryClient.invalidateQueries({ queryKey: notificationKeys.latest })
  }, [count.data?.latestAtUtc, queryClient])

  const markAllRead = useMutation({
    mutationFn: () => notificationsApi.markAllRead(),
    onSuccess: () => refresh(),
  })

  const unread = count.data?.unreadCount ?? 0
  const label = unread > 0 ? t('notifications.bellUnread', { count: unread }) : t('notifications.bell')

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button variant="ghost" size="icon" className="relative" aria-label={label} title={label}>
          <Bell />
          {unread > 0 && (
            <span className="bg-destructive absolute top-1 right-1 flex h-4 min-w-4 items-center justify-center rounded-full px-1 text-[0.625rem] leading-none font-semibold text-white tabular-nums">
              {unread > 99 ? '99+' : unread}
            </span>
          )}
        </Button>
      </PopoverTrigger>
      <PopoverContent align="end" className="w-[min(24rem,calc(100vw-2rem))] p-0">
        <div className="flex items-center justify-between gap-2 px-4 py-3">
          <div>
            <p className="text-sm font-semibold">{t('notifications.title')}</p>
            <p className="text-muted-foreground text-xs">{t('notifications.unreadCount', { count: unread })}</p>
          </div>
          <Button
            variant="ghost"
            size="sm"
            disabled={unread === 0 || markAllRead.isPending}
            onClick={() => markAllRead.mutate()}
          >
            <CheckCheck />
            {t('notifications.markAllRead')}
          </Button>
        </div>
        <Separator />

        {latest.isPending && (
          <div className="text-muted-foreground flex items-center justify-center gap-2 p-6 text-sm">
            <Loader2 className="size-4 animate-spin" />
            {t('common.loading')}
          </div>
        )}

        {latest.data?.items.length === 0 && (
          <div className="grid justify-items-center gap-2 px-6 py-10 text-center">
            <Bell className="text-muted-foreground size-6" />
            <p className="text-sm font-medium">{t('notifications.empty.title')}</p>
            <p className="text-muted-foreground text-xs">{t('notifications.empty.description')}</p>
          </div>
        )}

        {latest.data && latest.data.items.length > 0 && (
          <ScrollArea className="max-h-[min(26rem,60vh)]">
            <ul className="divide-y">
              {latest.data.items.map((notification) => (
                <li key={notification.id}>
                  <NotificationItem notification={notification} onOpened={() => setOpen(false)} />
                </li>
              ))}
            </ul>
          </ScrollArea>
        )}

        <Separator />
        <div className="p-2">
          <Button asChild variant="ghost" size="sm" className="w-full">
            <Link to="/notifications" onClick={() => setOpen(false)}>
              {t('notifications.seeAll')}
            </Link>
          </Button>
        </div>
      </PopoverContent>
    </Popover>
  )
}

/** One line of the popover: a button that marks the notification read and opens its link. */
function NotificationItem({ notification, onOpened }: { notification: PortalNotification; onOpened: () => void }) {
  const { t } = useI18n()
  const navigate = useNavigate()
  const refresh = useNotificationRefresh()
  const { title, body } = notificationText(t, notification)
  const quote = notificationQuote(notification)
  const icon = notificationIcon(notification.type, 'size-4')
  const isUnread = notification.readAtUtc === null

  const open = useMutation({
    mutationFn: () => (isUnread ? notificationsApi.markRead(notification.id) : Promise.resolve(notification)),
    onSettled: () => refresh(),
  })

  return (
    <button
      type="button"
      className="hover:bg-accent focus-visible:bg-accent flex w-full gap-3 px-4 py-3 text-left outline-none"
      onClick={() => {
        open.mutate()
        onOpened()

        if (notification.link) {
          void navigate(notification.link)
        }
      }}
    >
      <span
        className={cn(
          'mt-0.5 flex size-8 shrink-0 items-center justify-center rounded-full border',
          isUnread ? 'bg-primary/10 border-primary/20' : 'bg-muted',
        )}
      >
        {icon}
      </span>
      <span className="grid min-w-0 flex-1 gap-0.5">
        <span className={cn('truncate text-sm', isUnread ? 'font-semibold' : 'text-muted-foreground')}>{title}</span>
        {body && <span className="text-muted-foreground line-clamp-2 text-xs">{body}</span>}
        {quote && <span className="text-muted-foreground line-clamp-1 text-xs italic">“{quote}”</span>}
        <span className="text-muted-foreground text-[0.7rem]">{formatRelative(notification.createdAtUtc)}</span>
      </span>
      {isUnread && <span className="bg-primary mt-2 size-2 shrink-0 rounded-full" aria-label={t('notifications.unread')} />}
    </button>
  )
}

/**
 * Shows a toast when the newest notification changes after the page has loaded (something arrived while the
 * user was here). The first answer only sets the baseline: notifications already there are not news.
 */
function useNewNotificationToast(latestAtUtc: string | null | undefined) {
  const { t } = useI18n()
  const baseline = useRef<string | null | undefined>(undefined)

  useEffect(() => {
    if (latestAtUtc === undefined) {
      return
    }

    if (baseline.current === undefined) {
      baseline.current = latestAtUtc
      return
    }

    if (latestAtUtc === null || latestAtUtc === baseline.current) {
      return
    }

    baseline.current = latestAtUtc
    const controller = new AbortController()

    void notificationsApi
      .list({ page: 1, pageSize: 1, unreadOnly: true }, controller.signal)
      .then((page) => {
        const newest = page.items[0]
        if (newest) {
          const { title, body } = notificationText(t, newest)
          notify.info(title, body || undefined)
        }
      })
      .catch(() => undefined)

    return () => controller.abort()
  }, [latestAtUtc, t])
}
