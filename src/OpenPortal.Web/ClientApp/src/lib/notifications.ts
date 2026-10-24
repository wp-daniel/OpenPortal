import { useQuery, useQueryClient } from '@tanstack/react-query'
import { AppWindow, Bell, Building2, KeyRound, ShieldAlert, UserPlus, type LucideIcon } from 'lucide-react'
import { createElement, useCallback, useEffect, useState, type ReactElement } from 'react'
import { notificationKeys, notificationsApi } from '@/api/notifications'
import type { NotificationCount, PortalNotification } from '@/api/types'
import type { TFunction } from '@/i18n/store'

/** How often the bell asks for the unread count while the tab is visible. */
export const NOTIFICATION_POLL_MS = 30_000

/** The same, while the event stream is connected and the poll is only a safety net. */
const NOTIFICATION_LIVE_POLL_MS = 120_000

/**
 * A notification's title and body in the active language. The server stores a type and the values its text
 * mentions; an unknown type (a newer server) still shows something sensible.
 */
export function notificationText(t: TFunction, notification: PortalNotification): { title: string; body: string } {
  const params: Record<string, string> = {}

  for (const [key, value] of Object.entries(notification.parameters)) {
    params[key] = value ?? ''
  }

  const titleKey = `notification.${notification.type}.title`
  const bodyKey = `notification.${notification.type}.body`
  const title = t(titleKey, params)

  return title === titleKey
    ? { title: notification.type, body: '' }
    : { title, body: t(bodyKey, params) }
}

/**
 * A note a person wrote (a reviewer's reason, a requester's motivation) shown under the body as a quote; the
 * translated body never repeats it, so it is not lost when the language changes.
 */
export function notificationQuote(notification: PortalNotification): string | null {
  return notification.parameters.note || notification.parameters.reason || null
}

const icons: Readonly<Record<string, LucideIcon>> = {
  access: KeyRound,
  application: AppWindow,
  organization: Building2,
  user: UserPlus,
  account: ShieldAlert,
}

/** The icon of a notification's category (the part of its type before the dot), as an element. */
export function notificationIcon(type: string, className?: string): ReactElement {
  return createElement(icons[type.split('.')[0]] ?? Bell, { className })
}

/** The unread count, polled while the tab is visible and refreshed when the user comes back to it. */
export function useNotificationCount(enabled = true, live = false) {
  return useQuery({
    queryKey: notificationKeys.count,
    queryFn: ({ signal }) => notificationsApi.count(signal),
    enabled,
    staleTime: 10_000,
    refetchInterval: live ? NOTIFICATION_LIVE_POLL_MS : NOTIFICATION_POLL_MS,
    refetchOnWindowFocus: true,
  })
}

/**
 * Listens to the server's notification stream (server-sent events): the count arrives the moment something changes,
 * from this tab, another tab or another instance of the portal. Returns whether the stream is connected; the browser
 * reconnects by itself after a drop, and the count query keeps a slow poll as a safety net.
 */
export function useNotificationStream(enabled = true): boolean {
  const queryClient = useQueryClient()
  const [connected, setConnected] = useState(false)

  useEffect(() => {
    if (!enabled || typeof EventSource === 'undefined') {
      return
    }

    const source = new EventSource('/api/notifications/stream', { withCredentials: true })

    source.onopen = () => setConnected(true)
    source.onerror = () => setConnected(false)
    source.addEventListener('count', (event) => {
      try {
        queryClient.setQueryData<NotificationCount>(notificationKeys.count, JSON.parse((event as MessageEvent<string>).data))
      } catch {
        // A malformed event changes nothing; the next one or the poll will.
      }
    })

    return () => {
      source.close()
      setConnected(false)
    }
  }, [enabled, queryClient])

  return connected
}

/** Refreshes every notification query (bell, list, count) after a change. */
export function useNotificationRefresh() {
  const queryClient = useQueryClient()

  return useCallback(() => queryClient.invalidateQueries({ queryKey: notificationKeys.all }), [queryClient])
}
