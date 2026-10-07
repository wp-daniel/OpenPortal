import { useMutation, useQuery } from '@tanstack/react-query'
import { Loader2, Pencil, Plus, Trash2, UserPlus, UserRound, X } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { z } from 'zod'
import { accessApi, accessKeys, applicationsApi, groupsApi } from '@/api/access'
import type { GroupDetail, GroupSummary } from '@/api/types'
import { GroupPagesSection } from '@/components/access/GroupPagesSection'
import { UserPicker, type PickedPrincipal } from '@/components/access/PrincipalPicker'
import { ApplicationStatusBadge } from '@/components/access/shared'
import { useAccessRefresh } from '@/lib/access'
import { FormField } from '@/components/FormField'
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
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { TableToolbar } from '@/components/TableToolbar'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Separator } from '@/components/ui/separator'
import { Switch } from '@/components/ui/switch'
import { Table, TableBody, TableCaption, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useSession } from '@/hooks/useSession'
import { notify } from '@/hooks/useToast'
import { matchesSearch } from '@/lib/tableFilter'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'
import { reportFormError, zodFieldErrors } from '@/lib/forms'

type GroupTab = 'details' | 'members' | 'applications' | 'pages'

/** What the group dialog is showing: a new group, or an existing one on one of its tabs. */
type DialogState = { readonly mode: 'create' } | { readonly mode: 'edit'; readonly groupId: string; readonly tab: GroupTab }

/**
 * Groups of users. A group granted an application gives it to every member, so the usual way to manage
 * access is: put people in groups, give groups applications.
 */
