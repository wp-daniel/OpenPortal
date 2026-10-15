import { useMutation, useQueryClient } from '@tanstack/react-query'
import { CalendarClock, Loader2, LogOut, Undo2 } from 'lucide-react'
import { Navigate, useNavigate } from 'react-router-dom'
import { accountApi, authApi } from '@/api/auth'
import { Section } from '@/components/Section'
import { Button } from '@/components/ui/button'
import { SESSION_QUERY_KEY, useSession } from '@/hooks/useSession'
import { notify } from '@/hooks/useToast'
import { formatDate } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'

const CARD = 'bg-card/80 shadow-lg backdrop-blur'

/**
 * Shown instead of the portal while the account waits to be deleted: the date, and the two ways out (keep the
 * account, or sign out and let it go).
 */
export function DeletionPendingPage() {
  const { t } = useI18n()
  const { user } = useSession()
  const queryClient = useQueryClient()
  const navigate = useNavigate()

  const cancel = useMutation({
    mutationFn: () => accountApi.cancelDeletion(),
    onSuccess: async () => {
      notify.success(t('privacy.pending.cancelled'))
      await queryClient.invalidateQueries({ queryKey: SESSION_QUERY_KEY })
      navigate('/', { replace: true })
    },
  })

  const signOut = useMutation({
    mutationFn: () => authApi.logout(),
    onSettled: () => {
      queryClient.clear()
      navigate('/sign-in', { replace: true })
    },
  })

  if (!user?.deletionScheduledAtUtc) {
    return <Navigate to="/" replace />
  }

  return (
    <Section className={CARD}>
      <div className="grid justify-items-center gap-4 py-2 text-center">
        <CalendarClock className="text-muted-foreground size-10" />
        <h1 className="text-xl font-semibold tracking-tight">{t('privacy.pending.title')}</h1>
        <p className="text-muted-foreground text-sm">
          {t('privacy.pending.description', {
            date: formatDate(user.deletionScheduledAtUtc, { dateStyle: 'full' }),
          })}
        </p>
        <div className="flex flex-col gap-2 sm:flex-row">
          <Button onClick={() => cancel.mutate()} disabled={cancel.isPending}>
            {cancel.isPending ? <Loader2 className="animate-spin" /> : <Undo2 />}
            {t('privacy.pending.cancel')}
          </Button>
          <Button variant="outline" onClick={() => signOut.mutate()} disabled={signOut.isPending}>
            <LogOut />
            {t('nav.signOut')}
          </Button>
        </div>
      </div>
    </Section>
  )
}
