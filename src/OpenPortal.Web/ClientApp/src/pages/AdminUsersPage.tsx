import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { z } from 'zod'
import type { PasswordPolicy, UserSummary } from '@/api/types'
import { userAdminApi } from '@/api/users'
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
import { describeError, traceIdOf } from '@/lib/errors'
import { useSession } from '@/hooks/useSession'
import { notify } from '@/hooks/useToast'
import { reportFormError, serverFieldErrors, zodFieldErrors } from '@/lib/forms'
import { describePasswordPolicy, newPasswordSchema } from '@/lib/passwordPolicy'

/** The roles the server recognises. Anything else is rejected there, so the list is fixed here. */
const ROLES = ['Administrator', 'User'] as const

const searchSchema = z.object({
  search: z.string().max(256, 'Keep the search under 256 characters.'),
})

/** Administrator-only account management. */
export function AdminUsersPage() {
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')
  const queryClient = useQueryClient()
  const { session } = useSession()

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
      notify.success('Roles updated', user.email)
      await refresh()
    },
  })

  function applySearch(event: FormEvent) {
    event.preventDefault()
    const parsed = searchSchema.safeParse({ search })

    if (!parsed.success) {
      notify.warning(parsed.error.issues[0]?.message ?? 'Invalid search')

      return
    }

    setPage(1)
    setAppliedSearch(parsed.data.search)
  }

  return (
    <div className="space-y-6">
      <PageHeader title="Users" description="Create accounts and manage who can administer the portal." />

      <CreateUserForm onCreated={refresh} policy={session.passwordPolicy} />

      <Section title="Accounts">
        <form onSubmit={applySearch} className="mb-5 flex flex-wrap items-end gap-3">
          <FormField label="Search" htmlFor="search" className="w-full sm:w-72">
            <Input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Email or display name"
            />
          </FormField>
          <Button type="submit" variant="outline">
            Search
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
              Clear
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
          <p className="text-muted-foreground py-6 text-center text-sm">No accounts match.</p>
        )}

        {users.data && users.data.items.length > 0 && (
          <>
            <Table>
              <TableCaption className="sr-only">
                Accounts, page {users.data.page} of {users.data.totalPages}
              </TableCaption>
              <TableHeader>
                <TableRow>
                  <TableHead>Email</TableHead>
                  <TableHead>Display name</TableHead>
                  <TableHead>Roles</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
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
                              {role}
                            </Badge>
                          ))}
                        </div>
                      </TableCell>
                      <TableCell>
                        {user.isLockedOut ? (
                          <Badge variant="warning">Locked out</Badge>
                        ) : user.emailConfirmed ? (
                          <Badge variant="success">Active</Badge>
                        ) : (
                          <Badge variant="warning">Unconfirmed</Badge>
                        )}
                      </TableCell>
                      <TableCell className="text-right">
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
                          {isAdmin ? 'Revoke admin' : 'Make admin'}
                        </Button>
                      </TableCell>
                    </TableRow>
                  )
                })}
              </TableBody>
            </Table>

            <nav aria-label="Pagination" className="mt-4 flex items-center justify-between gap-2 text-sm">
              <Button variant="outline" size="sm" disabled={!users.data.hasPrevious} onClick={() => setPage(page - 1)}>
                Previous
              </Button>
              <span className="text-muted-foreground text-center">
                Page {users.data.page} of {users.data.totalPages} ({users.data.totalCount} accounts)
              </span>
              <Button variant="outline" size="sm" disabled={!users.data.hasNext} onClick={() => setPage(page + 1)}>
                Next
              </Button>
            </nav>
          </>
        )}
      </Section>
    </div>
  )
}

function CreateUserForm({ onCreated, policy }: { onCreated: () => void; policy: PasswordPolicy }) {
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
        email: z.string().trim().min(1, 'An email is required.').email('Enter a valid email address.'),
        password: newPasswordSchema(policy, 'A password is required.'),
        displayName: z
          .string()
          .trim()
          .refine((value) => value === '' || value.length >= 2, 'Display name must be at least 2 characters.')
          .max(120, 'Display name must be at most 120 characters.'),
      }),
    [policy],
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
      notify.success('Account created', created.email)
      onCreated()
      setEmail('')
      setPassword('')
      setDisplayName('')
      setRoles([])
      setErrors({})
    },
    onError: (failure) => reportFormError(failure, 'The account could not be created'),
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
    <Section title="Create an account" description="The password must satisfy the server's policy.">
      <form onSubmit={submit} noValidate className="grid gap-5">
        <div className="grid items-start gap-5 sm:grid-cols-3">
          <FormField label="Email" htmlFor="newEmail" error={errors.email ?? serverErrors.email?.[0]}>
            <Input type="email" value={email} onChange={(event) => setEmail(event.target.value)} />
          </FormField>

          <FormField
            label="Password"
            htmlFor="newPassword"
            error={errors.password ?? serverErrors.password?.[0]}
            hint={describePasswordPolicy(policy)}
          >
            <Input
              type="password"
              autoComplete="new-password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />
          </FormField>

          <FormField
            label="Display name"
            htmlFor="newDisplayName"
            error={errors.displayName ?? serverErrors.displayName?.[0]}
            hint="Defaults to the email's local part."
          >
            <Input value={displayName} onChange={(event) => setDisplayName(event.target.value)} />
          </FormField>
        </div>

        <fieldset className="grid gap-2">
          <legend className="text-sm font-medium">Roles</legend>
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
                  {role}
                </Label>
              </div>
            ))}
          </div>
        </fieldset>

        <div>
          <Button type="submit" disabled={create.isPending}>
            {create.isPending && <Loader2 className="animate-spin" />}
            {create.isPending ? 'Creating…' : 'Create account'}
          </Button>
        </div>
      </form>
    </Section>
  )
}
