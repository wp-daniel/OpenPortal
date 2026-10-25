import { useMutation, useQuery } from '@tanstack/react-query'
import { CheckCheck, ChevronLeft, ChevronRight, Circle, CircleCheck, ExternalLink, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { notificationKeys, notificationsApi } from '@/api/notifications'
import type { PortalNotification } from '@/api/types'
import { NotificationEmailPreferences } from '@/components/NotificationEmailPreferences'
import { PageHeader } from '@/components/PageHeader'
import { Section } from '@/components/Section'
import { EmptyState, ErrorPanel, LoadingState } from '@/components/StatePanels'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { useTabParam } from '@/hooks/useTabParam'
import { notify } from '@/hooks/useToast'
import { formatDate, formatRelative } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'
import { notificationIcon, notificationQuote, notificationText, useNotificationRefresh } from '@/lib/notifications'
import { cn } from '@/lib/utils'

const TABS = ['all', 'unread'] as const
const PAGE_SIZE = 20

/** The notification center: every notification of the signed-in user, newest first, paged. */
export function NotificationsPage() {
  const { t } = useI18n()
  const [tab, setTab] = useTabParam(TABS)
  const [page, setPage] = useState(1)
  const refresh = useNotificationRefresh()
  const unreadOnly = tab === 'unread'

  const list = useQuery({
    queryKey: notificationKeys.list({ page, unreadOnly }),
    queryFn: ({ signal }) => notificationsApi.list({ page, pageSize: PAGE_SIZE, unreadOnly }, signal),
    placeholderData: (previous) => previous,
  })

  const markAllRead = useMutation({
    mutationFn: () => notificationsApi.markAllRead(),
    onSuccess: async () => {
      notify.success(t('notifications.allRead'))
      await refresh()
    },
  })

  const removeRead = useMutation({
    mutationFn: () => notificationsApi.removeRead(),
    onSuccess: async () => {
      notify.success(t('notifications.readDeleted'))
      setPage(1)
      await refresh()
    },
  })

  const unread = list.data?.unreadCount ?? 0

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('notifications.title')}
        description={t('notifications.description')}
        actions={
          <>
            <Button variant="outline" disabled={unread === 0 || markAllRead.isPending} onClick={() => markAllRead.mutate()}>
              <CheckCheck />
              {t('notifications.markAllRead')}
            </Button>
            <Button variant="outline" disabled={removeRead.isPending} onClick={() => removeRead.mutate()}>
              <Trash2 />
              {t('notifications.deleteRead')}
            </Button>
          </>
        }
      />

      <Tabs
        value={tab}
        onValueChange={(value) => {
          setTab(value)
          setPage(1)
        }}
      >
        <TabsList variant="line">
          <TabsTrigger value="all">{t('notifications.tab.all')}</TabsTrigger>
          <TabsTrigger value="unread">{t('notifications.tab.unread', { count: unread })}</TabsTrigger>
        </TabsList>
        <Separator className="-mt-2" />
      </Tabs>

      <Section>
        {list.isError && (
          <ErrorPanel message={describeError(list.error)} traceId={traceIdOf(list.error)} onRetry={() => void list.refetch()} />
        )}
        {list.isPending && <LoadingState />}

        {list.data?.items.length === 0 && (
          <EmptyState
            title={unreadOnly ? t('notifications.emptyUnread.title') : t('notifications.empty.title')}
            description={unreadOnly ? t('notifications.emptyUnread.description') : t('notifications.empty.description')}
          />
        )}

        {list.data && list.data.items.length > 0 && (
          <>
            <ul className="divide-y rounded-lg border">
              {list.data.items.map((notification) => (
                <NotificationRow key={notification.id} notification={notification} />
              ))}
            </ul>

            {list.data.totalPages > 1 && (
              <div className="mt-4 flex items-center justify-between gap-2 text-sm">
                <span className="text-muted-foreground">
                  {t('common.pageOf', { page: list.data.page, pages: list.data.totalPages })}
                </span>
                <div className="flex gap-2">
                  <Button variant="outline" size="sm" disabled={!list.data.hasPrevious} onClick={() => setPage(page - 1)}>
                    <ChevronLeft />
                    {t('common.previous')}
                  </Button>
                  <Button variant="outline" size="sm" disabled={!list.data.hasNext} onClick={() => setPage(page + 1)}>
                    {t('common.next')}
                    <ChevronRight />
                  </Button>
                </div>
              </div>
            )}
          </>
        )}
      </Section>

      <NotificationEmailPreferences />
    </div>
  )
}

function NotificationRow({ notification }: { notification: PortalNotification }) {
  const { t } = useI18n()
  const refresh = useNotificationRefresh()
  const { title, body } = notificationText(t, notification)
  const quote = notificationQuote(notification)
  const icon = notificationIcon(notification.type, 'size-4')
  const isUnread = notification.readAtUtc === null

  const toggleRead = useMutation({
    mutationFn: () => (isUnread ? notificationsApi.markRead(notification.id) : notificationsApi.markUnread(notification.id)),
    onSuccess: () => refresh(),
  })

  const remove = useMutation({
    mutationFn: () => notificationsApi.remove(notification.id),
    onSuccess: () => refresh(),
  })

  return (
    <li className={cn('flex gap-3 p-4', isUnread && 'bg-primary/[0.03]')}>
      <span
        className={cn(
          'flex size-9 shrink-0 items-center justify-center rounded-full border',
          isUnread ? 'bg-primary/10 border-primary/20' : 'bg-muted',
        )}
      >
        {icon}
      </span>

      <div className="grid min-w-0 flex-1 gap-1">
        <div className="flex flex-wrap items-baseline gap-x-2">
          <span className={cn('text-sm', isUnread ? 'font-semibold' : 'font-medium')}>{title}</span>
          <Tooltip>
            <TooltipTrigger asChild>
              <span className="text-muted-foreground text-xs">{formatRelative(notification.createdAtUtc)}</span>
            </TooltipTrigger>
            <TooltipContent>{formatDate(notification.createdAtUtc, { dateStyle: 'full', timeStyle: 'short' })}</TooltipContent>
          </Tooltip>
        </div>
        {body && <p className="text-muted-foreground text-sm">{body}</p>}
        {quote && <blockquote className="text-muted-foreground border-l-2 pl-3 text-sm italic">{quote}</blockquote>}
        {notification.link && (
          <div>
            <Button asChild variant="link" size="sm" className="h-auto px-0">
              <Link
                to={notification.link}
                onClick={() => {
                  if (isUnread) {
                    toggleRead.mutate()
                  }
                }}
              >
                {t('notifications.open')}
                <ExternalLink />
              </Link>
            </Button>
          </div>
        )}
      </div>

      <div className="flex shrink-0 items-start gap-1">
        <Button
          variant="ghost"
          size="icon"
          aria-label={isUnread ? t('notifications.markRead') : t('notifications.markUnread')}
          title={isUnread ? t('notifications.markRead') : t('notifications.markUnread')}
          disabled={toggleRead.isPending}
          onClick={() => toggleRead.mutate()}
        >
          {isUnread ? <CircleCheck /> : <Circle />}
        </Button>
        <Button
          variant="ghost"
          size="icon"
          aria-label={t('common.delete')}
          title={t('common.delete')}
          disabled={remove.isPending}
          onClick={() => remove.mutate()}
        >
          <Trash2 />
        </Button>
      </div>
    </li>
  )
}

