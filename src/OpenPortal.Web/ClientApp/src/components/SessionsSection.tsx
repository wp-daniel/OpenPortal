import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Globe, Headset, LogOut, Monitor, Smartphone } from 'lucide-react'
import { useState } from 'react'
import { accountApi } from '@/api/auth'
import type { SessionInfo } from '@/api/types'
import { Section } from '@/components/Section'
import { ErrorPanel, LoadingState } from '@/components/StatePanels'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import { useSession } from '@/hooks/useSession'
import { notify } from '@/hooks/useToast'
import { formatDate, formatRelative } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'

const SESSIONS_KEY = ['account-sessions'] as const

/** Phones and tablets get a phone icon; everything else a monitor. */
function DeviceIcon({ device }: { device: string | null }) {
  const mobile = device !== null && /iOS|Android/.test(device)
  const Icon = mobile ? Smartphone : device ? Monitor : Globe

  return <Icon className="text-muted-foreground size-5 shrink-0" />
}

/** One session as a list row: device, address, when, how it was established; an action at the end. */
export function SessionRow({ session, action }: { session: SessionInfo; action?: React.ReactNode }) {
  const { t } = useI18n()
  const ended = session.endedAtUtc ?? (session.endReason === 'expired' ? session.expiresAtUtc : null)

  return (
    <li className="flex items-center gap-3 p-3">
      <DeviceIcon device={session.device} />
      <div className="grid min-w-0 flex-1 gap-0.5">
        <span className="flex flex-wrap items-center gap-2 text-sm font-medium">
          <span className="truncate">{session.device ?? t('sessions.unknownDevice')}</span>
          {session.current && <Badge variant="success">{t('sessions.thisDevice')}</Badge>}
          {session.supportSession && (
            <Badge variant="warning">
              <Headset />
              {t('sessions.support')}
            </Badge>
          )}
          {session.methods.includes('mfa') && <Badge variant="outline">{t('sessions.method.mfa')}</Badge>}
          {session.methods.includes('fed') && <Badge variant="outline">{t('sessions.method.fed')}</Badge>}
        </span>
        <span className="text-muted-foreground text-xs">
          {[
            session.ipAddress,
            t('sessions.signedIn', { date: formatDate(session.createdAtUtc, { dateStyle: 'medium', timeStyle: 'short' }) }),
            ended
              ? t(`sessions.ended.${session.endReason ?? 'signed_out'}`, { when: formatRelative(ended) })
              : t('sessions.lastSeen', { when: formatRelative(session.lastSeenAtUtc) }),
          ]
            .filter(Boolean)
            .join(' · ')}
        </span>
      </div>
      {action}
    </li>
  )
}

/**
 * Where the account is signed in (each browser is a session the portal keeps), ending one left open elsewhere or all
 * of them but this one, and the recent sign-ins.
 */
export function SessionsSection() {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const { session: current } = useSession()
  const [historyOpen, setHistoryOpen] = useState(false)

  const sessions = useQuery({ queryKey: SESSIONS_KEY, queryFn: ({ signal }) => accountApi.sessions(signal) })
  const refresh = () => queryClient.invalidateQueries({ queryKey: SESSIONS_KEY })

  const revoke = useMutation({
    mutationFn: (session: SessionInfo) => accountApi.revokeSession(session.id),
    onSuccess: async () => {
      notify.success(t('sessions.revoked'))
      await refresh()
    },
  })

  const revokeOthers = useMutation({
    mutationFn: () => accountApi.revokeOtherSessions(),
    onSuccess: async (result) => {
      notify.success(t('sessions.othersRevoked', { count: result.count }))
      await refresh()
    },
  })

  // A support session cannot end the owner's sessions (the server refuses it too).
  const mayRevoke = current.user?.impersonator == null
  const others = sessions.data?.active.filter((session) => !session.current).length ?? 0

  return (
    <Section
      title={t('sessions.title')}
      description={t('sessions.description')}
      actions={
        mayRevoke && others > 0 ? (
          <Button size="sm" variant="outline" disabled={revokeOthers.isPending} onClick={() => revokeOthers.mutate()}>
            <LogOut />
            {t('sessions.revokeOthers')}
          </Button>
        ) : undefined
      }
    >
      {sessions.isPending && <LoadingState />}
      {sessions.isError && (
        <ErrorPanel message={describeError(sessions.error)} traceId={traceIdOf(sessions.error)} onRetry={() => void sessions.refetch()} />
      )}

      {sessions.data && (
        <div className="grid gap-4">
          <ul className="divide-y rounded-lg border">
            {sessions.data.active.map((session) => (
              <SessionRow
                key={session.id}
                session={session}
                action={
                  mayRevoke && !session.current ? (
                    <Button
                      size="sm"
                      variant="ghost"
                      disabled={revoke.isPending}
                      onClick={() => revoke.mutate(session)}
                      aria-label={t('sessions.revoke')}
                    >
                      <LogOut />
                      <span className="hidden sm:inline">{t('sessions.revoke')}</span>
                    </Button>
                  ) : undefined
                }
              />
            ))}
          </ul>

          {sessions.data.recent.length > 0 && (
            <Collapsible open={historyOpen} onOpenChange={setHistoryOpen}>
              <CollapsibleTrigger asChild>
                <Button variant="link" size="sm" className="px-0">
                  {historyOpen ? t('sessions.hideHistory') : t('sessions.showHistory', { count: sessions.data.recent.length })}
                </Button>
              </CollapsibleTrigger>
              <CollapsibleContent>
                <ul className="mt-2 divide-y rounded-lg border opacity-80">
                  {sessions.data.recent.map((session) => (
                    <SessionRow key={session.id} session={session} />
                  ))}
                </ul>
              </CollapsibleContent>
            </Collapsible>
          )}
        </div>
      )}
    </Section>
  )
}
