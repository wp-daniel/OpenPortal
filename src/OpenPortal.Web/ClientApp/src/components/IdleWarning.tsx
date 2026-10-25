import { useQueryClient } from '@tanstack/react-query'
import { Clock } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { authApi } from '@/api/auth'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { SESSION_QUERY_KEY, useSession } from '@/hooks/useSession'
import { notify } from '@/hooks/useToast'
import { useI18n } from '@/i18n/useI18n'

/** Shared between tabs, so working in one keeps the others from warning. */
const ACTIVITY_KEY = 'openportal-activity'

const MINUTE = 60_000

/** The server records activity at most once a minute, so the client counts from its last ping and ends a minute early. */
const PING_INTERVAL = MINUTE
const SAFETY_MARGIN = MINUTE
const WARNING_AHEAD = 2 * MINUTE

const ACTIVITY_EVENTS = ['pointerdown', 'keydown', 'wheel', 'touchstart'] as const

function readShared(): number {
  try {
    return Number(window.localStorage.getItem(ACTIVITY_KEY)) || 0
  } catch {
    return 0
  }
}

function writeShared(value: number) {
  try {
    window.localStorage.setItem(ACTIVITY_KEY, String(value))
  } catch {
    // Private mode or blocked storage: each tab keeps its own clock.
  }
}

/**
 * Warns before the portal's idle timeout ends the session (Settings → Security). Using the page (a click, a key)
 * tells the server the person is still there, at most once a minute; the notification bell's own polling does not.
 * Two minutes before the end a dialog counts down, with a way to stay signed in.
 */
export function IdleWarning() {
  const { t } = useI18n()
  const { session } = useSession()
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const idleMinutes = session.isAuthenticated ? session.features.idleTimeoutMinutes : 0
  const lastPing = useRef(Date.now())
  const warning = useRef(false)
  const [remaining, setRemaining] = useState<number | null>(null)

  useEffect(() => {
    if (idleMinutes <= 0) {
      return
    }

    lastPing.current = Math.max(Date.now(), readShared())

    const ping = (force: boolean) => {
      const now = Date.now()
      if (!force && now - Math.max(lastPing.current, readShared()) < PING_INTERVAL) {
        return
      }

      lastPing.current = now
      writeShared(now)
      void authApi.session().catch(() => undefined)
    }

    const onActivity = () => {
      // While the warning shows, only its button counts: a stray key press must not hide it unread.
      if (!warning.current) ping(false)
    }

    const onStorage = (event: StorageEvent) => {
      if (event.key === ACTIVITY_KEY) {
        lastPing.current = Math.max(lastPing.current, Number(event.newValue) || 0)
      }
    }

    const tick = window.setInterval(() => {
      const deadline = Math.max(lastPing.current, readShared()) + idleMinutes * MINUTE - SAFETY_MARGIN
      const left = deadline - Date.now()

      if (left <= 0) {
        window.clearInterval(tick)
        warning.current = false
        setRemaining(null)
        queryClient.clear()
        notify.info(t('idle.signedOut'))
        void navigate('/sign-in', { replace: true })
        void queryClient.invalidateQueries({ queryKey: SESSION_QUERY_KEY })

        return
      }

      warning.current = left <= WARNING_AHEAD
      setRemaining(warning.current ? left : null)
    }, 1000)

    for (const name of ACTIVITY_EVENTS) window.addEventListener(name, onActivity, { passive: true })
    window.addEventListener('storage', onStorage)

    return () => {
      window.clearInterval(tick)
      for (const name of ACTIVITY_EVENTS) window.removeEventListener(name, onActivity)
      window.removeEventListener('storage', onStorage)
    }
  }, [idleMinutes, navigate, queryClient, t])

  function stay() {
    const now = Date.now()
    lastPing.current = now
    writeShared(now)
    warning.current = false
    setRemaining(null)
    void authApi.session().catch(() => undefined)
  }

  async function signOut() {
    warning.current = false
    setRemaining(null)
    try {
      await authApi.logout()
    } finally {
      queryClient.clear()
      await navigate('/sign-in', { replace: true })
    }
  }

  const seconds = remaining === null ? 0 : Math.ceil(remaining / 1000)
  const clock = `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`

  return (
    <AlertDialog open={remaining !== null}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle className="flex items-center gap-2">
            <Clock className="size-5" />
            {t('idle.title')}
          </AlertDialogTitle>
          <AlertDialogDescription>{t('idle.description', { time: clock })}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel onClick={() => void signOut()}>{t('nav.signOut')}</AlertDialogCancel>
          <AlertDialogAction onClick={stay}>{t('idle.stay')}</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
