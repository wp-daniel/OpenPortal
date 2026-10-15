import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Copy, KeySquare, Loader2, Plus, Trash2 } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { accountApi } from '@/api/auth'
import type { CreatedPersonalAccessToken, PersonalAccessToken, PersonalAccessTokenScope } from '@/api/types'
import { FormField } from '@/components/FormField'
import { Section } from '@/components/Section'
import { EmptyState, ErrorPanel, LoadingState } from '@/components/StatePanels'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
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
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { Table, TableBody, TableCaption, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { notify } from '@/hooks/useToast'
import { formatDate, formatRelative } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'
import { reportFormError } from '@/lib/forms'

const TOKENS_KEY = ['account-tokens'] as const
const LIFETIMES = [7, 30, 90, 365] as const

/** A token's status as a badge (active, expired, revoked). */
export function TokenStatus({ token }: { token: PersonalAccessToken }) {
  const { t } = useI18n()

  return (
    <Badge variant={token.status === 'active' ? 'success' : 'outline'}>{t(`tokens.status.${token.status}`)}</Badge>
  )
}

/**
 * The account holder's personal access tokens for scripts and command-line tools: create (the value is shown once),
 * see when each was last used, revoke.
 */
export function TokensSection() {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const [creating, setCreating] = useState(false)
  const [created, setCreated] = useState<CreatedPersonalAccessToken | null>(null)
  const [revoking, setRevoking] = useState<PersonalAccessToken | null>(null)

  const tokens = useQuery({ queryKey: TOKENS_KEY, queryFn: ({ signal }) => accountApi.tokens(signal) })

  const revoke = useMutation({
    mutationFn: (token: PersonalAccessToken) => accountApi.revokeToken(token.id),
    onSuccess: async () => {
      setRevoking(null)
      notify.success(t('tokens.revoked'))
      await queryClient.invalidateQueries({ queryKey: TOKENS_KEY })
    },
  })

  return (
    <Section
      title={t('tokens.title')}
      description={t('tokens.description')}
      actions={
        <Button size="sm" onClick={() => setCreating(true)}>
          <Plus />
          {t('tokens.create')}
        </Button>
      }
    >
      {tokens.isPending && <LoadingState />}
      {tokens.isError && (
        <ErrorPanel message={describeError(tokens.error)} traceId={traceIdOf(tokens.error)} onRetry={() => void tokens.refetch()} />
      )}
      {tokens.data?.length === 0 && <EmptyState title={t('tokens.empty.title')} description={t('tokens.empty.description')} />}

      {tokens.data && tokens.data.length > 0 && (
        <div className="overflow-x-auto">
          <Table>
            <TableCaption className="sr-only">{t('tokens.title')}</TableCaption>
            <TableHeader>
              <TableRow>
                <TableHead>{t('tokens.column.name')}</TableHead>
                <TableHead>{t('tokens.column.scope')}</TableHead>
                <TableHead>{t('tokens.column.expires')}</TableHead>
                <TableHead>{t('tokens.column.lastUsed')}</TableHead>
                <TableHead className="text-right">{t('common.actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {tokens.data.map((token) => (
                <TableRow key={token.id}>
                  <TableCell>
                    <div className="grid">
                      <span className="font-medium">{token.name}</span>
                      <code className="text-muted-foreground text-xs">{token.prefix}…</code>
                    </div>
                  </TableCell>
                  <TableCell>
                    {token.scopes.includes('write') ? t('tokens.scope.write') : t('tokens.scope.read')}
                  </TableCell>
                  <TableCell>
                    <div className="flex flex-wrap items-center gap-2">
                      {formatDate(token.expiresAtUtc, { dateStyle: 'medium' })}
                      {token.status !== 'active' && <TokenStatus token={token} />}
                    </div>
                  </TableCell>
                  <TableCell>
                    {token.lastUsedAtUtc ? formatRelative(token.lastUsedAtUtc) : <span className="text-muted-foreground">—</span>}
                  </TableCell>
                  <TableCell className="text-right">
                    {token.status === 'active' && (
                      <Button variant="ghost" size="sm" aria-label={t('tokens.revoke')} onClick={() => setRevoking(token)}>
                        <Trash2 />
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      <Dialog
        open={creating || created !== null}
        onOpenChange={(open) => {
          if (!open) {
            setCreating(false)
            setCreated(null)
          }
        }}
      >
        <DialogContent className="sm:max-w-lg">
          {created ? (
            <CreatedToken created={created} onClose={() => setCreated(null)} />
          ) : (
            creating && (
              <CreateTokenForm
                onCancel={() => setCreating(false)}
                onCreated={async (result) => {
                  setCreating(false)
                  setCreated(result)
                  await queryClient.invalidateQueries({ queryKey: TOKENS_KEY })
                }}
              />
            )
          )}
        </DialogContent>
      </Dialog>

      <AlertDialog open={revoking !== null} onOpenChange={(open) => !open && setRevoking(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('tokens.revokeTitle', { name: revoking?.name ?? '' })}</AlertDialogTitle>
            <AlertDialogDescription>{t('tokens.revokeDescription')}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t('common.cancel')}</AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              disabled={revoke.isPending}
              onClick={(event) => {
                event.preventDefault()
                if (revoking) {
                  revoke.mutate(revoking)
                }
              }}
            >
              {t('tokens.revoke')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </Section>
  )
}

function CreateTokenForm({
  onCancel,
  onCreated,
}: {
  onCancel: () => void
  onCreated: (created: CreatedPersonalAccessToken) => Promise<void>
}) {
  const { t } = useI18n()
  const [name, setName] = useState('')
  const [scope, setScope] = useState<PersonalAccessTokenScope>('read')
  const [days, setDays] = useState<string>('30')
  const [error, setError] = useState<string>()

  const create = useMutation({
    mutationFn: () => accountApi.createToken({ name: name.trim(), scopes: [scope], lifetimeDays: Number(days) }),
    meta: { handlesErrors: true },
    onSuccess: onCreated,
    onError: (failure) => reportFormError(failure, t('tokens.createFailed')),
  })

  function submit(event: FormEvent) {
    event.preventDefault()

    if (name.trim().length === 0 || name.trim().length > 80) {
      setError(t('tokens.nameRequired'))
      return
    }

    setError(undefined)
    create.mutate()
  }

  return (
    <form onSubmit={submit} noValidate className="grid gap-5">
      <DialogHeader>
        <DialogTitle>{t('tokens.create')}</DialogTitle>
        <DialogDescription>{t('tokens.createDescription')}</DialogDescription>
      </DialogHeader>

      <FormField label={t('tokens.column.name')} htmlFor="token-name" hint={t('tokens.nameHint')} error={error}>
        <Input maxLength={80} autoFocus value={name} onChange={(event) => setName(event.target.value)} />
      </FormField>

      <fieldset className="grid gap-3">
        <legend className="mb-1 text-sm font-medium">{t('tokens.column.scope')}</legend>
        <RadioGroup value={scope} onValueChange={(value) => setScope(value as PersonalAccessTokenScope)} className="gap-2">
          {(['read', 'write'] as const).map((value) => (
            <div key={value} className="flex items-start gap-3">
              <RadioGroupItem value={value} id={`token-scope-${value}`} className="mt-0.5" />
              <div className="grid gap-0.5">
                <Label htmlFor={`token-scope-${value}`}>{t(`tokens.scope.${value}`)}</Label>
                <p className="text-muted-foreground text-xs">{t(`tokens.scope.${value}Hint`)}</p>
              </div>
            </div>
          ))}
        </RadioGroup>
      </fieldset>

      <fieldset className="grid gap-3">
        <legend className="mb-1 text-sm font-medium">{t('tokens.lifetime')}</legend>
        <RadioGroup value={days} onValueChange={setDays} className="flex flex-wrap gap-4">
          {LIFETIMES.map((value) => (
            <div key={value} className="flex items-center gap-2">
              <RadioGroupItem value={String(value)} id={`token-days-${value}`} />
              <Label htmlFor={`token-days-${value}`}>{t('tokens.days', { count: value })}</Label>
            </div>
          ))}
        </RadioGroup>
      </fieldset>

      <DialogFooter>
        <Button variant="outline" onClick={onCancel}>
          {t('common.cancel')}
        </Button>
        <Button type="submit" disabled={create.isPending}>
          {create.isPending && <Loader2 className="animate-spin" />}
          {t('tokens.create')}
        </Button>
      </DialogFooter>
    </form>
  )
}

/** The new token's value, shown once, with how to use it. */
function CreatedToken({ created, onClose }: { created: CreatedPersonalAccessToken; onClose: () => void }) {
  const { t } = useI18n()
  const [copied, setCopied] = useState(false)

  async function copy() {
    try {
      await navigator.clipboard.writeText(created.value)
      setCopied(true)
      notify.success(t('tokens.copied'))
    } catch {
      notify.warning(t('tokens.copyFailed'))
    }
  }

  const example = `curl -H "Authorization: Bearer ${created.value}" ${window.location.origin}/api/account/profile`

  return (
    <div className="grid gap-4">
      <DialogHeader>
        <DialogTitle className="flex items-center gap-2">
          <KeySquare className="size-5" />
          {created.token.name}
        </DialogTitle>
        <DialogDescription>{t('tokens.createdDescription')}</DialogDescription>
      </DialogHeader>

      <Alert>
        <AlertTitle>{t('tokens.onceTitle')}</AlertTitle>
        <AlertDescription>{t('tokens.onceDescription')}</AlertDescription>
      </Alert>

      <div className="flex gap-2">
        <Input readOnly value={created.value} className="font-mono text-xs" aria-label={t('tokens.value')} />
        <Button variant="outline" size="icon" aria-label={t('tokens.copy')} onClick={() => void copy()}>
          {copied ? <Check /> : <Copy />}
        </Button>
      </div>

      <div className="grid gap-1.5">
        <Label>{t('tokens.example')}</Label>
        <pre className="bg-muted overflow-x-auto rounded-md p-3 font-mono text-xs">{example}</pre>
      </div>

      <DialogFooter>
        <Button onClick={onClose}>{t('common.close')}</Button>
      </DialogFooter>
    </div>
  )
}
