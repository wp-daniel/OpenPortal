import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Clock, KeyRound, Loader2, Undo2, XCircle } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { myAccessRequestsApi, requestKeys } from '@/api/requests'
import type { RequestableApplication } from '@/api/types'
import { FormField } from '@/components/FormField'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Textarea } from '@/components/ui/textarea'
import { notify } from '@/hooks/useToast'
import { formatRelative } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { reportFormError } from '@/lib/forms'

/** Longest reason a request may carry (`AccessRequest.ReasonMaxLength` on the server). */
const REASON_MAX_LENGTH = 500

/** Refreshes what depends on the user's requests: the catalog and their own list. */
function useRequestRefresh() {
  const queryClient = useQueryClient()

  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: requestKeys.catalog }),
      queryClient.invalidateQueries({ queryKey: requestKeys.mine }),
    ])
}

/**
 * Where the user stands with an application they cannot open: a button to ask for it, the request waiting (which
 * they can withdraw), or the last refusal with the reviewer's note (and the button to ask again).
 */
export function RequestAccessControl({ application }: { application: RequestableApplication }) {
  const { t } = useI18n()
  const refresh = useRequestRefresh()
  const [asking, setAsking] = useState(false)
  const latest = application.latestRequest

  const cancel = useMutation({
    mutationFn: (id: string) => myAccessRequestsApi.cancel(id),
    onSuccess: async () => {
      notify.success(t('requests.cancelled'), application.displayName)
      await refresh()
    },
  })

  if (latest?.status === 'pending') {
    return (
      <div className="grid gap-2">
        <Badge variant="warning" className="gap-1">
          <Clock />
          {t('requests.pendingSince', { when: formatRelative(latest.createdAtUtc) })}
        </Badge>
        <Button variant="ghost" size="sm" className="justify-self-start" disabled={cancel.isPending} onClick={() => cancel.mutate(latest.id)}>
          <Undo2 />
          {t('requests.cancel')}
        </Button>
      </div>
    )
  }

  return (
    <div className="grid gap-2">
      {latest?.status === 'rejected' && (
        <div className="text-muted-foreground grid gap-1 text-xs">
          <span className="text-destructive flex items-center gap-1 font-medium">
            <XCircle className="size-3.5" />
            {t('requests.rejectedOn', { when: formatRelative(latest.decidedAtUtc ?? latest.createdAtUtc) })}
          </span>
          {latest.decisionNote && <span className="italic">“{latest.decisionNote}”</span>}
        </div>
      )}

      <Button size="sm" variant="outline" className="justify-self-start" onClick={() => setAsking(true)}>
        <KeyRound />
        {latest?.status === 'rejected' ? t('requests.askAgain') : t('requests.ask')}
      </Button>

      <Dialog open={asking} onOpenChange={setAsking}>
        <DialogContent>{asking && <RequestForm application={application} onDone={() => setAsking(false)} />}</DialogContent>
      </Dialog>
    </div>
  )
}

/** The dialog body: why the user needs the application, for the reviewer. */
function RequestForm({ application, onDone }: { application: RequestableApplication; onDone: () => void }) {
  const { t } = useI18n()
  const refresh = useRequestRefresh()
  const [reason, setReason] = useState('')
  const [error, setError] = useState<string | undefined>()

  const submit = useMutation({
    mutationFn: () => myAccessRequestsApi.submit(application.id, reason.trim() === '' ? null : reason.trim()),
    meta: { handlesErrors: true },
    onSuccess: async () => {
      notify.success(t('requests.sent'), t('requests.sentDescription', { application: application.displayName }))
      onDone()
      await refresh()
    },
    onError: (failure) => reportFormError(failure, t('requests.failed')),
  })

  function send(event: FormEvent) {
    event.preventDefault()

    if (reason.trim().length > REASON_MAX_LENGTH) {
      setError(t('requests.reasonMax', { max: REASON_MAX_LENGTH }))

      return
    }

    setError(undefined)
    submit.mutate()
  }

  return (
    <>
      <DialogHeader>
        <DialogTitle>{t('requests.dialogTitle', { application: application.displayName })}</DialogTitle>
        <DialogDescription>{t('requests.dialogDescription')}</DialogDescription>
      </DialogHeader>

      <form id="access-request-form" onSubmit={send} noValidate className="grid items-start gap-5">
        <FormField label={t('requests.reason')} htmlFor="access-request-reason" hint={t('requests.reasonHint')} error={error}>
          <Textarea
            rows={4}
            maxLength={REASON_MAX_LENGTH}
            value={reason}
            onChange={(event) => setReason(event.target.value)}
          />
        </FormField>
      </form>

      <DialogFooter>
        <Button variant="outline" onClick={onDone}>
          {t('common.cancel')}
        </Button>
        <Button type="submit" form="access-request-form" disabled={submit.isPending}>
          {submit.isPending && <Loader2 className="animate-spin" />}
          {t('requests.send')}
        </Button>
      </DialogFooter>
    </>
  )
}
