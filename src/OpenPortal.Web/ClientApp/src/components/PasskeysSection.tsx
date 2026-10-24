import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Fingerprint, Loader2, Pencil, Plus, Trash2 } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { accountApi } from '@/api/auth'
import type { Passkey } from '@/api/types'
import { FormField } from '@/components/FormField'
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
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { SESSION_QUERY_KEY, useSession } from '@/hooks/useSession'
import { notify } from '@/hooks/useToast'
import { formatDate } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'
import { reportFormError } from '@/lib/forms'
import { createPasskey, isPasskeyCancelled, passkeysSupported } from '@/lib/webauthn'

const PASSKEYS_KEY = ['account-passkeys'] as const
const NAME_MAX_LENGTH = 64

/**
 * The account's passkeys: sign in with a fingerprint, face or PIN instead of a password and code. Shown while the
 * portal offers passkeys.
 */
export function PasskeysSection() {
  const { t } = useI18n()
  const { session } = useSession()
  const queryClient = useQueryClient()
  const [renaming, setRenaming] = useState<Passkey | null>(null)
  const [removing, setRemoving] = useState<Passkey | null>(null)

  const passkeys = useQuery({ queryKey: PASSKEYS_KEY, queryFn: ({ signal }) => accountApi.passkeys(signal) })

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: PASSKEYS_KEY })
    // A first passkey may lift the portal's "set up a second factor" restriction.
    await queryClient.invalidateQueries({ queryKey: SESSION_QUERY_KEY })
  }

  const add = useMutation({
    mutationFn: async () => {
      const options = await accountApi.passkeyCreationOptions()
      const credential = await createPasskey(options)

      return accountApi.registerPasskey(credential, '')
    },
    meta: { handlesErrors: true },
    onSuccess: async (passkey) => {
      notify.success(t('passkeys.added'))
      await refresh()
      setRenaming(passkey)
    },
    onError: (failure) => {
      if (!isPasskeyCancelled(failure)) {
        reportFormError(failure, t('passkeys.addFailed'))
      }
    },
  })

  const remove = useMutation({
    mutationFn: (passkey: Passkey) => accountApi.deletePasskey(passkey.id),
    meta: { handlesErrors: true },
    onSuccess: async () => {
      notify.success(t('passkeys.removed'))
      setRemoving(null)
      await refresh()
    },
    onError: (failure) => {
      setRemoving(null)
      reportFormError(failure, t('passkeys.removeFailed'))
    },
  })

  if (!session.registration.passkeys && (passkeys.data?.length ?? 0) === 0) {
    return null
  }

  return (
    <Section
      title={
        <span className="flex items-center gap-2">
          {t('passkeys.title')}
          {(passkeys.data?.length ?? 0) > 0 && <Badge variant="success">{t('passkeys.count', { count: passkeys.data!.length })}</Badge>}
        </span>
      }
      description={t('passkeys.description')}
      actions={
        session.registration.passkeys && passkeysSupported() ? (
          <Button size="sm" onClick={() => add.mutate()} disabled={add.isPending}>
            {add.isPending ? <Loader2 className="animate-spin" /> : <Plus />}
            {t('passkeys.add')}
          </Button>
        ) : undefined
      }
    >
      {!passkeysSupported() && (
        <Alert className="mb-4">
          <AlertDescription>{t('passkeys.unsupported')}</AlertDescription>
        </Alert>
      )}

      {passkeys.isPending && <LoadingState />}
      {passkeys.isError && (
        <ErrorPanel message={describeError(passkeys.error)} traceId={traceIdOf(passkeys.error)} onRetry={() => void passkeys.refetch()} />
      )}

      {passkeys.data && passkeys.data.length === 0 && (
        <p className="text-muted-foreground text-sm">{t('passkeys.none')}</p>
      )}

      {passkeys.data && passkeys.data.length > 0 && (
        <ul className="divide-y rounded-lg border">
          {passkeys.data.map((passkey) => (
            <li key={passkey.id} className="flex items-center gap-3 p-3">
              <Fingerprint className="text-muted-foreground size-5 shrink-0" />
              <div className="grid min-w-0 flex-1 gap-0.5">
                <span className="truncate text-sm font-medium">{passkey.name}</span>
                <span className="text-muted-foreground text-xs">
                  {t('passkeys.created', { date: formatDate(passkey.createdAtUtc, { dateStyle: 'medium' }) })}
                  {passkey.backedUp && ` · ${t('passkeys.synced')}`}
                </span>
              </div>
              <Button size="sm" variant="ghost" aria-label={t('common.edit')} onClick={() => setRenaming(passkey)}>
                <Pencil />
              </Button>
              <Button size="sm" variant="ghost" aria-label={t('common.delete')} onClick={() => setRemoving(passkey)}>
                <Trash2 />
              </Button>
            </li>
          ))}
        </ul>
      )}

      <Dialog open={renaming !== null} onOpenChange={(open) => !open && setRenaming(null)}>
        <DialogContent>{renaming && <RenameForm passkey={renaming} onDone={() => setRenaming(null)} />}</DialogContent>
      </Dialog>

      <AlertDialog open={removing !== null} onOpenChange={(open) => !open && setRemoving(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('passkeys.removeTitle', { name: removing?.name ?? '' })}</AlertDialogTitle>
            <AlertDialogDescription>{t('passkeys.removeDescription')}</AlertDialogDescription>
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
              {t('common.delete')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </Section>
  )
}

/** Names a passkey ("Laptop", "YubiKey"), right after adding it or later. */
function RenameForm({ passkey, onDone }: { passkey: Passkey; onDone: () => void }) {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const [name, setName] = useState(passkey.name)
  const [error, setError] = useState<string>()

  const rename = useMutation({
    mutationFn: () => accountApi.renamePasskey(passkey.id, name.trim()),
    meta: { handlesErrors: true },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: PASSKEYS_KEY })
      onDone()
    },
    onError: (failure) => reportFormError(failure, t('passkeys.renameFailed')),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    const value = name.trim()

    if (value === '' || value.length > NAME_MAX_LENGTH) {
      setError(t('passkeys.nameInvalid'))
      return
    }

    setError(undefined)
    rename.mutate()
  }

  return (
    <form onSubmit={submit} noValidate className="grid gap-4">
      <DialogHeader>
        <DialogTitle>{t('passkeys.renameTitle')}</DialogTitle>
        <DialogDescription>{t('passkeys.renameDescription')}</DialogDescription>
      </DialogHeader>
      <FormField label={t('passkeys.name')} htmlFor="passkey-name" error={error}>
        <Input autoFocus maxLength={NAME_MAX_LENGTH} value={name} onChange={(event) => setName(event.target.value)} />
      </FormField>
      <DialogFooter>
        <Button variant="outline" onClick={onDone}>
          {t('common.cancel')}
        </Button>
        <Button type="submit" disabled={rename.isPending}>
          {rename.isPending && <Loader2 className="animate-spin" />}
          {t('common.save')}
        </Button>
      </DialogFooter>
    </form>
  )
}
