import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Building2, Loader2, Pencil, Plus, Trash2, UserPlus, UserRound, X } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { z } from 'zod'
import { organizationKeys, organizationsApi } from '@/api/organizations'
import type { OrganizationDetail, OrganizationRole, OrganizationSummary } from '@/api/types'
import { UserPicker, type PickedPrincipal } from '@/components/access/PrincipalPicker'
import { MemberApplicationRoles, OrganizationDomains } from '@/components/organizations/OrganizationTenancy'
import { FormField } from '@/components/FormField'
import { PageHeader } from '@/components/PageHeader'
import { Section } from '@/components/Section'
import { EmptyState, ErrorPanel, LoadingState } from '@/components/StatePanels'
import { ListPager } from '@/components/ListPager'
import { TableToolbar } from '@/components/TableToolbar'
import { useDebounced } from '@/hooks/useDebounced'
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
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Separator } from '@/components/ui/separator'
import { Switch } from '@/components/ui/switch'
import { Table, TableBody, TableCaption, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { notify } from '@/hooks/useToast'
import { formatDate } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'
import { reportFormError, serverFieldErrors, zodFieldErrors } from '@/lib/forms'

const ROLES: readonly OrganizationRole[] = ['member', 'admin']
const PAGE_SIZE = 25

type EditorTab = 'details' | 'members' | 'domains'

type DialogState = { readonly mode: 'create' } | { readonly mode: 'edit'; readonly id: string; readonly tab: EditorTab }

/** Refreshes every organization query (list, details, the signed-in user's own memberships). */
function useOrganizationRefresh() {
  const queryClient = useQueryClient()

  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: organizationKeys.all }),
      queryClient.invalidateQueries({ queryKey: organizationKeys.mine }),
    ])
}

/**
 * Organizations (tenants). An application that works per organization is told which one the user signs in for,
 * and keeps each organization's data apart; members choose among theirs when signing in.
 */
