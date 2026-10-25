import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Link2, Loader2, Unlink } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { accountApi } from '@/api/auth'
import type { ExternalLogin, ExternalProviderKey } from '@/api/types'
import { ExternalProviderIcon } from '@/components/ExternalProviders'
import { Section } from '@/components/Section'
import { ErrorPanel, LoadingState } from '@/components/StatePanels'
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
import { Button } from '@/components/ui/button'
import { notify } from '@/hooks/useToast'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'
import { EXTERNAL_LOGINS_KEY, useExternalLogins } from '@/lib/externalLogins'
import { reportFormError } from '@/lib/forms'
import { postForm } from '@/lib/formPost'

/**
 * Google and Microsoft accounts connected to this one (sign in with either), connecting another (a round trip to the
 * provider) and disconnecting one. Shown while a provider is on or something is connected.
 */
export function ExternalLoginsSection() {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const [searchParams, setSearchParams] = useSearchParams()
  const logins = useExternalLogins()
  const [removing, setRemoving] = useState<ExternalLogin | null>(null)
  const [leaving, setLeaving] = useState<ExternalProviderKey | null>(null)

  // Coming back from the provider: say how it went once, then drop the marker from the address.
  useEffect(() => {
    const linked = searchParams.get('external')
    const error = searchParams.get('externalError')
    if (!linked && !error) {
      return
    }

    if (error) {
      notify.error(t('externalLogins.linkFailed'), t(`error.${error}`))
    } else {
      notify.success(t('externalLogins.linked'))
    }

    const next = new URLSearchParams(searchParams)
    next.delete('external')
    next.delete('externalError')
    setSearchParams(next, { replace: true })
  }, [searchParams, setSearchParams, t])

  const remove = useMutation({
    mutationFn: (login: ExternalLogin) => accountApi.removeExternalLogin(login.provider),
    meta: { handlesErrors: true },
    onSuccess: async () => {
      notify.success(t('externalLogins.removed'))
      setRemoving(null)
      await queryClient.invalidateQueries({ queryKey: EXTERNAL_LOGINS_KEY })
    },
    onError: (failure) => {
      setRemoving(null)
      reportFormError(failure, t('externalLogins.removeFailed'))
    },
  })

  function connect(provider: ExternalProviderKey) {
    setLeaving(provider)
    void postForm(`/api/account/external-logins/${provider}`).catch(() => setLeaving(null))
  }

  if (logins.data && logins.data.logins.length === 0 && logins.data.available.length === 0) {
    return null
  }

  return (
    <Section title={t('externalLogins.title')} description={t('externalLogins.description')}>
      {logins.isPending && <LoadingState />}
      {logins.isError && (
        <ErrorPanel message={describeError(logins.error)} traceId={traceIdOf(logins.error)} onRetry={() => void logins.refetch()} />
      )}

      {logins.data && (
        <div className="grid gap-4">
          {logins.data.logins.length > 0 ? (
            <ul className="divide-y rounded-lg border">
              {logins.data.logins.map((login) => (
                <li key={login.provider} className="flex items-center gap-3 p-3">
                  <ExternalProviderIcon provider={login.provider} className="size-5 shrink-0" />
                  <div className="grid min-w-0 flex-1 gap-0.5">
                    <span className="truncate text-sm font-medium">{login.displayName}</span>
                    {login.accountName && <span className="text-muted-foreground truncate text-xs">{login.accountName}</span>}
                  </div>
                  <Button size="sm" variant="ghost" onClick={() => setRemoving(login)}>
                    <Unlink />
                    {t('externalLogins.disconnect')}
                  </Button>
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-muted-foreground text-sm">{t('externalLogins.none')}</p>
          )}

          {logins.data.available.length > 0 && (
            <div className="flex flex-wrap gap-2">
              {logins.data.available.map((provider) => (
                <Button key={provider.key} variant="outline" size="sm" disabled={leaving !== null} onClick={() => connect(provider.key)}>
                  {leaving === provider.key ? <Loader2 className="animate-spin" /> : <Link2 />}
                  {t('externalLogins.connect', { provider: provider.displayName })}
                </Button>
              ))}
            </div>
          )}
        </div>
      )}

      <AlertDialog open={removing !== null} onOpenChange={(open) => !open && setRemoving(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('externalLogins.removeTitle', { provider: removing?.displayName ?? '' })}</AlertDialogTitle>
            <AlertDialogDescription>{t('externalLogins.removeDescription')}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t('common.cancel')}</AlertDialogCancel>
            <AlertDialogAction
              onClick={(event) => {
                event.preventDefault()
                if (removing) remove.mutate(removing)
              }}
              disabled={remove.isPending}
            >
              {remove.isPending && <Loader2 className="animate-spin" />}
              {t('externalLogins.disconnect')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </Section>
  )
}
