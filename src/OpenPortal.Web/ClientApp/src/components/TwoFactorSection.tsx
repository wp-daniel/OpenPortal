import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Copy, Download, KeyRound, Loader2, ShieldCheck, ShieldOff, Smartphone } from 'lucide-react'
import { QRCodeSVG } from 'qrcode.react'
import { useState, type FormEvent } from 'react'
import { accountApi } from '@/api/auth'
import { ApiError } from '@/api/client'
import type { RecoveryCodes } from '@/api/types'
import { FormField } from '@/components/FormField'
import { OTP_LENGTH, OtpCodeInput } from '@/components/OtpCodeInput'
import { Section } from '@/components/Section'
import { ErrorPanel, LoadingState } from '@/components/StatePanels'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { notify } from '@/hooks/useToast'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'
import { reportFormError } from '@/lib/forms'

const STATUS_KEY = ['account-two-factor'] as const

/**
 * The account's authenticator app (TOTP). Shown only while the portal offers two-factor authentication, or
 * while this account is still enrolled from before an administrator turned it off (it can then still turn it
 * off itself).
 */
export function TwoFactorSection() {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const [setupOpen, setSetupOpen] = useState(false)
  const [confirming, setConfirming] = useState<'disable' | 'regenerate' | null>(null)
  const [codes, setCodes] = useState<readonly string[] | null>(null)

  const status = useQuery({ queryKey: STATUS_KEY, queryFn: ({ signal }) => accountApi.twoFactor(signal) })
  const refresh = () => queryClient.invalidateQueries({ queryKey: STATUS_KEY })

  if (status.isPending) {
    return <LoadingState />
  }

  if (status.isError) {
    return (
      <ErrorPanel
        message={describeError(status.error)}
        traceId={traceIdOf(status.error)}
        onRetry={() => void status.refetch()}
      />
    )
  }

  const { available, enabled, recoveryCodesLeft } = status.data

  if (!available && !enabled) {
    return null
  }

  return (
    <Section
      title={
        <span className="flex items-center gap-2">
          {t('twoFactor.title')}
          {enabled ? (
            <Badge variant="success">
              <ShieldCheck />
              {t('twoFactor.on')}
            </Badge>
          ) : (
            <Badge variant="secondary">{t('twoFactor.off')}</Badge>
          )}
        </span>
      }
      description={t('twoFactor.description')}
    >
      <div className="grid gap-4">
        {enabled && !available && (
          <Alert>
            <ShieldOff />
            <AlertTitle>{t('twoFactor.suspendedTitle')}</AlertTitle>
            <AlertDescription>{t('twoFactor.suspendedDescription')}</AlertDescription>
          </Alert>
        )}

        {enabled ? (
          <>
            <p className="text-sm">
              {recoveryCodesLeft <= 3 ? (
                <Badge variant="warning">{t('twoFactor.codesLeft', { count: recoveryCodesLeft })}</Badge>
              ) : (
                <span className="text-muted-foreground">{t('twoFactor.codesLeft', { count: recoveryCodesLeft })}</span>
              )}
            </p>
            <div className="flex flex-wrap gap-2">
              <Button variant="outline" onClick={() => setConfirming('regenerate')}>
                <KeyRound />
                {t('twoFactor.regenerate')}
              </Button>
              <Button variant="outline" className="text-destructive" onClick={() => setConfirming('disable')}>
                <ShieldOff />
                {t('twoFactor.disable')}
              </Button>
            </div>
          </>
        ) : (
          <div>
            <Button onClick={() => setSetupOpen(true)}>
              <Smartphone />
              {t('twoFactor.setUp')}
            </Button>
          </div>
        )}
      </div>

      <Dialog open={setupOpen} onOpenChange={(open) => !open && setSetupOpen(false)}>
        {/* Rendered only while open, so every opening starts from the first step. */}
        <DialogContent className="sm:max-w-lg">
          {setupOpen && (
            <SetupForm
              onEnabled={() => void refresh()}
              onClose={() => setSetupOpen(false)}
            />
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={confirming !== null} onOpenChange={(open) => !open && setConfirming(null)}>
        <DialogContent className="sm:max-w-md">
          {confirming && (
            <PasswordConfirmForm
              action={confirming}
              onDone={(result) => {
                setConfirming(null)
                void refresh()

                if (result) {
                  setCodes(result.codes)
                }
              }}
              onCancel={() => setConfirming(null)}
            />
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={codes !== null} onOpenChange={(open) => !open && setCodes(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t('twoFactor.codesTitle')}</DialogTitle>
            <DialogDescription>{t('twoFactor.codesDescription')}</DialogDescription>
          </DialogHeader>
          {codes && <RecoveryCodeList codes={codes} />}
          <DialogFooter>
            <Button onClick={() => setCodes(null)}>{t('twoFactor.done')}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Section>
  )
}

/**
 * Enrolment: scan the QR code (or type the key), prove it with one code, then keep the recovery codes. The key
 * is fetched when the dialog opens; the server hands back the same key until the account is enrolled, so
 * reopening the dialog does not invalidate a code already scanned.
 */
function SetupForm({ onEnabled, onClose }: { onEnabled: () => void; onClose: () => void }) {
  const { t } = useI18n()
  const [code, setCode] = useState('')
  const [error, setError] = useState<string | undefined>()
  const [copied, setCopied] = useState(false)

  const setup = useQuery({
    queryKey: ['account-two-factor-setup'],
    queryFn: () => accountApi.setupTwoFactor(),
    // A POST behind a query: fetched once per opening, never retried or kept.
    retry: false,
    gcTime: 0,
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  })

  const enable = useMutation({
    mutationFn: (value: string) => accountApi.enableTwoFactor(value),
    meta: { handlesErrors: true },
    onSuccess: () => {
      notify.success(t('twoFactor.enabled'))
      onEnabled()
    },
    onError: (failure) => {
      if (failure instanceof ApiError && failure.problem?.errorCode === 'identity.two_factor_setup_code_invalid') {
        setError(t('twoFactor.setupInvalid'))
        setCode('')

        return
      }

      reportFormError(failure, t('twoFactor.enableFailed'))
    },
  })

  function submit(event?: FormEvent) {
    event?.preventDefault()

    if (code.length !== OTP_LENGTH) {
      setError(t('signIn.twoFactor.required'))

      return
    }

    setError(undefined)
    enable.mutate(code)
  }

  async function copyKey(key: string) {
    try {
      await navigator.clipboard.writeText(key.replaceAll(' ', ''))
      setCopied(true)
      notify.success(t('twoFactor.keyCopied'))
    } catch {
      notify.warning(t('twoFactor.copyFailed'))
    }
  }

  if (enable.data) {
    return (
      <>
        <DialogHeader>
          <DialogTitle>{t('twoFactor.codesTitle')}</DialogTitle>
          <DialogDescription>{t('twoFactor.codesDescription')}</DialogDescription>
        </DialogHeader>
        <RecoveryCodeList codes={enable.data.codes} />
        <DialogFooter>
          <Button onClick={onClose}>{t('twoFactor.done')}</Button>
        </DialogFooter>
      </>
    )
  }

  return (
    <>
      <DialogHeader>
        <DialogTitle className="flex items-center gap-2">
          <Smartphone className="size-5" />
          {t('twoFactor.setupTitle')}
        </DialogTitle>
        <DialogDescription>{t('twoFactor.setupDescription')}</DialogDescription>
      </DialogHeader>

      {setup.isPending && <LoadingState />}

      {setup.isError && (
        <ErrorPanel
          message={describeError(setup.error)}
          traceId={traceIdOf(setup.error)}
          onRetry={() => void setup.refetch()}
        />
      )}

      {setup.data && (
        <form onSubmit={submit} noValidate className="grid gap-5">
          <ol className="text-muted-foreground list-decimal space-y-1 pl-5 text-sm">
            <li>{t('twoFactor.step.install')}</li>
            <li>{t('twoFactor.step.scan')}</li>
            <li>{t('twoFactor.step.code')}</li>
          </ol>

          {/* Always dark on white, whatever the theme: phone cameras read that most reliably. */}
          <div className="justify-self-center rounded-lg border bg-white p-3">
            <QRCodeSVG value={setup.data.authenticatorUri} size={176} level="M" title={t('twoFactor.qrLabel')} />
          </div>

          <div className="grid gap-2">
            <p className="text-muted-foreground text-sm">{t('twoFactor.keyHint')}</p>
            <div className="flex gap-2">
              <Input
                readOnly
                aria-label={t('twoFactor.keyLabel')}
                value={setup.data.sharedKey}
                className="font-mono text-xs"
                onFocus={(event) => event.target.select()}
              />
              <Button
                variant="outline"
                size="icon"
                aria-label={t('twoFactor.copyKey')}
                onClick={() => void copyKey(setup.data.sharedKey)}
              >
                {copied ? <Check /> : <Copy />}
              </Button>
            </div>
          </div>

          <div className="grid justify-items-center gap-2">
            <OtpCodeInput
              aria-label={t('signIn.twoFactor.codeLabel')}
              aria-describedby={error ? 'setupCode-error' : undefined}
              value={code}
              onChange={(value) => {
                setCode(value)
                setError(undefined)
              }}
              onComplete={(value) => enable.mutate(value)}
              disabled={enable.isPending}
              invalid={error !== undefined}
            />
            {error && (
              <p id="setupCode-error" className="text-destructive text-sm">
                {error}
              </p>
            )}
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={onClose}>
              {t('common.cancel')}
            </Button>
            <Button type="submit" disabled={enable.isPending}>
              {enable.isPending && <Loader2 className="animate-spin" />}
              {t('twoFactor.activate')}
            </Button>
          </DialogFooter>
        </form>
      )}
    </>
  )
}

/** Asks for the password before turning two-factor off or replacing the recovery codes. */
function PasswordConfirmForm({
  action,
  onDone,
  onCancel,
}: {
  action: 'disable' | 'regenerate'
  onDone: (codes: RecoveryCodes | null) => void
  onCancel: () => void
}) {
  const { t } = useI18n()
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | undefined>()

  const confirm = useMutation({
    mutationFn: async (): Promise<RecoveryCodes | null> => {
      if (action === 'disable') {
        await accountApi.disableTwoFactor(password)

        return null
      }

      return accountApi.regenerateRecoveryCodes(password)
    },
    meta: { handlesErrors: true },
    onSuccess: (codes) => {
      if (action === 'disable') {
        notify.success(t('twoFactor.disabled'))
      }

      onDone(codes)
    },
    onError: (failure) => {
      if (failure instanceof ApiError && failure.problem?.errorCode === 'identity.current_password_incorrect') {
        setError(t('twoFactor.passwordIncorrect'))

        return
      }

      reportFormError(failure, t('twoFactor.actionFailed'))
    },
  })

  function submit(event: FormEvent) {
    event.preventDefault()

    if (password.length === 0) {
      setError(t('validation.currentPasswordRequired'))

      return
    }

    setError(undefined)
    confirm.mutate()
  }

  return (
    <form onSubmit={submit} noValidate className="grid gap-4">
      <DialogHeader>
        <DialogTitle>{action === 'disable' ? t('twoFactor.disableTitle') : t('twoFactor.regenerateTitle')}</DialogTitle>
        <DialogDescription>
          {action === 'disable' ? t('twoFactor.disableDescription') : t('twoFactor.regenerateDescription')}
        </DialogDescription>
      </DialogHeader>

      <FormField label={t('account.currentPassword')} htmlFor="twoFactorPassword" error={error}>
        <Input
          name="twoFactorPassword"
          type="password"
          autoComplete="current-password"
          autoFocus
          value={password}
          onChange={(event) => setPassword(event.target.value)}
        />
      </FormField>

      <DialogFooter>
        <Button variant="outline" onClick={onCancel}>
          {t('common.cancel')}
        </Button>
        <Button type="submit" variant={action === 'disable' ? 'destructive' : 'default'} disabled={confirm.isPending}>
          {confirm.isPending && <Loader2 className="animate-spin" />}
          {action === 'disable' ? t('twoFactor.disable') : t('twoFactor.regenerate')}
        </Button>
      </DialogFooter>
    </form>
  )
}

/** The recovery codes, shown once, with copy and download (a plain text file made in the browser). */
function RecoveryCodeList({ codes }: { codes: readonly string[] }) {
  const { t } = useI18n()
  const text = codes.join('\n')

  async function copy() {
    try {
      await navigator.clipboard.writeText(text)
      notify.success(t('twoFactor.codesCopied'))
    } catch {
      notify.warning(t('twoFactor.copyFailed'))
    }
  }

  function download() {
    const url = URL.createObjectURL(new Blob([`${text}\n`], { type: 'text/plain' }))
    const link = document.createElement('a')
    link.href = url
    link.download = 'recovery-codes.txt'
    link.click()
    URL.revokeObjectURL(url)
  }

  return (
    <div className="grid gap-4">
      <Alert>
        <KeyRound />
        <AlertTitle>{t('twoFactor.codesOnceTitle')}</AlertTitle>
        <AlertDescription>{t('twoFactor.codesOnceDescription')}</AlertDescription>
      </Alert>
      <ul className="bg-muted grid grid-cols-2 gap-x-6 gap-y-1 rounded-md p-4 font-mono text-sm">
        {codes.map((code) => (
          <li key={code}>{code}</li>
        ))}
      </ul>
      <div className="flex flex-wrap gap-2">
        <Button variant="outline" size="sm" onClick={() => void copy()}>
          <Copy />
          {t('twoFactor.copyCodes')}
        </Button>
        <Button variant="outline" size="sm" onClick={download}>
          <Download />
          {t('twoFactor.downloadCodes')}
        </Button>
      </div>
    </div>
  )
}
