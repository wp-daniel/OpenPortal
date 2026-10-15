import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Info, Loader2, Pencil, Plus, Trash2, TriangleAlert, Wrench } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { announcementKeys, announcementsApi } from '@/api/announcements'
import type { Announcement, AnnouncementAudience, AnnouncementSeverity, AnnouncementText } from '@/api/types'
import { FormField } from '@/components/FormField'
import { PageHeader } from '@/components/PageHeader'
import { Section } from '@/components/Section'
import { EmptyState, ErrorPanel, LoadingState } from '@/components/StatePanels'
import { FilterSelect, TableToolbar } from '@/components/TableToolbar'
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
import { Separator } from '@/components/ui/separator'
import { Switch } from '@/components/ui/switch'
import { Table, TableBody, TableCaption, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Textarea } from '@/components/ui/textarea'
import { notify } from '@/hooks/useToast'
import { formatDate } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { fromLocalInput, toLocalInput } from '@/lib/dateInput'
import { describeError, traceIdOf } from '@/lib/errors'
import { reportFormError } from '@/lib/forms'
import { matchesSearch } from '@/lib/tableFilter'

const SEVERITIES: readonly AnnouncementSeverity[] = ['info', 'warning', 'maintenance']
const AUDIENCES: readonly AnnouncementAudience[] = ['everyone', 'signed_in']
const STATUSES = ['scheduled', 'active', 'ended'] as const

const SEVERITY_ICONS = { info: Info, warning: TriangleAlert, maintenance: Wrench } as const

type DialogState = { readonly mode: 'create' } | { readonly mode: 'edit'; readonly announcement: Announcement }

