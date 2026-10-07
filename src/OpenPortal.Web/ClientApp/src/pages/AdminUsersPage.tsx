import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { KeyRound, Loader2, Pencil, Plus, ShieldCheck, Trash2 } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { z } from 'zod'
import type { PasswordPolicy, UserSummary } from '@/api/types'
import { userAdminApi } from '@/api/users'
import { UserAccessSheet } from '@/components/access/UserAccessSheet'
import { AvatarUpload } from '@/components/AvatarUpload'
import { FormField } from '@/components/FormField'
import { PageHeader } from '@/components/PageHeader'
import { Section } from '@/components/Section'
import { ErrorPanel, LoadingState } from '@/components/StatePanels'
import { FilterSelect, TableToolbar } from '@/components/TableToolbar'
import { UserAvatar } from '@/components/UserAvatar'
import { UserDetailsFields } from '@/components/UserDetailsFields'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
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
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Separator } from '@/components/ui/separator'
import { Switch } from '@/components/ui/switch'
import {
  Table,
  TableBody,
  TableCaption,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useDebounced } from '@/hooks/useDebounced'
import { useSession } from '@/hooks/useSession'
import { notify } from '@/hooks/useToast'
import { useI18n } from '@/i18n/useI18n'
import { useAccessRefresh } from '@/lib/access'
import { describeError, traceIdOf } from '@/lib/errors'
import { reportFormError, serverFieldErrors, zodFieldErrors } from '@/lib/forms'
import { describePasswordPolicy, newPasswordSchema } from '@/lib/passwordPolicy'
import {
  detailsToForm,
  EMPTY_DETAILS,
  formToDetails,
  USER_DETAILS_GROUP_OF,
  userDetailsShape,
  type UserDetailsField,
  type UserDetailsForm,
} from '@/lib/userDetails'

const STATUSES = ['active', 'locked', 'unconfirmed'] as const

/**
 * Account management, for administrators and holders of the users page. Whether an account is an
 * administrator is a single flag that only an administrator can set (the server enforces it).
 */
