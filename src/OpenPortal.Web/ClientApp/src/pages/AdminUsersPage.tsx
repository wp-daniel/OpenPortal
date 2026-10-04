import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { KeyRound, Loader2 } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { z } from 'zod'
import type { PasswordPolicy, UserSummary } from '@/api/types'
import { userAdminApi } from '@/api/users'
import { UserAccessSheet } from '@/components/access/UserAccessSheet'
import { FormField } from '@/components/FormField'
import { PageHeader } from '@/components/PageHeader'
import { Section } from '@/components/Section'
import { ErrorPanel, LoadingState } from '@/components/StatePanels'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Table,
  TableBody,
  TableCaption,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import type { TFunction } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'
import { useSession } from '@/hooks/useSession'
import { notify } from '@/hooks/useToast'
import { reportFormError, serverFieldErrors, zodFieldErrors } from '@/lib/forms'
import { describePasswordPolicy, newPasswordSchema } from '@/lib/passwordPolicy'

/** The roles the server recognises. Anything else is rejected there, so the list is fixed here. */
const ROLES = ['Administrator', 'User'] as const

const makeSearchSchema = (t: TFunction) =>
  z.object({
    search: z.string().max(256, t('validation.searchMax', { max: 256 })),
  })

/** Administrator-only account management. */
export function AdminUsersPage() {
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')
  const [accessUser, setAccessUser] = useState<UserSummary | null>(null)
  const queryClient = useQueryClient()
  const { session } = useSession()
  const { t } = useI18n()
  const searchSchema = useMemo(() => makeSearchSchema(t), [t])

  const users = useQuery({
    queryKey: ['admin-users', page, appliedSearch],
    queryFn: ({ signal }) => userAdminApi.list({ page, pageSize: 20, search: appliedSearch }, signal),
  })

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['admin-users'] })

  // Failures are toasted by the global MutationCache handler, so a failed role change is never silent.
  const setRoles = useMutation({
    mutationFn: ({ user, roles }: { user: UserSummary; roles: string[] }) =>
      userAdminApi.update(user.id, { displayName: user.displayName, roles }),
    onSuccess: async (_result, { user }) => {
      notify.success(t('users.rolesUpdated'), user.email)
      await refresh()
    },
  })

  function applySearch(event: FormEvent) {
    event.preventDefault()
    const parsed = searchSchema.safeParse({ search })

    if (!parsed.success) {
      notify.warning(parsed.error.issues[0]?.message ?? t('validation.invalidSearch'))

      return
    }

    setPage(1)
    setAppliedSearch(parsed.data.search)
  }

  return (
    <div className="space-y-6">
      <PageHeader title={t('users.title')} description={t('users.description')} />

      <CreateUserForm onCreated={refresh} policy={session.passwordPolicy} />

      <Section title={t('users.accounts')}>
        <form onSubmit={applySearch} className="mb-5 flex flex-wrap items-end gap-3">
          <FormField label={t('common.search')} htmlFor="search" className="w-full sm:w-72">
            <Input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder={t('users.searchPlaceholder')}
            />
          </FormField>
          <Button type="submit" variant="outline">
            {t('common.search')}
          </Button>
          {appliedSearch && (
            <Button
              variant="ghost"
              onClick={() => {
                setSearch('')
                setAppliedSearch('')
                setPage(1)
              }}
            >
              {t('common.clear')}
            </Button>
          )}
        </form>

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
                  <TableHead>{t('common.email')}</TableHead>
                  <TableHead>{t('common.displayName')}</TableHead>
                  <TableHead>{t('common.roles')}</TableHead>
                  <TableHead>{t('common.status')}</TableHead>
                  <TableHead className="text-right">{t('common.actions')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {users.data.items.map((user) => {
                  const isAdmin = user.roles.includes('Administrator')

                  return (
                    <TableRow key={user.id}>
                      <TableCell className="font-mono text-xs">{user.email}</TableCell>
                      <TableCell>{user.displayName}</TableCell>
                      <TableCell>
                        <div className="flex flex-wrap gap-1">
                          {user.roles.map((role) => (
                            <Badge key={role} variant="secondary">
                              {t(`role.${role}`)}
                            </Badge>
                          ))}
                        </div>
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
                        <Button variant="ghost" size="sm" onClick={() => setAccessUser(user)}>
                          <KeyRound />
                          {t('users.access')}
                        </Button>
                        <Button
                          variant="outline"
                          size="sm"
                          disabled={setRoles.isPending}
                          onClick={() =>
                            setRoles.mutate({
                              user,
                              roles: isAdmin
                                ? user.roles.filter((role) => role !== 'Administrator')
                                : [...user.roles, 'Administrator'],
                            })
                          }
                        >
                          {isAdmin ? t('users.revokeAdmin') : t('users.makeAdmin')}
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

      <UserAccessSheet user={accessUser} onClose={() => setAccessUser(null)} />
    </div>
  )
}

function CreateUserForm({ onCreated, policy }: { onCreated: () => void; policy: PasswordPolicy }) {
  const { t } = useI18n()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [roles, setRoles] = useState<string[]>([])
  const [errors, setErrors] = useState<Partial<Record<'email' | 'password' | 'displayName', string>>>({})

  // Built from the policy the server published, so this form cannot quietly disagree with what the user
  // store will accept. The display name stays optional because the server derives one from the address.
  const schema = useMemo(
    () =>
      z.object({
        email: z.string().trim().min(1, t('validation.emailRequiredNew')).email(t('validation.emailInvalid')),
        password: newPasswordSchema(policy, t, t('validation.passwordRequiredNew')),
        displayName: z
          .string()
          .trim()
          .refine((value) => value === '' || value.length >= 2, t('validation.displayNameMin', { min: 2 }))
          .max(120, t('validation.displayNameMax', { max: 120 })),
      }),
    [policy, t],
  )

  const create = useMutation({
    mutationFn: () => {
      const trimmedDisplayName = displayName.trim()

      return userAdminApi.create({
        email: email.trim(),
        password,
        // Omitted rather than sent blank when empty: the server defaults it to the email's local part.
        displayName: trimmedDisplayName === '' ? null : trimmedDisplayName,
        roles,
      })
    },
    meta: { handlesErrors: true },
    onSuccess: async (created) => {
      notify.success(t('users.create.created'), created.email)
      onCreated()
      setEmail('')
      setPassword('')
      setDisplayName('')
      setRoles([])
      setErrors({})
    },
    onError: (failure) => reportFormError(failure, t('users.create.failed')),
  })

  function submit(event: FormEvent) {
    event.preventDefault()

    const parsed = schema.safeParse({ email, password, displayName })

    if (!parsed.success) {
      setErrors(zodFieldErrors(parsed.error))

      return
    }

    setErrors({})
    create.mutate()
  }

  const serverErrors = serverFieldErrors(create.error)

  return (
    <Section title={t('users.create.title')} description={t('users.create.description')}>
      <form onSubmit={submit} noValidate className="grid gap-5">
        <div className="grid items-start gap-5 sm:grid-cols-3">
          <FormField label={t('common.email')} htmlFor="newEmail" error={errors.email ?? serverErrors.email?.[0]}>
            <Input type="email" value={email} onChange={(event) => setEmail(event.target.value)} />
          </FormField>

          <FormField
            label={t('common.password')}
            htmlFor="newPassword"
            error={errors.password ?? serverErrors.password?.[0]}
            hint={describePasswordPolicy(policy, t)}
          >
            <Input
              type="password"
              autoComplete="new-password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />
          </FormField>

          <FormField
            label={t('common.displayName')}
            htmlFor="newDisplayName"
            error={errors.displayName ?? serverErrors.displayName?.[0]}
            hint={t('users.create.displayNameHint')}
          >
            <Input value={displayName} onChange={(event) => setDisplayName(event.target.value)} />
          </FormField>
        </div>

        <fieldset className="grid gap-2">
          <legend className="text-sm font-medium">{t('common.roles')}</legend>
          <div className="flex gap-6">
            {ROLES.map((role) => (
              <div key={role} className="flex items-center gap-2">
                <Checkbox
                  id={`role-${role}`}
                  checked={roles.includes(role)}
                  onCheckedChange={(checked) =>
                    setRoles(checked === true ? [...roles, role] : roles.filter((entry) => entry !== role))
                  }
                />
                <Label htmlFor={`role-${role}`} className="font-normal">
                  {t(`role.${role}`)}
                </Label>
              </div>
            ))}
          </div>
        </fieldset>

        <div>
          <Button type="submit" disabled={create.isPending}>
            {create.isPending && <Loader2 className="animate-spin" />}
            {create.isPending ? t('users.create.submitting') : t('users.create.submit')}
          </Button>
        </div>
      </form>
    </Section>
  )
}
