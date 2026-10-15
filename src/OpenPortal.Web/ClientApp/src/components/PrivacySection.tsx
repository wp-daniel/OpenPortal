import { useQueryClient } from '@tanstack/react-query'
import { Download, FileText, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { accountApi } from '@/api/auth'
import { PasswordPrompt } from '@/components/PasswordPrompt'
import { Section } from '@/components/Section'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent } from '@/components/ui/dialog'
import { useSession } from '@/hooks/useSession'
import { notify } from '@/hooks/useToast'
import { formatDate } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { saveFile } from '@/lib/download'

/**
 * The account holder's privacy rights: download a copy of their data, read the documents in force, and ask for the
 * account to be deleted (when the portal allows it). Both operations ask for the password again.
 */
export function PrivacySection() {
  const { t } = useI18n()
  const { session } = useSession()
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const [dialog, setDialog] = useState<'export' | 'delete' | null>(null)

  return (
    <>
      <Section title={t('privacy.export.title')} description={t('privacy.export.description')}>
        <div className="flex flex-wrap items-center gap-3">
          <Button variant="outline" onClick={() => setDialog('export')}>
            <Download />
            {t('privacy.export.action')}
          </Button>
          {session.legal.privacy && (
            <Button variant="ghost" asChild>
              <Link to="/privacy">
                <FileText />
                {t('legal.privacy.title')}
              </Link>
            </Button>
          )}
          {session.legal.terms && (
            <Button variant="ghost" asChild>
              <Link to="/terms">
                <FileText />
                {t('legal.terms.title')}
              </Link>
            </Button>
          )}
        </div>
      </Section>

      {session.features.selfDeletion && (
        <Section title={t('privacy.delete.title')} description={t('privacy.delete.description')}>
          <Button variant="destructive" onClick={() => setDialog('delete')}>
            <Trash2 />
            {t('privacy.delete.action')}
          </Button>
        </Section>
      )}

      <Dialog open={dialog !== null} onOpenChange={(open) => !open && setDialog(null)}>
        <DialogContent>
          {dialog === 'export' && (
            <PasswordPrompt
              title={t('privacy.export.title')}
              description={t('privacy.export.confirm')}
              confirmLabel={t('privacy.export.action')}
              failureMessage={t('privacy.export.failed')}
              onConfirm={(password) => accountApi.exportData(password)}
              onDone={(file) => {
                saveFile(file)
                setDialog(null)
                notify.success(t('privacy.export.done'))
              }}
              onCancel={() => setDialog(null)}
            />
          )}
          {dialog === 'delete' && (
            <PasswordPrompt
              title={t('privacy.delete.confirmTitle')}
              description={t('privacy.delete.confirm')}
              confirmLabel={t('privacy.delete.action')}
              destructive
              failureMessage={t('privacy.delete.failed')}
              onConfirm={(password) => accountApi.requestDeletion(password)}
              onDone={(deletion) => {
                // The server signed the account out: start over at the sign-in page.
                queryClient.clear()
                notify.info(
                  t('privacy.delete.scheduled', {
                    date: formatDate(deletion.scheduledAtUtc, { dateStyle: 'long' }),
                  }),
                )
                navigate('/sign-in', { replace: true })
              }}
              onCancel={() => setDialog(null)}
            >
              <Alert variant="destructive">
                <AlertTitle>{t('privacy.delete.warningTitle')}</AlertTitle>
                <AlertDescription>{t('privacy.delete.warning')}</AlertDescription>
              </Alert>
            </PasswordPrompt>
          )}
        </DialogContent>
      </Dialog>
    </>
  )
}
