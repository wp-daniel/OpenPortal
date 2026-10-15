import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Eye, FilePlus2, Loader2, Pencil, Send, Trash2 } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { legalApi, legalKeys } from '@/api/legal'
import type { LegalDocument, LegalDocumentKind, SaveLegalDocument } from '@/api/types'
import { FormField } from '@/components/FormField'
import { Markdown } from '@/components/Markdown'
import { PageHeader } from '@/components/PageHeader'
import { Section } from '@/components/Section'
import { EmptyState, ErrorPanel, LoadingState } from '@/components/StatePanels'
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
import { ScrollArea } from '@/components/ui/scroll-area'
import { Separator } from '@/components/ui/separator'
import { Switch } from '@/components/ui/switch'
import { Table, TableBody, TableCaption, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Textarea } from '@/components/ui/textarea'
import { notify } from '@/hooks/useToast'
import { formatDate } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'
import { reportFormError } from '@/lib/forms'

const KINDS: readonly LegalDocumentKind[] = ['privacy', 'terms']

type Editing =
  | { readonly mode: 'create'; readonly kind: LegalDocumentKind; readonly from: LegalDocument | null }
  | { readonly mode: 'edit'; readonly document: LegalDocument }

/**
 * The privacy notice and the terms of use. Each is written as a draft and published as a new version; publishing a
 * version that requires acceptance asks every account to accept it at its next visit (and before signing in to any
 * application). Published versions never change, so each acceptance refers to the exact text accepted.
 */
