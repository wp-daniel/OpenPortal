import { useMutation } from '@tanstack/react-query'
import { FileDown, FileUp, Loader2 } from 'lucide-react'
import { useState } from 'react'
import { userAdminApi } from '@/api/users'
import type { UserImportResult } from '@/api/types'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Switch } from '@/components/ui/switch'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { notify } from '@/hooks/useToast'
import { translate } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { reportFormError } from '@/lib/forms'

/** The columns the import reads, in the template file. */
const TEMPLATE =
  'email,firstName,lastName,phoneNumber,jobTitle,company,department,addressLine,postalCode,city,country,groups,organizations\n' +
  'ada@example.com,Ada,Lovelace,,Engineer,Example Ltd,R&D,,,,GB,Engineering|Support,example\n'

const TEMPLATE_URL = `data:text/csv;charset=utf-8,${encodeURIComponent(TEMPLATE)}`

const OUTCOME_VARIANTS = { invited: 'success', updated: 'secondary', skipped: 'outline', error: 'destructive' } as const

/**
 * Imports a CSV in two steps: a dry run shows what would happen to each row (nothing is written), then the import
 * itself. New addresses are invited; existing accounts are updated only when asked.
 */
export function UserImportDialog({ open, onClose, onImported }: { open: boolean; onClose: () => void; onImported: () => Promise<void> }) {
  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-3xl">
        {open && <ImportForm onClose={onClose} onImported={onImported} />}
      </DialogContent>
    </Dialog>
  )
}

function ImportForm({ onClose, onImported }: { onClose: () => void; onImported: () => Promise<void> }) {
  const { t } = useI18n()
  const [file, setFile] = useState<File | null>(null)
  const [updateExisting, setUpdateExisting] = useState(false)
  const [preview, setPreview] = useState<UserImportResult | null>(null)

  const run = useMutation({
    mutationFn: (dryRun: boolean) => userAdminApi.importCsv(file!, { dryRun, updateExisting }),
    meta: { handlesErrors: true },
    onSuccess: async (result) => {
      if (result.dryRun) {
        setPreview(result)
        return
      }

      notify.success(t('users.import.done', { invited: result.invited, updated: result.updated, failed: result.failed }))
      await onImported()
      setPreview(result)
    },
    onError: (failure) => reportFormError(failure, t('users.import.failed')),
  })

  const finished = preview !== null && !preview.dryRun

  return (
    <div className="grid gap-5">
      <DialogHeader>
        <DialogTitle>{t('users.import.title')}</DialogTitle>
        <DialogDescription>{t('users.import.description')}</DialogDescription>
      </DialogHeader>

      {!finished && (
        <div className="grid gap-4">
          <div className="grid gap-2">
            <Label htmlFor="import-file">{t('users.import.file')}</Label>
            <Input
              id="import-file"
              type="file"
              accept=".csv,text/csv"
              onChange={(event) => {
                setFile(event.target.files?.[0] ?? null)
                setPreview(null)
              }}
            />
            <p className="text-muted-foreground text-xs">
              {t('users.import.fileHint')}{' '}
              <a href={TEMPLATE_URL} download="openportal-users-template.csv" className="inline-flex items-center gap-1 underline">
                <FileDown className="size-3" />
                {t('users.import.template')}
              </a>
            </p>
          </div>

          <div className="flex items-start justify-between gap-6 rounded-lg border p-4">
            <div className="grid gap-1">
              <Label htmlFor="import-update">{t('users.import.updateExisting')}</Label>
              <p className="text-muted-foreground text-sm">{t('users.import.updateExistingHint')}</p>
            </div>
            <Switch
              id="import-update"
              checked={updateExisting}
              onCheckedChange={(checked) => {
                setUpdateExisting(checked)
                setPreview(null)
              }}
            />
          </div>
        </div>
      )}

      {preview && <ImportSummary result={preview} />}

      <DialogFooter>
        <Button variant="outline" onClick={onClose}>
          {finished ? t('common.close') : t('common.cancel')}
        </Button>
        {!finished && (
          <>
            <Button variant="secondary" disabled={!file || run.isPending} onClick={() => run.mutate(true)}>
              {run.isPending && run.variables === true && <Loader2 className="animate-spin" />}
              {t('users.import.check')}
            </Button>
            <Button disabled={!file || !preview || run.isPending} onClick={() => run.mutate(false)}>
              {run.isPending && run.variables === false ? <Loader2 className="animate-spin" /> : <FileUp />}
              {t('users.import.run')}
            </Button>
          </>
        )}
      </DialogFooter>
    </div>
  )
}

function ImportSummary({ result }: { result: UserImportResult }) {
  const { t } = useI18n()

  return (
    <div className="grid gap-3">
      <div className="flex flex-wrap gap-2 text-sm">
        <Badge variant="success">{t('users.import.outcome.invited')}: {result.invited}</Badge>
        <Badge variant="secondary">{t('users.import.outcome.updated')}: {result.updated}</Badge>
        <Badge variant="outline">{t('users.import.outcome.skipped')}: {result.skipped}</Badge>
        <Badge variant="destructive">{t('users.import.outcome.error')}: {result.failed}</Badge>
        {result.dryRun && <span className="text-muted-foreground">{t('users.import.dryRunNote')}</span>}
      </div>
      <ScrollArea className="h-64 rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="w-16">{t('users.import.row')}</TableHead>
              <TableHead>{t('common.email')}</TableHead>
              <TableHead>{t('common.status')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {result.rows.map((row) => (
              <TableRow key={row.row}>
                <TableCell className="tabular-nums">{row.row}</TableCell>
                <TableCell className="font-mono text-xs break-all">{row.email || '—'}</TableCell>
                <TableCell>
                  <div className="grid gap-1">
                    <Badge variant={OUTCOME_VARIANTS[row.outcome]} className="w-fit">
                      {t(`users.import.outcome.${row.outcome}`)}
                    </Badge>
                    {row.errorCode && (
                      <span className="text-muted-foreground text-xs">
                        {translate(`error.${row.errorCode}`) === `error.${row.errorCode}` ? row.errorCode : translate(`error.${row.errorCode}`)}
                      </span>
                    )}
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </ScrollArea>
    </div>
  )
}
