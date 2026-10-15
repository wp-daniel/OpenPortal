import { useMutation } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { useState, type FormEvent, type ReactNode } from 'react'
import { ApiError } from '@/api/client'
import { FormField } from '@/components/FormField'
import { Button } from '@/components/ui/button'
import { DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { useI18n } from '@/i18n/useI18n'
import { reportFormError } from '@/lib/forms'

const WRONG_PASSWORD = new Set(['identity.password_incorrect', 'identity.current_password_incorrect'])

/**
 * Asks for the account's password again before an operation on the whole account (render it inside a
 * `DialogContent`, so it starts empty on every opening). A wrong password shows under the field; anything else is
 * a toast.
 */
export function PasswordPrompt<T>({
  title,
  description,
  confirmLabel,
  destructive = false,
  failureMessage,
  children,
  onConfirm,
  onDone,
  onCancel,
}: {
  title: ReactNode
  description: ReactNode
  confirmLabel: ReactNode
  destructive?: boolean
  failureMessage: string
  children?: ReactNode
  onConfirm: (password: string) => Promise<T>
  onDone: (result: T) => void | Promise<void>
  onCancel: () => void
}) {
  const { t } = useI18n()
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | undefined>()

  const confirm = useMutation({
    mutationFn: () => onConfirm(password),
    meta: { handlesErrors: true },
    onSuccess: (result) => onDone(result),
    onError: (failure) => {
      if (failure instanceof ApiError && WRONG_PASSWORD.has(failure.problem?.errorCode ?? '')) {
        setError(t('account.passwordIncorrect'))
        return
      }

      reportFormError(failure, failureMessage)
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
        <DialogTitle>{title}</DialogTitle>
        <DialogDescription>{description}</DialogDescription>
      </DialogHeader>

      {children}

      <FormField label={t('account.currentPassword')} htmlFor="password-prompt" error={error}>
        <Input
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
        <Button type="submit" variant={destructive ? 'destructive' : 'default'} disabled={confirm.isPending}>
          {confirm.isPending && <Loader2 className="animate-spin" />}
          {confirmLabel}
        </Button>
      </DialogFooter>
    </form>
  )
}
