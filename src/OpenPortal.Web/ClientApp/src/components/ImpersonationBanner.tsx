import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Headset, Loader2, LogOut } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { authApi } from '@/api/auth'
import { Button } from '@/components/ui/button'
import { useSession } from '@/hooks/useSession'
import { formatDate } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'

/** Ends the support session and goes back to the administrator's own account, starting from a clean cache. */
export function useStopImpersonation() {
  const queryClient = useQueryClient()
  const navigate = useNavigate()

  return useMutation({
    mutationFn: () => authApi.stopImpersonation(),
    onSettled: () => {
      // Nothing cached as the other account may leak into the administrator's screens.
      queryClient.clear()
      navigate('/admin/users', { replace: true })
    },
  })
}

/**
 * Always on top while an administrator acts as another account: whose account it is, who is behind it, when it ends
 * by itself, and the way back.
 */
export function ImpersonationBanner() {
  const { t } = useI18n()
  const { user } = useSession()
  const stop = useStopImpersonation()

  if (!user?.impersonator) {
    return null
  }

  return (
    <div
      role="status"
      className="bg-warning text-background flex flex-wrap items-center justify-center gap-x-3 gap-y-1 px-4 py-2 text-sm"
    >
      <Headset className="size-4 shrink-0" />
      <span>
        {t('impersonation.banner', {
          user: user.displayName,
          admin: user.impersonator.email ?? '',
          time: formatDate(user.impersonator.endsAtUtc, { timeStyle: 'short' }),
        })}
      </span>
      <Button
        size="sm"
        variant="secondary"
        className="h-7"
        onClick={() => stop.mutate()}
        disabled={stop.isPending}
      >
        {stop.isPending ? <Loader2 className="animate-spin" /> : <LogOut />}
        {t('impersonation.stop')}
      </Button>
    </div>
  )
}
