import { Check, Copy, KeyRound } from 'lucide-react'
import { useState } from 'react'
import type { ApplicationSecret } from '@/api/types'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
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
import { Label } from '@/components/ui/label'
import { notify } from '@/hooks/useToast'
import { useI18n } from '@/i18n/useI18n'

/**
 * Shows a freshly issued client secret. The server stores only a hash, so this is the one chance to copy it;
 * the dialog says so and closes only when the administrator dismisses it.
 */
export function SecretDialog({ secret, onClose }: { secret: ApplicationSecret | null; onClose: () => void }) {
  const { t } = useI18n()
  const [copied, setCopied] = useState(false)

  async function copy(value: string) {
    try {
      await navigator.clipboard.writeText(value)
      setCopied(true)
      notify.success(t('applications.secret.copied'))
    } catch {
      notify.warning(t('applications.secret.copyFailed'))
    }
  }

  const settings = secret
    ? JSON.stringify(
        {
          OpenPortal: {
            ClientId: secret.application.clientId,
            ClientSecret: secret.clientSecret,
          },
        },
        null,
        2,
      )
    : ''

  return (
    <Dialog
      open={secret !== null}
      onOpenChange={(open) => {
        if (!open) {
          setCopied(false)
          onClose()
        }
      }}
    >
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <KeyRound className="size-5" />
            {t('applications.secret.title')}
          </DialogTitle>
          <DialogDescription>
            {t('applications.secret.description', { name: secret?.application.displayName ?? '' })}
          </DialogDescription>
        </DialogHeader>

        {secret && (
          <div className="grid gap-4">
            <Alert>
              <AlertTitle>{t('applications.secret.onceTitle')}</AlertTitle>
              <AlertDescription>{t('applications.secret.onceDescription')}</AlertDescription>
            </Alert>

            <div className="grid gap-2">
              <Label htmlFor="client-secret">{t('applications.secret.label')}</Label>
              <div className="flex gap-2">
                <Input id="client-secret" readOnly value={secret.clientSecret} className="font-mono text-xs" onFocus={(event) => event.target.select()} />
                <Button variant="outline" size="icon" aria-label={t('applications.secret.copy')} onClick={() => void copy(secret.clientSecret)}>
                  {copied ? <Check /> : <Copy />}
                </Button>
              </div>
            </div>

            <div className="grid gap-2">
              <p className="text-muted-foreground text-sm">{t('applications.secret.settingsHint')}</p>
              <pre className="bg-muted overflow-x-auto rounded-md p-3 font-mono text-xs">{settings}</pre>
            </div>
          </div>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => void copy(settings)}>
            <Copy />
            {t('applications.secret.copySettings')}
          </Button>
          <Button onClick={onClose}>{t('applications.secret.done')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