export function AdminLegalPage() {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<Editing | null>(null)
  const [viewing, setViewing] = useState<LegalDocument | null>(null)
  const [publishing, setPublishing] = useState<LegalDocument | null>(null)
  const [deleting, setDeleting] = useState<LegalDocument | null>(null)

  const documents = useQuery({
    queryKey: legalKeys.admin,
    queryFn: ({ signal }) => legalApi.list(signal),
  })

  const refresh = () => queryClient.invalidateQueries({ queryKey: legalKeys.all })

  const publish = useMutation({
    mutationFn: (document: LegalDocument) => legalApi.publish(document.id),
    onSuccess: async () => {
      setPublishing(null)
      notify.success(t('legalAdmin.published'))
      await refresh()
      await queryClient.invalidateQueries({ queryKey: ['session'] })
    },
  })

  const remove = useMutation({
    mutationFn: (document: LegalDocument) => legalApi.removeDraft(document.id),
    onSuccess: async () => {
      setDeleting(null)
      notify.success(t('legalAdmin.draftDeleted'))
      await refresh()
    },
  })

  return (
    <div className="space-y-6">
      <PageHeader title={t('legalAdmin.title')} description={t('legalAdmin.description')} />

      {documents.isError && (
        <ErrorPanel
          message={describeError(documents.error)}
          traceId={traceIdOf(documents.error)}
          onRetry={() => void documents.refetch()}
        />
      )}
      {documents.isPending && <LoadingState />}

      {documents.data &&
        KINDS.map((kind) => {
          const versions = documents.data.filter((document) => document.kind === kind)
          const hasDraft = versions.some((document) => document.status === 'draft')
          const latest = versions.find((document) => document.status !== 'draft') ?? null

          return (
            <Section
              key={kind}
              title={t(`legal.${kind}.title`)}
              description={t(`legalAdmin.${kind}.description`)}
              actions={
                !hasDraft && (
                  <Button variant="outline" size="sm" onClick={() => setEditing({ mode: 'create', kind, from: latest })}>
                    <FilePlus2 />
                    {t('legalAdmin.newVersion')}
                  </Button>
                )
              }
            >
              {versions.length === 0 ? (
                <EmptyState title={t('legalAdmin.empty.title')} description={t('legalAdmin.empty.description')} />
              ) : (
                <div className="overflow-x-auto">
                  <Table>
                    <TableCaption className="sr-only">{t(`legal.${kind}.title`)}</TableCaption>
                    <TableHeader>
                      <TableRow>
                        <TableHead>{t('legalAdmin.column.version')}</TableHead>
                        <TableHead>{t('legalAdmin.column.status')}</TableHead>
                        <TableHead>{t('legalAdmin.column.published')}</TableHead>
                        <TableHead>{t('legalAdmin.column.acceptances')}</TableHead>
                        <TableHead className="text-right">{t('common.actions')}</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {versions.map((document) => (
                        <TableRow key={document.id}>
                          <TableCell>
                            <div className="grid">
                              <span className="font-medium">v{document.version}</span>
                              <span className="text-muted-foreground text-xs">
                                {document.texts.map((text) => text.language.toUpperCase()).join(', ')}
                                {document.requiresAcceptance ? ` · ${t('legalAdmin.requiresAcceptance')}` : ''}
                              </span>
                            </div>
                          </TableCell>
                          <TableCell>
                            <Badge
                              variant={
                                document.status === 'published' ? 'success' : document.status === 'draft' ? 'warning' : 'outline'
                              }
                            >
                              {t(`legalAdmin.status.${document.status}`)}
                            </Badge>
                          </TableCell>
                          <TableCell>
                            {document.publishedAtUtc ? formatDate(document.publishedAtUtc, { dateStyle: 'medium' }) : '—'}
                          </TableCell>
                          <TableCell className="tabular-nums">
                            {document.status === 'draft' ? '—' : document.acceptanceCount}
                          </TableCell>
                          <TableCell className="text-right">
                            <div className="flex justify-end gap-2">
                              {document.status === 'draft' ? (
                                <>
                                  <Button variant="ghost" size="sm" onClick={() => setEditing({ mode: 'edit', document })}>
                                    <Pencil />
                                    {t('common.edit')}
                                  </Button>
                                  <Button variant="ghost" size="sm" onClick={() => setPublishing(document)}>
                                    <Send />
                                    {t('legalAdmin.publish')}
                                  </Button>
                                  <Button variant="ghost" size="sm" aria-label={t('common.delete')} onClick={() => setDeleting(document)}>
                                    <Trash2 />
                                  </Button>
                                </>
                              ) : (
                                <Button variant="ghost" size="sm" onClick={() => setViewing(document)}>
                                  <Eye />
                                  {t('legalAdmin.view')}
                                </Button>
                              )}
                            </div>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </Section>
          )
        })}

      <Dialog open={editing !== null} onOpenChange={(open) => !open && setEditing(null)}>
        <DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-3xl">
          {editing && (
            <LegalDocumentForm
              editing={editing}
              onClose={() => setEditing(null)}
              onSaved={async () => {
                setEditing(null)
                await refresh()
              }}
            />
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={viewing !== null} onOpenChange={(open) => !open && setViewing(null)}>
        <DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-3xl">
          {viewing && <LegalDocumentView document={viewing} />}
        </DialogContent>
      </Dialog>

      <AlertDialog open={publishing !== null} onOpenChange={(open) => !open && setPublishing(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('legalAdmin.publishTitle', { version: publishing?.version ?? '' })}</AlertDialogTitle>
            <AlertDialogDescription>
              {publishing?.requiresAcceptance ? t('legalAdmin.publishDescriptionAcceptance') : t('legalAdmin.publishDescription')}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t('common.cancel')}</AlertDialogCancel>
            <AlertDialogAction
              disabled={publish.isPending}
              onClick={(event) => {
                event.preventDefault()
                if (publishing) {
                  publish.mutate(publishing)
                }
              }}
            >
              {t('legalAdmin.publish')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <AlertDialog open={deleting !== null} onOpenChange={(open) => !open && setDeleting(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('legalAdmin.deleteTitle')}</AlertDialogTitle>
            <AlertDialogDescription>{t('legalAdmin.deleteDescription')}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t('common.cancel')}</AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              disabled={remove.isPending}
              onClick={(event) => {
                event.preventDefault()
                if (deleting) {
                  remove.mutate(deleting)
                }
              }}
            >
              {t('common.delete')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}

/** A published or superseded version, read-only, in each of its languages. */
function LegalDocumentView({ document }: { document: LegalDocument }) {
  const { t } = useI18n()

  return (
    <>
      <DialogHeader>
        <DialogTitle>
          {t(`legal.${document.kind}.title`)} v{document.version}
        </DialogTitle>
        <DialogDescription>{t(`legalAdmin.status.${document.status}`)}</DialogDescription>
      </DialogHeader>
      <Tabs defaultValue={document.texts[0]?.language}>
        <TabsList variant="line">
          {document.texts.map((text) => (
            <TabsTrigger key={text.language} value={text.language}>
              {text.language.toUpperCase()}
            </TabsTrigger>
          ))}
        </TabsList>
        <Separator className="-mt-2" />
        {document.texts.map((text) => (
          <TabsContent key={text.language} value={text.language} className="pt-4">
            <h2 className="mb-2 font-semibold">{text.title}</h2>
            <ScrollArea className="h-[60vh] rounded-md border">
              <Markdown className="p-4">{text.body}</Markdown>
            </ScrollArea>
          </TabsContent>
        ))}
      </Tabs>
    </>
  )
}

/**
 * A draft: whether it must be accepted, and its title and Markdown text in each of the portal's languages (only
 * the languages given a title and a text are saved). A new version starts from the latest one.
 */
function LegalDocumentForm({
  editing,
  onClose,
  onSaved,
}: {
  editing: Editing
  onClose: () => void
  onSaved: () => Promise<void>
}) {
  const { t, languages, defaultLanguage } = useI18n()
  const source = editing.mode === 'edit' ? editing.document : editing.from
  const kind = editing.mode === 'edit' ? editing.document.kind : editing.kind

  const [requiresAcceptance, setRequiresAcceptance] = useState(source?.requiresAcceptance ?? true)
  const [texts, setTexts] = useState<Record<string, { title: string; body: string }>>(() =>
    Object.fromEntries(
      languages.map(({ code }) => {
        const text = source?.texts.find((candidate) => candidate.language === code)
        return [code, { title: text?.title ?? '', body: text?.body ?? '' }]
      }),
    ),
  )
  const [tab, setTab] = useState(defaultLanguage)
  const [preview, setPreview] = useState(false)
  const [error, setError] = useState<string>()

  const save = useMutation({
    mutationFn: (body: SaveLegalDocument) =>
      editing.mode === 'edit' ? legalApi.updateDraft(editing.document.id, body) : legalApi.createDraft(kind, body),
    meta: { handlesErrors: true },
    onSuccess: async () => {
      notify.success(t('legalAdmin.draftSaved'))
      await onSaved()
    },
    onError: (failure) => reportFormError(failure, t('legalAdmin.saveFailed')),
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()

    const filled = Object.entries(texts)
      .filter(([, text]) => text.title.trim() !== '' && text.body.trim() !== '')
      .map(([language, text]) => ({ language, title: text.title.trim(), body: text.body.trim() }))

    if (filled.length === 0) {
      setError(t('legalAdmin.form.textRequired'))
      setTab(defaultLanguage)
      return
    }

    setError(undefined)
    save.mutate({ requiresAcceptance, texts: filled })
  }

  const update = (code: string, field: 'title' | 'body', value: string) =>
    setTexts((current) => ({ ...current, [code]: { ...current[code]!, [field]: value } }))

  return (
    <form onSubmit={submit} noValidate className="grid gap-5">
      <DialogHeader>
        <DialogTitle>
          {t(`legal.${kind}.title`)} · {editing.mode === 'edit' ? `v${editing.document.version}` : t('legalAdmin.newVersion')}
        </DialogTitle>
        <DialogDescription>{t('legalAdmin.form.description')}</DialogDescription>
      </DialogHeader>

      <div className="flex items-start justify-between gap-6 rounded-lg border p-4">
        <div className="grid gap-1">
          <Label htmlFor="legal-requires-acceptance">{t('legalAdmin.form.requiresAcceptance')}</Label>
          <p className="text-muted-foreground text-sm">{t('legalAdmin.form.requiresAcceptanceHint')}</p>
        </div>
        <Switch id="legal-requires-acceptance" checked={requiresAcceptance} onCheckedChange={setRequiresAcceptance} />
      </div>

      <Tabs value={tab} onValueChange={setTab}>
        <div className="flex items-end justify-between gap-2">
          <TabsList variant="line" className="max-w-full flex-wrap justify-start">
            {languages.map(({ code, name }) => (
              <TabsTrigger key={code} value={code}>
                {name}
                {texts[code]?.title.trim() && texts[code]?.body.trim() && (
                  <span className="bg-success ml-1.5 size-1.5 rounded-full" aria-hidden />
                )}
              </TabsTrigger>
            ))}
          </TabsList>
          <Button variant="ghost" size="sm" onClick={() => setPreview((current) => !current)}>
            {preview ? <Pencil /> : <Eye />}
            {preview ? t('legalAdmin.form.write') : t('legalAdmin.form.preview')}
          </Button>
        </div>
        <Separator className="-mt-2" />
        {languages.map(({ code }) => (
          <TabsContent key={code} value={code} className="grid gap-4 pt-4">
            <FormField
              label={t('legalAdmin.form.titleField')}
              htmlFor={`legal-title-${code}`}
              error={code === defaultLanguage ? error : undefined}
            >
              <Input maxLength={200} value={texts[code]?.title ?? ''} onChange={(event) => update(code, 'title', event.target.value)} />
            </FormField>
            {preview ? (
              <ScrollArea className="h-[50vh] rounded-md border">
                <Markdown className="p-4">{texts[code]?.body || t('legalAdmin.form.emptyPreview')}</Markdown>
              </ScrollArea>
            ) : (
              <FormField label={t('legalAdmin.form.body')} htmlFor={`legal-body-${code}`} hint={t('legalAdmin.form.bodyHint')}>
                <Textarea
                  rows={16}
                  className="font-mono text-xs"
                  value={texts[code]?.body ?? ''}
                  onChange={(event) => update(code, 'body', event.target.value)}
                />
              </FormField>
            )}
          </TabsContent>
        ))}
      </Tabs>

      <DialogFooter>
        <Button variant="outline" onClick={onClose}>
          {t('common.cancel')}
        </Button>
        <Button type="submit" disabled={save.isPending}>
          {save.isPending && <Loader2 className="animate-spin" />}
          {t('legalAdmin.saveDraft')}
        </Button>
      </DialogFooter>
    </form>
  )
}