export function AdminUsersPage() {
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [kind, setKind] = useState('')
  const [status, setStatus] = useState('')
  const [accessUser, setAccessUser] = useState<UserSummary | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<UserSummary | null>(null)
  const [deleting, setDeleting] = useState<UserSummary | null>(null)
  const queryClient = useQueryClient()
  const refreshAccess = useAccessRefresh()
  // Only an administrator may grant the administrator role or change an administrator (the server enforces it).
  const { session, isAdministrator } = useSession()
  const { t } = useI18n()
  const appliedSearch = useDebounced(search.trim())

  const users = useQuery({
    queryKey: ['admin-users', page, appliedSearch, kind, status],
    queryFn: ({ signal }) =>
      userAdminApi.list(
        { page, pageSize: 20, search: appliedSearch, administrator: kind === '' ? undefined : kind === 'administrators', status },
        signal,
      ),
    // Keeps the table on screen while a filter change loads, instead of flashing a skeleton per keystroke.
    placeholderData: (previous) => previous,
  })

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['admin-users'] })

  // Failures are toasted by the global MutationCache handler, so a failed delete is never silent.
  const remove = useMutation({
    mutationFn: (user: UserSummary) => userAdminApi.remove(user.id),
    onSuccess: async (_result, user) => {
      setDeleting(null)
      notify.success(t('users.deleted'), user.email)
      await Promise.all([refresh(), refreshAccess()])
    },
  })

  const filtersActive = search !== '' || kind !== '' || status !== ''

  function changeFilter(apply: () => void) {
    apply()
    setPage(1)
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('users.title')}
        description={t('users.description')}
        actions={
          <Button
            onClick={() => {
              setEditing(null)
              setFormOpen(true)
            }}
          >
            <Plus />
            {t('users.create.open')}
          </Button>
        }
      />

      <Section title={t('users.accounts')}>
        <TableToolbar
          search={search}
          onSearchChange={(value) => changeFilter(() => setSearch(value))}
          searchPlaceholder={t('users.searchPlaceholder')}
          filtersActive={filtersActive}
          onReset={() =>
            changeFilter(() => {
              setSearch('')
              setKind('')
              setStatus('')
            })
          }
          resultCount={users.data?.totalCount}
        >
          <FilterSelect
            label={t('users.kind.label')}
            value={kind}
            onChange={(value) => changeFilter(() => setKind(value))}
            options={[
              { value: 'administrators', label: t('users.kind.administrators') },
              { value: 'others', label: t('users.kind.others') },
            ]}
          />
          <FilterSelect
            label={t('common.status')}
            value={status}
            onChange={(value) => changeFilter(() => setStatus(value))}
            options={STATUSES.map((entry) => ({ value: entry, label: t(`users.status.${entry}`) }))}
          />
        </TableToolbar>

        {users.isError && (
          <ErrorPanel
            message={describeError(users.error)}
            traceId={traceIdOf(users.error)}
            onRetry={() => void users.refetch()}
          />
        )}

        {users.isPending && <LoadingState />}

        {users.data && users.data.items.length === 0 && (
          <p className="text-muted-foreground py-6 text-center text-sm">{t('users.noMatch')}</p>
        )}

        {users.data && users.data.items.length > 0 && (
          <>
            <Table>
              <TableCaption className="sr-only">
                {t('users.caption', { page: users.data.page, total: users.data.totalPages })}
              </TableCaption>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('common.displayName')}</TableHead>
                  <TableHead>{t('common.phone')}</TableHead>
                  <TableHead>{t('common.company')}</TableHead>
                  <TableHead>{t('users.administrator')}</TableHead>
                  <TableHead>{t('common.status')}</TableHead>
                  <TableHead className="text-right">{t('common.actions')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {users.data.items.map((user) => {
                  const locked = user.isAdministrator && !isAdministrator
                  const self = user.id === session.user?.id

                  return (
                    <TableRow key={user.id}>
                      <TableCell>
                        <div className="flex items-center gap-3">
                          <UserAvatar user={user} />
                          <div className="min-w-0">
                            <div className="font-medium">{user.displayName}</div>
                            <div className="text-muted-foreground font-mono text-xs">{user.email}</div>
                          </div>
                        </div>
                      </TableCell>
                      <TableCell>{user.phoneNumber ?? <span className="text-muted-foreground">—</span>}</TableCell>
                      <TableCell>
                        {user.company || user.jobTitle ? (
                          <>
                            <div>{user.company}</div>
                            {user.jobTitle && <div className="text-muted-foreground text-xs">{user.jobTitle}</div>}
                          </>
                        ) : (
                          <span className="text-muted-foreground">—</span>
                        )}
                      </TableCell>
                      <TableCell>
                        {user.isAdministrator ? (
                          <Badge variant="secondary">
                            <ShieldCheck />
                            {t('users.administrator')}
                          </Badge>
                        ) : (
                          <span className="text-muted-foreground">—</span>
                        )}
                      </TableCell>
                      <TableCell>
                        {user.isLockedOut ? (
                          <Badge variant="warning">{t('users.lockedOut')}</Badge>
                        ) : user.emailConfirmed ? (
                          <Badge variant="success">{t('users.active')}</Badge>
                        ) : (
                          <Badge variant="warning">{t('users.unconfirmed')}</Badge>
                        )}
                      </TableCell>
                      <TableCell className="text-right">
                        <div className="flex justify-end gap-2">
                          <Button
                            variant="ghost"
                            size="sm"
                            disabled={locked}
                            title={locked ? t('users.administratorOnly') : undefined}
                            onClick={() => {
                              setEditing(user)
                              setFormOpen(true)
                            }}
                          >
                            <Pencil />
                            {t('common.edit')}
                          </Button>
                          <Button variant="ghost" size="sm" onClick={() => setAccessUser(user)}>
                            <KeyRound />
                            {t('users.access')}
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            aria-label={t('common.delete')}
                            disabled={locked || self}
                            title={self ? t('users.cannotDeleteSelf') : locked ? t('users.administratorOnly') : undefined}
                            onClick={() => setDeleting(user)}
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

            <nav aria-label={t('users.pagination')} className="mt-4 flex items-center justify-between gap-2 text-sm">
              <Button variant="outline" size="sm" disabled={!users.data.hasPrevious} onClick={() => setPage(page - 1)}>
                {t('common.previous')}
              </Button>
              <span className="text-muted-foreground text-center">
                {t('users.pageOf', {
                  page: users.data.page,
                  total: users.data.totalPages,
                  count: users.data.totalCount,
                })}
              </span>
              <Button variant="outline" size="sm" disabled={!users.data.hasNext} onClick={() => setPage(page + 1)}>
                {t('common.next')}
              </Button>
            </nav>
          </>
        )}
      </Section>

      <UserFormDialog
        open={formOpen}
        user={editing}
        policy={session.passwordPolicy}
        onClose={() => setFormOpen(false)}
        onSaved={() => void refresh()}
      />

      <UserAccessSheet user={accessUser} onClose={() => setAccessUser(null)} />

      <AlertDialog open={deleting !== null} onOpenChange={(open) => !open && setDeleting(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('users.deleteTitle', { name: deleting?.displayName ?? '' })}</AlertDialogTitle>
            <AlertDialogDescription>{t('users.deleteDescription', { email: deleting?.email ?? '' })}</AlertDialogDescription>
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

function UserFormDialog({
  open,
  user,
  policy,
  onClose,
  onSaved,
}: {
  open: boolean
  user: UserSummary | null
  policy: PasswordPolicy
  onClose: () => void
  onSaved: () => void
}) {
  // The content unmounts while closed, so the form starts from fresh state on every opening.
  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
        <UserForm user={user} policy={policy} onClose={onClose} onSaved={onSaved} />
      </DialogContent>
    </Dialog>
  )
}

type AccountField = 'email' | 'password'

const USER_FORM_TABS = ['account', 'profile', 'address'] as const

type UserFormTab = (typeof USER_FORM_TABS)[number]

/** The dialog tab a field is on. Server field names arrive camelCased, like the form's own. */
function tabOfField(field: string): UserFormTab {
  const group = USER_DETAILS_GROUP_OF[field as UserDetailsField]

  if (group === undefined) {
    return 'account'
  }

  return group === 'address' ? 'address' : 'profile'
}

function UserForm({
  user,
  policy,
  onClose,
  onSaved,
}: {
  user: UserSummary | null
  policy: PasswordPolicy
  onClose: () => void
  onSaved: () => void
}) {
  const { t } = useI18n()
  const editing = user !== null
  const { isAdministrator, user: me } = useSession()
  const self = user !== null && user.id === me?.id
  const [email, setEmail] = useState(user?.email ?? '')
  const [password, setPassword] = useState('')
  const [details, setDetails] = useState<UserDetailsForm>(user ? detailsToForm(user) : EMPTY_DETAILS)
  const [administrator, setAdministrator] = useState(user?.isAdministrator ?? false)
  const [errors, setErrors] = useState<Partial<Record<AccountField | UserDetailsField, string>>>({})
  const [tab, setTab] = useState<UserFormTab>('account')

  // Built from the policy the server published, so this form cannot quietly disagree with what the user
  // store will accept.
  const schema = useMemo(
    () =>
      z.object({
        email: z.string().trim().min(1, t('validation.emailRequiredNew')).email(t('validation.emailInvalid')),
        password: editing ? z.string() : newPasswordSchema(policy, t, t('validation.passwordRequiredNew')),
        ...userDetailsShape(t),
      }),
    [editing, policy, t],
  )

  const save = useMutation({
    mutationFn: () => {
      const body = { ...formToDetails(details), isAdministrator: administrator }

      return user
        ? userAdminApi.update(user.id, body)
        : userAdminApi.create({ ...body, email: email.trim(), password })
    },
    meta: { handlesErrors: true },
    onSuccess: (saved) => {
      notify.success(editing ? t('users.edit.saved') : t('users.create.created'), saved.email)
      onSaved()
      onClose()
    },
    onError: (failure) => {
      showFirstInvalidTab(Object.keys(serverFieldErrors(failure)))
      reportFormError(failure, editing ? t('users.edit.failed') : t('users.create.failed'))
    },
  })

  /** Errors can sit on a tab that is not showing; open the first tab that has one so it is seen. */
  function showFirstInvalidTab(fields: readonly string[]) {
    const invalid = new Set(fields.map((field) => tabOfField(field)))
    const first = USER_FORM_TABS.find((entry) => invalid.has(entry))

    if (first) {
      setTab(first)
    }
  }

  function submit(event: FormEvent) {
    event.preventDefault()

    const parsed = schema.safeParse({ email, password, ...details })

    if (!parsed.success) {
      const found = zodFieldErrors<AccountField | UserDetailsField>(parsed.error)
      setErrors(found)
      showFirstInvalidTab(Object.keys(found))

      return
    }

    setErrors({})
    save.mutate()
  }

  const serverErrors = serverFieldErrors(save.error)
  const errorFor = (field: AccountField | UserDetailsField) => errors[field] ?? serverErrors[field]?.[0]
  const detailErrors = Object.fromEntries(
    Object.keys(USER_DETAILS_GROUP_OF).map((field) => [field, errorFor(field as UserDetailsField)]),
  )

  return (
    <>
      <DialogHeader>
        <DialogTitle>{editing ? t('users.edit.title') : t('users.create.title')}</DialogTitle>
        <DialogDescription>{editing ? t('users.edit.description') : t('users.create.description')}</DialogDescription>
      </DialogHeader>

      <form id="user-form" onSubmit={submit} noValidate>
        <Tabs value={tab} onValueChange={(value) => setTab(value as UserFormTab)}>
          <TabsList variant="line" className="max-w-full flex-wrap justify-start group-data-[orientation=horizontal]/tabs:h-auto">
            <TabsTrigger value="account">{t('users.tab.account')}</TabsTrigger>
            <TabsTrigger value="profile">{t('users.tab.profile')}</TabsTrigger>
            <TabsTrigger value="address">{t('users.tab.address')}</TabsTrigger>
          </TabsList>
          <Separator className="-mt-2" />

          <TabsContent value="account" className="grid gap-6 pt-4">
            <fieldset className="grid gap-3">
              {/* The tab already says "Account"; the legend stays for screen readers. */}
              <legend className="sr-only">{t('profile.section.account')}</legend>
              <div className="grid items-start gap-5 sm:grid-cols-2">
                <FormField label={t('common.email')} htmlFor="user-email" error={errorFor('email')}>
                  <Input
                    type="email"
                    autoComplete="off"
                    disabled={editing}
                    value={email}
                    onChange={(event) => setEmail(event.target.value)}
                  />
                </FormField>

                {!editing && (
                  <FormField
                    label={t('common.password')}
                    htmlFor="user-password"
                    error={errorFor('password')}
                    hint={describePasswordPolicy(policy, t)}
                  >
                    <Input
                      type="password"
                      autoComplete="new-password"
                      value={password}
                      onChange={(event) => setPassword(event.target.value)}
                    />
                  </FormField>
                )}
              </div>

              {/* Only an administrator sees the flag; nobody can change their own. */}
              {isAdministrator && (
                <div className="flex items-start justify-between gap-4 rounded-md border p-3">
                  <div className="grid gap-1">
                    <Label htmlFor="user-administrator">{t('users.administrator')}</Label>
                    <p className="text-muted-foreground text-xs">
                      {self ? t('users.administratorSelfHint') : t('users.administratorHint')}
                    </p>
                  </div>
                  <Switch
                    id="user-administrator"
                    checked={administrator}
                    disabled={self}
                    onCheckedChange={setAdministrator}
                  />
                </div>
              )}
            </fieldset>

            <Separator />

            {user ? (
              <AvatarUpload
                user={user}
                upload={(image) => userAdminApi.uploadAvatar(user.id, image)}
                remove={() => userAdminApi.removeAvatar(user.id)}
                onChanged={onSaved}
              />
            ) : (
              <p className="text-muted-foreground text-sm">{t('users.avatarAfterCreate')}</p>
            )}
          </TabsContent>

          <TabsContent value="profile" className="grid gap-6 pt-4">
            <UserDetailsFields
              idPrefix="user"
              groups={['personal', 'work']}
              values={details}
              onChange={(field, value) => setDetails((current) => ({ ...current, [field]: value }))}
              errors={detailErrors}
            />
          </TabsContent>

          <TabsContent value="address" className="grid gap-6 pt-4">
            <UserDetailsFields
              idPrefix="user"
              groups={['address']}
              values={details}
              onChange={(field, value) => setDetails((current) => ({ ...current, [field]: value }))}
              errors={detailErrors}
            />
          </TabsContent>
        </Tabs>
      </form>

      <DialogFooter>
        <Button variant="outline" onClick={onClose}>
          {t('common.cancel')}
        </Button>
        <Button type="submit" form="user-form" disabled={save.isPending}>
          {save.isPending && <Loader2 className="animate-spin" />}
          {save.isPending ? t('common.saving') : editing ? t('common.save') : t('users.create.submit')}
        </Button>
      </DialogFooter>
    </>
  )
}