/** The banners shown across the portal: news, warnings and planned maintenance, each with its own window. */
export function AdminAnnouncementsPage() {
  const { t, language } = useI18n()
  const queryClient = useQueryClient()
  const [dialog, setDialog] = useState<DialogState | null>(null)
  const [deleting, setDeleting] = useState<Announcement | null>(null)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')

  const announcements = useQuery({
    queryKey: announcementKeys.admin,
    queryFn: ({ signal }) => announcementsApi.list(signal),
  })

  const visible = useMemo(
    () =>
      (announcements.data ?? []).filter(
        (announcement) =>
          (status === '' || announcement.status === status) &&
          matchesSearch(search, ...announcement.texts.flatMap((text) => [text.title, text.body])),
      ),
    [announcements.data, search, status],
  )

  const refresh = () => queryClient.invalidateQueries({ queryKey: announcementKeys.all })

  const remove = useMutation({
    mutationFn: (announcement: Announcement) => announcementsApi.remove(announcement.id),
    onSuccess: async () => {
      setDeleting(null)
      notify.success(t('announcements.deleted'))
      await refresh()
    },
  })

  const titleOf = (announcement: Announcement) =>
    (announcement.texts.find((text) => text.language === language) ?? announcement.texts[0])?.title ?? ''

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('announcements.title')}
        description={t('announcements.description')}
        actions={
          <Button onClick={() => setDialog({ mode: 'create' })}>
            <Plus />
            {t('announcements.create')}
          </Button>
        }
      />

      <Section title={t('announcements.listTitle')}>
        {announcements.isError && (
          <ErrorPanel
            message={describeError(announcements.error)}
            traceId={traceIdOf(announcements.error)}
            onRetry={() => void announcements.refetch()}
          />
        )}
        {announcements.isPending && <LoadingState />}

        {announcements.data?.length === 0 && (
          <EmptyState title={t('announcements.empty.title')} description={t('announcements.empty.description')}>
            <Button variant="outline" onClick={() => setDialog({ mode: 'create' })}>
              <Plus />
              {t('announcements.create')}
            </Button>
          </EmptyState>
        )}

        {announcements.data && announcements.data.length > 0 && (
          <>
            <TableToolbar
              search={search}
              onSearchChange={setSearch}
              searchPlaceholder={t('announcements.search')}
              filtersActive={search !== '' || status !== ''}
              onReset={() => {
                setSearch('')
                setStatus('')
              }}
              resultCount={visible.length}
            >
              <FilterSelect
                label={t('announcements.column.status')}
                value={status}
                onChange={setStatus}
                options={STATUSES.map((value) => ({ value, label: t(`announcements.status.${value}`) }))}
              />
            </TableToolbar>
            {visible.length === 0 ? (
              <p className="text-muted-foreground py-6 text-center text-sm">{t('table.noResults')}</p>
            ) : (
              <div className="overflow-x-auto">
                <Table>
                  <TableCaption className="sr-only">{t('announcements.listTitle')}</TableCaption>
                  <TableHeader>
                    <TableRow>
                      <TableHead>{t('announcements.column.title')}</TableHead>
                      <TableHead>{t('announcements.column.audience')}</TableHead>
                      <TableHead>{t('announcements.column.window')}</TableHead>
                      <TableHead>{t('announcements.column.status')}</TableHead>
                      <TableHead className="text-right">{t('common.actions')}</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {visible.map((announcement) => {
                      const Icon = SEVERITY_ICONS[announcement.severity]

                      return (
                        <TableRow key={announcement.id}>
                          <TableCell>
                            <div className="flex items-start gap-2">
                              <Icon className="text-muted-foreground mt-0.5 size-4 shrink-0" />
                              <div className="grid">
                                <span className="font-medium">{titleOf(announcement)}</span>
                                <span className="text-muted-foreground text-xs">
                                  {t(`announcements.severity.${announcement.severity}`)} ·{' '}
                                  {announcement.texts.map((text) => text.language.toUpperCase()).join(', ')}
                                </span>
                              </div>
                            </div>
                          </TableCell>
                          <TableCell>{t(`announcements.audience.${announcement.audience}`)}</TableCell>
                          <TableCell className="text-sm whitespace-nowrap">
                            <Window announcement={announcement} />
                          </TableCell>
                          <TableCell>
                            <Badge
                              variant={
                                announcement.status === 'active'
                                  ? 'success'
                                  : announcement.status === 'scheduled'
                                    ? 'secondary'
                                    : 'outline'
                              }
                            >
                              {t(`announcements.status.${announcement.status}`)}
                            </Badge>
                          </TableCell>
                          <TableCell className="text-right">
                            <div className="flex justify-end gap-2">
                              <Button variant="ghost" size="sm" onClick={() => setDialog({ mode: 'edit', announcement })}>
                                <Pencil />
                                {t('common.edit')}
                              </Button>
                              <Button
                                variant="ghost"
                                size="sm"
                                aria-label={t('common.delete')}
                                onClick={() => setDeleting(announcement)}
                              >
                                <Trash2 />
                              </Button>
                            </div>
                          </TableCell>
                        </TableRow>
                      )
                    })}
                  </TableBody>
                </Table>
              </div>
            )}
          </>
        )}
      </Section>

      <Dialog open={dialog !== null} onOpenChange={(open) => !open && setDialog(null)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          {dialog && (
            <AnnouncementForm
              announcement={dialog.mode === 'edit' ? dialog.announcement : null}
              onClose={() => setDialog(null)}
              onSaved={async () => {
                setDialog(null)
                await refresh()
              }}
            />
          )}
        </DialogContent>
      </Dialog>

      <AlertDialog open={deleting !== null} onOpenChange={(open) => !open && setDeleting(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('announcements.deleteTitle')}</AlertDialogTitle>
            <AlertDialogDescription>{t('announcements.deleteDescription')}</AlertDialogDescription>
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

function Window({ announcement }: { announcement: Announcement }) {
  const { t } = useI18n()
  const format = (value: string) => formatDate(value, { dateStyle: 'medium', timeStyle: 'short' })

  return (
    <div className="grid">
      <span>{announcement.startsAtUtc ? format(announcement.startsAtUtc) : t('announcements.window.now')}</span>
      <span className="text-muted-foreground text-xs">
        {announcement.endsAtUtc
          ? t('announcements.window.until', { date: format(announcement.endsAtUtc) })
          : t('announcements.window.open')}
      </span>
    </div>
  )
}

/**
 * Kind, audience, window and the text in each of the portal's languages. Only the languages given a title are
 * saved; a reader whose language has none sees the default language's text.
 */
function AnnouncementForm({
  announcement,
  onClose,
  onSaved,
}: {
  announcement: Announcement | null
  onClose: () => void
  onSaved: () => Promise<void>
}) {
  const { t, languages, defaultLanguage } = useI18n()
  const [severity, setSeverity] = useState<AnnouncementSeverity>(announcement?.severity ?? 'info')
  const [audience, setAudience] = useState<AnnouncementAudience>(announcement?.audience ?? 'everyone')
  const [startsAt, setStartsAt] = useState(toLocalInput(announcement?.startsAtUtc))
  const [endsAt, setEndsAt] = useState(toLocalInput(announcement?.endsAtUtc))
  const [dismissible, setDismissible] = useState(announcement?.dismissible ?? true)
  const [texts, setTexts] = useState<Record<string, { title: string; body: string }>>(() =>
    Object.fromEntries(
      languages.map(({ code }) => {
        const text = announcement?.texts.find((candidate) => candidate.language === code)
        return [code, { title: text?.title ?? '', body: text?.body ?? '' }]
      }),
    ),
  )
  const [tab, setTab] = useState(defaultLanguage)
  const [errors, setErrors] = useState<{ texts?: string; window?: string }>({})

  const save = useMutation({
    mutationFn: (body: Parameters<typeof announcementsApi.create>[0]) =>
      announcement ? announcementsApi.update(announcement.id, body) : announcementsApi.create(body),
    meta: { handlesErrors: true },
    onSuccess: async () => {
      notify.success(t('announcements.saved'))
      await onSaved()
    },
    onError: (error) => reportFormError(error, t('announcements.saveFailed')),
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()

    const filled: AnnouncementText[] = Object.entries(texts)
      .filter(([, text]) => text.title.trim() !== '')
      .map(([language, text]) => ({ language, title: text.title.trim(), body: text.body.trim() || null }))

    const startsAtUtc = fromLocalInput(startsAt)
    const endsAtUtc = fromLocalInput(endsAt)

    const next: typeof errors = {}
    if (filled.length === 0) {
      next.texts = t('announcements.form.textRequired')
    }
    if (startsAtUtc && endsAtUtc && endsAtUtc <= startsAtUtc) {
      next.window = t('announcements.form.windowInvalid')
    }

    setErrors(next)
    if (next.texts || next.window) {
      if (next.texts) {
        setTab(defaultLanguage)
      }
      return
    }

    save.mutate({ severity, audience, startsAtUtc, endsAtUtc, dismissible, texts: filled })
  }

  return (
    <form onSubmit={submit} noValidate className="grid gap-5">
      <DialogHeader>
        <DialogTitle>{announcement ? t('announcements.form.editTitle') : t('announcements.form.createTitle')}</DialogTitle>
        <DialogDescription>{t('announcements.form.description')}</DialogDescription>
      </DialogHeader>

      <div className="grid items-start gap-5 sm:grid-cols-2">
        <fieldset className="grid gap-3">
          <legend className="mb-1 text-sm font-medium">{t('announcements.form.severity')}</legend>
          <RadioGroup
            value={severity}
            onValueChange={(value) => {
              setSeverity(value as AnnouncementSeverity)
              if (value === 'maintenance') {
                setDismissible(false)
              }
            }}
            className="gap-2"
          >
            {SEVERITIES.map((value) => (
              <div key={value} className="flex items-center gap-3">
                <RadioGroupItem value={value} id={`announcement-severity-${value}`} />
                <Label htmlFor={`announcement-severity-${value}`}>{t(`announcements.severity.${value}`)}</Label>
              </div>
            ))}
          </RadioGroup>
        </fieldset>

        <fieldset className="grid gap-3">
          <legend className="mb-1 text-sm font-medium">{t('announcements.form.audience')}</legend>
          <RadioGroup value={audience} onValueChange={(value) => setAudience(value as AnnouncementAudience)} className="gap-2">
            {AUDIENCES.map((value) => (
              <div key={value} className="flex items-start gap-3">
                <RadioGroupItem value={value} id={`announcement-audience-${value}`} className="mt-0.5" />
                <div className="grid gap-0.5">
                  <Label htmlFor={`announcement-audience-${value}`}>{t(`announcements.audience.${value}`)}</Label>
                  <p className="text-muted-foreground text-xs">{t(`announcements.audience.${value}Hint`)}</p>
                </div>
              </div>
            ))}
          </RadioGroup>
        </fieldset>

        <FormField label={t('announcements.form.startsAt')} htmlFor="announcement-starts" hint={t('announcements.form.startsAtHint')}>
          <Input type="datetime-local" value={startsAt} onChange={(event) => setStartsAt(event.target.value)} />
        </FormField>

        <FormField
          label={t('announcements.form.endsAt')}
          htmlFor="announcement-ends"
          hint={t('announcements.form.endsAtHint')}
          error={errors.window}
        >
          <Input type="datetime-local" value={endsAt} onChange={(event) => setEndsAt(event.target.value)} />
        </FormField>
      </div>

      <div className="flex items-start justify-between gap-6 rounded-lg border p-4">
        <div className="grid gap-1">
          <Label htmlFor="announcement-dismissible">{t('announcements.form.dismissible')}</Label>
          <p className="text-muted-foreground text-sm">{t('announcements.form.dismissibleHint')}</p>
        </div>
        <Switch id="announcement-dismissible" checked={dismissible} onCheckedChange={setDismissible} />
      </div>

      <div className="grid gap-2">
        <Label>{t('announcements.form.texts')}</Label>
        <Tabs value={tab} onValueChange={setTab}>
          <TabsList variant="line" className="max-w-full flex-wrap justify-start">
            {languages.map(({ code, name }) => (
              <TabsTrigger key={code} value={code}>
                {name}
                {texts[code]?.title.trim() && <span className="bg-success ml-1.5 size-1.5 rounded-full" aria-hidden />}
              </TabsTrigger>
            ))}
          </TabsList>
          <Separator className="-mt-2" />
          {languages.map(({ code }) => (
            <TabsContent key={code} value={code} className="grid gap-4 pt-4">
              <FormField
                label={t('announcements.form.titleField')}
                htmlFor={`announcement-title-${code}`}
                error={code === defaultLanguage ? errors.texts : undefined}
              >
                <Input
                  maxLength={120}
                  value={texts[code]?.title ?? ''}
                  onChange={(event) => setTexts((current) => ({ ...current, [code]: { ...current[code]!, title: event.target.value } }))}
                />
              </FormField>
              <FormField label={t('announcements.form.body')} htmlFor={`announcement-body-${code}`} hint={t('announcements.form.bodyHint')}>
                <Textarea
                  rows={4}
                  maxLength={1000}
                  value={texts[code]?.body ?? ''}
                  onChange={(event) => setTexts((current) => ({ ...current, [code]: { ...current[code]!, body: event.target.value } }))}
                />
              </FormField>
            </TabsContent>
          ))}
        </Tabs>
        <p className="text-muted-foreground text-xs">{t('announcements.form.textsHint')}</p>
      </div>

      <DialogFooter>
        <Button variant="outline" onClick={onClose}>
          {t('common.cancel')}
        </Button>
        <Button type="submit" disabled={save.isPending}>
          {save.isPending && <Loader2 className="animate-spin" />}
          {t('common.save')}
        </Button>
      </DialogFooter>
    </form>
  )
}