export function AdminOrganizationsPage() {
  const { t } = useI18n()
  const refresh = useOrganizationRefresh()
  const [dialog, setDialog] = useState<DialogState | null>(null)
  const [deleting, setDeleting] = useState<OrganizationSummary | null>(null)
  const [search, setSearch] = useState(() => new URLSearchParams(window.location.search).get('search') ?? '')
  const [page, setPage] = useState(1)
  const term = useDebounced(search.trim())

  // Searched and paged on the server: a multi-tenant portal can hold thousands of organizations.
  const organizations = useQuery({
    queryKey: [...organizationKeys.all, 'page', term, page],
    queryFn: ({ signal }) => organizationsApi.slice({ search: term, skip: (page - 1) * PAGE_SIZE, take: PAGE_SIZE }, signal),
    placeholderData: (previous) => previous,
  })

  const visible = organizations.data?.items ?? []
  const nothingAtAll = organizations.data !== undefined && organizations.data.totalCount === 0 && term === ''

  function changeSearch(value: string) {
    setSearch(value)
    setPage(1)
  }

  const remove = useMutation({
    mutationFn: (organization: OrganizationSummary) => organizationsApi.remove(organization.id),
    onSuccess: async (_result, organization) => {
      setDeleting(null)
      notify.success(t('organizations.deleted'), organization.name)
      await refresh()
    },
  })

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('organizations.title')}
        description={t('organizations.description')}
        actions={
          <Button onClick={() => setDialog({ mode: 'create' })}>
            <Plus />
            {t('organizations.create')}
          </Button>
        }
      />

      <Section title={t('organizations.listTitle')}>
        {organizations.isError && (
          <ErrorPanel
            message={describeError(organizations.error)}
            traceId={traceIdOf(organizations.error)}
            onRetry={() => void organizations.refetch()}
          />
        )}
        {organizations.isPending && <LoadingState />}

        {nothingAtAll && (
          <EmptyState title={t('organizations.empty.title')} description={t('organizations.empty.description')}>
            <Button variant="outline" onClick={() => setDialog({ mode: 'create' })}>
              <Plus />
              {t('organizations.create')}
            </Button>
          </EmptyState>
        )}

        {organizations.data && !nothingAtAll && (
          <>
            <TableToolbar
              search={search}
              onSearchChange={changeSearch}
              searchPlaceholder={t('organizations.search')}
              filtersActive={search !== ''}
              onReset={() => changeSearch('')}
              resultCount={organizations.data.totalCount}
            />
            {visible.length === 0 ? (
              <p className="text-muted-foreground py-6 text-center text-sm">{t('table.noResults')}</p>
            ) : (
              <div className="overflow-x-auto">
                <Table>
                  <TableCaption className="sr-only">{t('organizations.listTitle')}</TableCaption>
                  <TableHeader>
                    <TableRow>
                      <TableHead>{t('organizations.column.name')}</TableHead>
                      <TableHead>{t('organizations.column.slug')}</TableHead>
                      <TableHead>{t('organizations.column.members')}</TableHead>
                      <TableHead>{t('organizations.column.created')}</TableHead>
                      <TableHead className="text-right">{t('common.actions')}</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {visible.map((organization) => (
                      <TableRow key={organization.id}>
                        <TableCell>
                          <div className="flex items-center gap-2">
                            <Building2 className="text-muted-foreground size-4 shrink-0" />
                            <div className="grid">
                              <span className="font-medium">{organization.name}</span>
                              {organization.description && (
                                <span className="text-muted-foreground text-xs">{organization.description}</span>
                              )}
                            </div>
                          </div>
                        </TableCell>
                        <TableCell>
                          <code className="text-xs">{organization.slug}</code>
                        </TableCell>
                        <TableCell>
                          <Button
                            variant="link"
                            size="sm"
                            className="h-auto px-0"
                            onClick={() => setDialog({ mode: 'edit', id: organization.id, tab: 'members' })}
                          >
                            {t('organizations.memberCount', { count: organization.memberCount })}
                          </Button>
                        </TableCell>
                        <TableCell>{formatDate(organization.createdAtUtc, { dateStyle: 'medium' })}</TableCell>
                        <TableCell className="text-right">
                          <div className="flex justify-end gap-2">
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => setDialog({ mode: 'edit', id: organization.id, tab: 'details' })}
                            >
                              <Pencil />
                              {t('common.edit')}
                            </Button>
                            <Button variant="ghost" size="sm" aria-label={t('common.delete')} onClick={() => setDeleting(organization)}>
                              <Trash2 />
                            </Button>
                          </div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
                <ListPager
                  page={page}
                  pageSize={PAGE_SIZE}
                  totalCount={organizations.data.totalCount}
                  onPageChange={setPage}
                  label={t('organizations.title')}
                />
              </div>
            )}
          </>
        )}
      </Section>

      <Dialog open={dialog !== null} onOpenChange={(open) => !open && setDialog(null)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          {dialog?.mode === 'create' && (
            <OrganizationDetailsForm
              organization={null}
              onClose={() => setDialog(null)}
              onSaved={(saved) => setDialog({ mode: 'edit', id: saved.id, tab: 'members' })}
            />
          )}
          {dialog?.mode === 'edit' && (
            <OrganizationEditor key={dialog.id} id={dialog.id} initialTab={dialog.tab} onClose={() => setDialog(null)} />
          )}
        </DialogContent>
      </Dialog>

      <AlertDialog open={deleting !== null} onOpenChange={(open) => !open && setDeleting(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('organizations.deleteTitle', { name: deleting?.name ?? '' })}</AlertDialogTitle>
            <AlertDialogDescription>{t('organizations.deleteDescription')}</AlertDialogDescription>
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

function OrganizationEditor({
  id,
  initialTab,
  onClose,
}: {
  id: string
  initialTab: EditorTab
  onClose: () => void
}) {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const refresh = useOrganizationRefresh()
  const [tab, setTab] = useState<EditorTab>(initialTab)

  const applied = async (saved: OrganizationDetail) => {
    queryClient.setQueryData(organizationKeys.detail(id), saved)
    await refresh()
  }

  const organization = useQuery({
    queryKey: organizationKeys.detail(id),
    queryFn: ({ signal }) => organizationsApi.get(id, signal),
  })

  if (organization.isError) {
    return <ErrorPanel message={describeError(organization.error)} onRetry={() => void organization.refetch()} />
  }

  if (!organization.data) {
    return (
      <>
        <DialogHeader>
          <DialogTitle>{t('organizations.form.editTitle')}</DialogTitle>
          <DialogDescription className="sr-only">{t('common.loading')}</DialogDescription>
        </DialogHeader>
        <LoadingState />
      </>
    )
  }

  return (
    <>
      <DialogHeader>
        <DialogTitle className="flex items-center gap-2">
          {organization.data.name}
          <code className="text-muted-foreground text-xs font-normal">{organization.data.slug}</code>
        </DialogTitle>
        <DialogDescription>{organization.data.description ?? t('organizations.form.editDescription')}</DialogDescription>
      </DialogHeader>

      <Tabs value={tab} onValueChange={(value) => setTab(value as EditorTab)}>
        <TabsList variant="line">
          <TabsTrigger value="details">{t('organizations.tab.details')}</TabsTrigger>
          <TabsTrigger value="members">
            {t('organizations.tab.members')}
            <Badge variant="secondary" className="ml-1.5 tabular-nums">
              {organization.data.members.length}
            </Badge>
          </TabsTrigger>
          <TabsTrigger value="domains">{t('organizations.tab.domains')}</TabsTrigger>
        </TabsList>
        <Separator className="-mt-2" />

        <TabsContent value="details" className="pt-4">
          <OrganizationDetailsForm organization={organization.data} onClose={onClose} onSaved={() => undefined} embedded />
        </TabsContent>

        <TabsContent value="members" className="pt-4">
          <OrganizationMembers organization={organization.data} onChanged={(saved) => void applied(saved)} />
        </TabsContent>

        <TabsContent value="domains" className="pt-4">
          <OrganizationDomains
            organization={organization.data}
            tenancy={organizationsApi}
            canVouch
            onChanged={(saved) => void applied(saved)}
          />
        </TabsContent>
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

/** Name, short name (only when creating: it never changes) and description. */
function OrganizationDetailsForm({
  organization,
  onClose,
  onSaved,
  embedded = false,
}: {
  organization: OrganizationDetail | null
  onClose: () => void
  onSaved: (saved: OrganizationDetail) => void
  embedded?: boolean
}) {
  const { t } = useI18n()
  const refresh = useOrganizationRefresh()
  const [name, setName] = useState(organization?.name ?? '')
  const [slug, setSlug] = useState(organization?.slug ?? '')
  const [slugTouched, setSlugTouched] = useState(false)
  const [description, setDescription] = useState(organization?.description ?? '')
  const [requireMultiFactor, setRequireMultiFactor] = useState(organization?.requireMultiFactor ?? false)
  const [errors, setErrors] = useState<Partial<Record<'name' | 'slug' | 'description', string>>>({})

  const schema = useMemo(
    () =>
      z.object({
        name: z.string().trim().min(1, t('organizations.form.nameRequired')).max(120, t('organizations.form.nameMax', { max: 120 })),
        slug: z
          .string()
          .trim()
          .min(1, t('organizations.form.slugRequired'))
          .max(64, t('organizations.form.slugInvalid'))
          .regex(/^[a-z0-9][a-z0-9-]*$/, t('organizations.form.slugInvalid')),
        description: z.string().trim().max(500, t('organizations.form.descriptionMax', { max: 500 })),
      }),
    [t],
  )

  const save = useMutation({
    mutationFn: () => {
      const text = description.trim() === '' ? null : description.trim()

      return organization
        ? organizationsApi.update(organization.id, { name: name.trim(), description: text, requireMultiFactor })
        : organizationsApi.create({ name: name.trim(), slug: slug.trim(), description: text })
    },
    meta: { handlesErrors: true },
    onSuccess: async (saved) => {
      notify.success(organization ? t('organizations.saved') : t('organizations.created'), saved.name)
      onSaved(saved)
      await refresh()
    },
    onError: (failure) => reportFormError(failure, t('organizations.saveFailed')),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    const parsed = schema.safeParse({ name, slug, description })

    if (!parsed.success) {
      setErrors(zodFieldErrors(parsed.error))
      return
    }

    setErrors({})
    save.mutate()
  }

  /** Until the short name is typed by hand, it follows the name. */
  function changeName(value: string) {
    setName(value)

    if (!organization && !slugTouched) {
      setSlug(
        value
          .normalize('NFKD')
          .replace(/[̀-ͯ]/g, '')
          .toLowerCase()
          .replace(/[^a-z0-9]+/g, '-')
          .replace(/^-+|-+$/g, '')
          .slice(0, 64),
      )
    }
  }

  const server = serverFieldErrors(save.error)

  return (
    <>
      {!embedded && (
        <DialogHeader>
          <DialogTitle>{t('organizations.form.createTitle')}</DialogTitle>
          <DialogDescription>{t('organizations.form.createDescription')}</DialogDescription>
        </DialogHeader>
      )}

      <form id="organization-form" onSubmit={submit} noValidate className="grid items-start gap-5">
        <div className="grid items-start gap-5 sm:grid-cols-2">
          <FormField label={t('organizations.form.name')} htmlFor="organization-name" error={errors.name ?? server.name?.[0]}>
            <Input value={name} onChange={(event) => changeName(event.target.value)} />
          </FormField>
          <FormField
            label={t('organizations.form.slug')}
            htmlFor="organization-slug"
            hint={organization ? t('organizations.form.slugLocked') : t('organizations.form.slugHint')}
            error={errors.slug ?? server.slug?.[0]}
          >
            <Input
              value={slug}
              disabled={organization !== null}
              className="font-mono"
              autoComplete="off"
              onChange={(event) => {
                setSlugTouched(true)
                setSlug(event.target.value.toLowerCase())
              }}
            />
          </FormField>
        </div>
        <FormField label={t('organizations.form.description')} htmlFor="organization-description" error={errors.description}>
          <Input value={description} onChange={(event) => setDescription(event.target.value)} />
        </FormField>

        {organization && (
          <div className="flex items-start justify-between gap-6 rounded-lg border p-4">
            <div className="grid gap-1">
              <Label htmlFor="organization-mfa">{t('organizations.form.requireMfa')}</Label>
              <p className="text-muted-foreground text-sm">{t('organizations.form.requireMfaHint')}</p>
            </div>
            <Switch id="organization-mfa" checked={requireMultiFactor} onCheckedChange={setRequireMultiFactor} />
          </div>
        )}
      </form>

      <DialogFooter className={embedded ? 'mt-6' : undefined}>
        <Button variant="outline" onClick={onClose}>
          {t('common.cancel')}
        </Button>
        <Button type="submit" form="organization-form" disabled={save.isPending}>
          {save.isPending && <Loader2 className="animate-spin" />}
          {t('common.save')}
        </Button>
      </DialogFooter>
    </>
  )
}

/** Members with their role: add with the user picker, change the role, remove. Each change applies at once. */
function OrganizationMembers({
  organization,
  onChanged,
}: {
  organization: OrganizationDetail
  onChanged: (organization: OrganizationDetail) => void
}) {
  const { t } = useI18n()
  const refresh = useOrganizationRefresh()
  const [picked, setPicked] = useState<PickedPrincipal | null>(null)
  const [role, setRole] = useState<OrganizationRole>('member')

  const setMember = useMutation({
    mutationFn: ({ userId, role: next }: { userId: string; role: OrganizationRole }) =>
      organizationsApi.setMember(organization.id, userId, next),
    onSuccess: async () => {
      setPicked(null)
      await refresh()
    },
  })

  const removeMember = useMutation({
    mutationFn: (userId: string) => organizationsApi.removeMember(organization.id, userId),
    onSuccess: () => refresh(),
  })

  return (
    <section className="grid gap-3">
      <p className="text-muted-foreground text-xs">{t('organizations.membersHint')}</p>

      <div className="flex flex-wrap gap-2">
        <div className="min-w-0 flex-1">
          <UserPicker value={picked} onChange={setPicked} exclude={organization.members.map((member) => member.id)} />
        </div>
        <RolePicker value={role} onChange={setRole} />
        <Button
          disabled={!picked || setMember.isPending}
          onClick={() => picked && setMember.mutate({ userId: picked.id, role })}
        >
          <UserPlus />
          {t('organizations.add')}
        </Button>
      </div>

      {organization.members.length === 0 ? (
        <p className="text-muted-foreground text-sm">{t('organizations.noMembers')}</p>
      ) : (
        <ul className="max-h-[50vh] divide-y overflow-y-auto rounded-md border">
          {organization.members.map((member) => (
            <li key={member.id} className="flex items-center gap-3 px-3 py-2">
              <UserRound className="text-muted-foreground size-4 shrink-0" />
              <div className="grid min-w-0 flex-1">
                <span className="truncate text-sm">{member.isKnown ? member.displayName : t('access.unknownUser')}</span>
                <span className="text-muted-foreground truncate font-mono text-xs">{member.email}</span>
              </div>
              <MemberApplicationRoles organization={organization} member={member} tenancy={organizationsApi} onChanged={onChanged} />
              <RolePicker
                value={member.role}
                disabled={setMember.isPending}
                onChange={(next) => setMember.mutate({ userId: member.id, role: next })}
              />
              <Button
                variant="ghost"
                size="sm"
                aria-label={t('organizations.remove', { name: member.displayName })}
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

function RolePicker({
  value,
  onChange,
  disabled,
}: {
  value: OrganizationRole
  onChange: (role: OrganizationRole) => void
  disabled?: boolean
}) {
  const { t } = useI18n()

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="outline" size="sm" disabled={disabled} aria-label={t('organizations.role')}>
          {t(`organizations.roleName.${value}`)}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuRadioGroup value={value} onValueChange={(next) => onChange(next as OrganizationRole)}>
          {ROLES.map((role) => (
            <DropdownMenuRadioItem key={role} value={role}>
              {t(`organizations.roleName.${role}`)}
            </DropdownMenuRadioItem>
          ))}
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