export function AdminGroupsPage() {
  const { t } = useI18n()
  const refresh = useAccessRefresh()

  const [dialog, setDialog] = useState<DialogState | null>(null)
  const [deleting, setDeleting] = useState<GroupSummary | null>(null)

  const groups = useQuery({
    queryKey: accessKeys.groups,
    queryFn: ({ signal }) => groupsApi.list(signal),
  })

  const [search, setSearch] = useState('')
  const visibleGroups = useMemo(
    () => (groups.data ?? []).filter((group) => matchesSearch(search, group.name, group.description)),
    [groups.data, search],
  )

  const remove = useMutation({
    mutationFn: (group: GroupSummary) => groupsApi.remove(group.id),
    onSuccess: async (_result, group) => {
      setDeleting(null)
      notify.success(t('groups.deleted'), group.name)
      await refresh()
    },
  })

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('groups.title')}
        description={t('groups.description')}
        actions={
          <Button onClick={() => setDialog({ mode: 'create' })}>
            <Plus />
            {t('groups.create')}
          </Button>
        }
      />

      <Section title={t('groups.listTitle')}>
        {groups.isError && (
          <ErrorPanel message={describeError(groups.error)} traceId={traceIdOf(groups.error)} onRetry={() => void groups.refetch()} />
        )}

        {groups.isPending && <LoadingState />}

        {groups.data?.length === 0 && <EmptyState title={t('groups.empty.title')} description={t('groups.empty.description')} />}

        {groups.data && groups.data.length > 0 && (
          <>
          <TableToolbar
            search={search}
            onSearchChange={setSearch}
            searchPlaceholder={t('groups.searchPlaceholder')}
            filtersActive={search !== ''}
            onReset={() => setSearch('')}
            resultCount={visibleGroups.length}
          />
          {visibleGroups.length === 0 ? (
            <p className="text-muted-foreground py-6 text-center text-sm">{t('table.noResults')}</p>
          ) : (
          <div className="overflow-x-auto">
            <Table>
              <TableCaption className="sr-only">{t('groups.listTitle')}</TableCaption>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('groups.column.name')}</TableHead>
                  <TableHead>{t('groups.column.members')}</TableHead>
                  <TableHead>{t('groups.column.applications')}</TableHead>
                  <TableHead className="text-right">{t('common.actions')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {visibleGroups.map((group) => (
                  <TableRow key={group.id}>
                    <TableCell>
                      <div className="font-medium">{group.name}</div>
                      {group.description && <div className="text-muted-foreground text-xs">{group.description}</div>}
                    </TableCell>
                    <TableCell>{group.memberCount}</TableCell>
                    <TableCell>{group.applicationCount}</TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-2">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => setDialog({ mode: 'edit', groupId: group.id, tab: 'details' })}
                        >
                          <Pencil />
                          {t('common.edit')}
                        </Button>
                        <Button variant="ghost" size="sm" aria-label={t('common.delete')} onClick={() => setDeleting(group)}>
                          <Trash2 />
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
          )}
          </>
        )}
      </Section>

      <Dialog open={dialog !== null} onOpenChange={(open) => !open && setDialog(null)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          {/* Rendered inside the content, so every opening starts fresh. A new group continues on its members. */}
          {dialog?.mode === 'create' && (
            <GroupDetailsForm
              group={null}
              onClose={() => setDialog(null)}
              onSaved={(saved) => {
                void refresh()
                setDialog({ mode: 'edit', groupId: saved.id, tab: 'members' })
              }}
            />
          )}
          {dialog?.mode === 'edit' && (
            <GroupEditor
              key={dialog.groupId}
              groupId={dialog.groupId}
              initialTab={dialog.tab}
              onClose={() => setDialog(null)}
            />
          )}
        </DialogContent>
      </Dialog>

      <AlertDialog open={deleting !== null} onOpenChange={(open) => !open && setDeleting(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('groups.deleteTitle', { name: deleting?.name ?? '' })}</AlertDialogTitle>
            <AlertDialogDescription>{t('groups.deleteDescription')}</AlertDialogDescription>
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

/**
 * An existing group in the dialog, one tab per concern: its details (saved with the button), then members,
 * applications and pages (each change applies at once). Pages are shown to administrators only.
 */
function GroupEditor({ groupId, initialTab, onClose }: { groupId: string; initialTab: GroupTab; onClose: () => void }) {
  const { t } = useI18n()
  const { isAdministrator } = useSession()
  const [tab, setTab] = useState<GroupTab>(initialTab)

  const group = useQuery({
    queryKey: accessKeys.group(groupId),
    queryFn: ({ signal }) => groupsApi.get(groupId, signal),
  })

  if (group.isError) {
    return <ErrorPanel message={describeError(group.error)} onRetry={() => void group.refetch()} />
  }

  if (!group.data) {
    return (
      <>
        <DialogHeader>
          <DialogTitle>{t('groups.form.editTitle')}</DialogTitle>
          <DialogDescription className="sr-only">{t('common.loading')}</DialogDescription>
        </DialogHeader>
        <LoadingState />
      </>
    )
  }

  const count = (value: number) => (
    <Badge variant="secondary" className="ml-1.5 tabular-nums">
      {value}
    </Badge>
  )

  return (
    <>
      <DialogHeader>
        <DialogTitle>{group.data.name}</DialogTitle>
        <DialogDescription>{group.data.description ?? t('groups.sheet.description')}</DialogDescription>
      </DialogHeader>

      <Tabs value={tab} onValueChange={(value) => setTab(value as GroupTab)}>
        <TabsList variant="line" className="max-w-full flex-wrap justify-start group-data-[orientation=horizontal]/tabs:h-auto">
          <TabsTrigger value="details">{t('groups.tab.details')}</TabsTrigger>
          <TabsTrigger value="members">
            {t('groups.tab.members')}
            {count(group.data.members.length)}
          </TabsTrigger>
          <TabsTrigger value="applications">
            {t('groups.tab.applications')}
            {count(group.data.applications.length)}
          </TabsTrigger>
          {isAdministrator && (
            <TabsTrigger value="pages">
              {t('groups.tab.pages')}
              {count(group.data.pages.length)}
            </TabsTrigger>
          )}
        </TabsList>
        <Separator className="-mt-2" />

        <TabsContent value="details" className="pt-4">
          <GroupDetailsForm group={group.data} onClose={onClose} onSaved={() => undefined} embedded />
        </TabsContent>

        <TabsContent value="members" className="pt-4">
          <GroupMembers group={group.data} />
        </TabsContent>

        <TabsContent value="applications" className="pt-4">
          <GroupApplications group={group.data} />
        </TabsContent>

        {isAdministrator && (
          <TabsContent value="pages" className="pt-4">
            <GroupPagesSection group={group.data} />
          </TabsContent>
        )}
      </Tabs>

      {tab !== 'details' && (
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            {t('common.close')}
          </Button>
        </DialogFooter>
      )}
    </>
  )
}

/**
 * Name and description. On its own for a new group (with the dialog header), or `embedded` as the first tab
 * of an existing one.
 */
function GroupDetailsForm({
  group,
  onClose,
  onSaved,
  embedded = false,
}: {
  group: GroupSummary | GroupDetail | null
  onClose: () => void
  onSaved: (saved: GroupDetail) => void
  embedded?: boolean
}) {
  const { t } = useI18n()
  const refresh = useAccessRefresh()
  const [name, setName] = useState(group?.name ?? '')
  const [description, setDescription] = useState(group?.description ?? '')
  const [errors, setErrors] = useState<Partial<Record<'name' | 'description', string>>>({})

  const schema = useMemo(
    () =>
      z.object({
        name: z.string().trim().min(1, t('groups.form.nameRequired')).max(80, t('groups.form.nameMax', { max: 80 })),
        description: z.string().trim().max(500, t('groups.form.descriptionMax', { max: 500 })),
      }),
    [t],
  )

  const save = useMutation({
    mutationFn: () => {
      const body = { name: name.trim(), description: description.trim() === '' ? null : description.trim() }
      return group ? groupsApi.update(group.id, body) : groupsApi.create(body)
    },
    meta: { handlesErrors: true },
    onSuccess: async (saved) => {
      notify.success(group ? t('groups.saved') : t('groups.created'), saved.name)
      onSaved(saved)
      await refresh()
    },
    onError: (failure) => reportFormError(failure, t('groups.saveFailed')),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    const parsed = schema.safeParse({ name, description })

    if (!parsed.success) {
      setErrors(zodFieldErrors(parsed.error))
      return
    }

    setErrors({})
    save.mutate()
  }

  return (
    <>
      {!embedded && (
        <DialogHeader>
          <DialogTitle>{t('groups.form.createTitle')}</DialogTitle>
          <DialogDescription>{t('groups.form.description')}</DialogDescription>
        </DialogHeader>
      )}

      <form id="group-form" onSubmit={submit} noValidate className="grid items-start gap-5">
        <FormField label={t('groups.form.name')} htmlFor="group-name" error={errors.name}>
          <Input value={name} onChange={(event) => setName(event.target.value)} />
        </FormField>
        <FormField label={t('groups.form.descriptionLabel')} htmlFor="group-description" error={errors.description}>
          <Input value={description} onChange={(event) => setDescription(event.target.value)} />
        </FormField>
      </form>

      <DialogFooter className={embedded ? 'mt-6' : undefined}>
        <Button variant="outline" onClick={onClose}>
          {t('common.cancel')}
        </Button>
        <Button type="submit" form="group-form" disabled={save.isPending}>
          {save.isPending && <Loader2 className="animate-spin" />}
          {t('common.save')}
        </Button>
      </DialogFooter>
    </>
  )
}

/** Members of the group: add with the user picker, remove from the list. Each change applies at once. */
function GroupMembers({ group }: { group: GroupDetail }) {
  const { t } = useI18n()
  const refresh = useAccessRefresh()
  const [picked, setPicked] = useState<PickedPrincipal | null>(null)

  const addMember = useMutation({
    mutationFn: (userId: string) => groupsApi.addMember(group.id, userId),
    onSuccess: async (updated: GroupDetail) => {
      setPicked(null)
      notify.success(t('groups.memberAdded'), updated.name)
      await refresh()
    },
  })

  const removeMember = useMutation({
    mutationFn: (userId: string) => groupsApi.removeMember(group.id, userId),
    onSuccess: () => refresh(),
  })

  return (
    <section className="grid gap-3">
      <div className="flex gap-2">
        <div className="min-w-0 flex-1">
          <UserPicker value={picked} onChange={setPicked} exclude={group.members.map((member) => member.id)} />
        </div>
        <Button disabled={!picked || addMember.isPending} onClick={() => picked && addMember.mutate(picked.id)}>
          <UserPlus />
          {t('groups.sheet.add')}
        </Button>
      </div>

      {group.members.length === 0 ? (
        <p className="text-muted-foreground text-sm">{t('groups.sheet.noMembers')}</p>
      ) : (
        <ul className="max-h-[50vh] divide-y overflow-y-auto rounded-md border">
          {group.members.map((member) => (
            <li key={member.id} className="flex items-center gap-3 px-3 py-2">
              <UserRound className="text-muted-foreground size-4 shrink-0" />
              <div className="grid min-w-0 flex-1">
                <span className="truncate text-sm">{member.isKnown ? member.displayName : t('access.unknownUser')}</span>
                <span className="text-muted-foreground truncate font-mono text-xs">{member.email}</span>
              </div>
              <Button
                variant="ghost"
                size="sm"
                aria-label={t('groups.sheet.remove', { name: member.displayName })}
                disabled={removeMember.isPending}
                onClick={() => removeMember.mutate(member.id)}
              >
                <X />
              </Button>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

/** One switch per application: on gives it to every member of the group. */
function GroupApplications({ group }: { group: GroupDetail }) {
  const { t } = useI18n()
  const refresh = useAccessRefresh()

  const applications = useQuery({
    queryKey: accessKeys.applications,
    queryFn: ({ signal }) => applicationsApi.list(signal),
  })

  const toggleApplication = useMutation({
    mutationFn: ({ applicationId, granted }: { applicationId: string; granted: boolean }) =>
      granted ? accessApi.grantGroup(applicationId, group.id) : accessApi.revokeGroup(applicationId, group.id),
    onSuccess: () => refresh(),
  })

  const grantedIds = new Set(group.applications.map((application) => application.id))

  return (
    <section className="grid gap-3">
      <p className="text-muted-foreground text-xs">{t('groups.sheet.applicationsHint')}</p>

      {applications.isError && (
        <ErrorPanel message={describeError(applications.error)} onRetry={() => void applications.refetch()} />
      )}
      {applications.isPending && <LoadingState />}
      {applications.data?.length === 0 && <p className="text-muted-foreground text-sm">{t('applications.empty.title')}</p>}

      {applications.data && applications.data.length > 0 && (
        <ul className="max-h-[50vh] divide-y overflow-y-auto rounded-md border">
          {applications.data.map((application) => {
            const switchId = `group-app-${application.id}`

            return (
              <li key={application.id} className="flex items-center gap-3 px-3 py-2">
                <div className="grid min-w-0 flex-1 gap-0.5">
                  <Label htmlFor={switchId} className="truncate">
                    {application.displayName}
                  </Label>
                  <span className="text-muted-foreground font-mono text-xs">{application.clientId}</span>
                </div>
                {application.status !== 'active' && <ApplicationStatusBadge status={application.status} />}
                <Switch
                  id={switchId}
                  checked={grantedIds.has(application.id)}
                  disabled={toggleApplication.isPending}
                  onCheckedChange={(granted) => toggleApplication.mutate({ applicationId: application.id, granted })}
                />
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}
