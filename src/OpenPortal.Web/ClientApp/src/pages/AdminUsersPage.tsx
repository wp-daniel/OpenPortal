import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState, type FormEvent } from 'react'
import { z } from 'zod'
import { ApiError } from '../api/client'
import type { PasswordPolicy, UserSummary } from '../api/types'
import { userAdminApi } from '../api/users'
import { Badge, Button, Card, ErrorPanel, Field } from '../components/ui'
import { describeError, traceIdOf } from '../hooks/useRetryableError'
import { useSession } from '../hooks/useSession'
import { describePasswordPolicy, newPasswordSchema } from '../lib/passwordPolicy'

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
    queryFn: ({ signal }) =>
      userAdminApi.list({ page, pageSize: 20, search: appliedSearch }, signal),
  })

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['admin-users'] })

  const setRoles = useMutation({
    mutationFn: ({ id, roles }: { id: string; roles: string[] }) =>
      userAdminApi.update(id, { displayName: displayNameOf(users.data, id), roles }),
    onSuccess: refresh,
  })

  return (
    <div className="space-y-8">
      <h1 className="text-2xl font-semibold tracking-tight">Users</h1>

      <CreateUserForm onCreated={refresh} policy={session.passwordPolicy} />

      <Card title="Accounts">
        <form
          onSubmit={(event: FormEvent) => {
            event.preventDefault()
            const parsed = searchSchema.safeParse({ search })

            if (parsed.success) {
              setPage(1)
              setAppliedSearch(parsed.data.search)
            }
          }}
          className="mb-5 flex flex-wrap items-end gap-3"
        >
          <Field label="Search" htmlFor="search">
            <input
              id="search"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Email or display name"
              className="border-input bg-background w-full rounded-md border px-3 py-2 text-sm sm:w-72"
            />
          </Field>
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
            onRetry={() => users.refetch()}
          />
        )}

        {users.isPending && (
          <p role="status" className="text-muted-foreground py-6 text-center text-sm">
            Loading…
          </p>
        )}

        {users.data && users.data.items.length === 0 && (
          <p className="text-muted-foreground py-6 text-center text-sm">No accounts match.</p>
        )}

        {users.data && users.data.items.length > 0 && (
          <>
            <table className="w-full text-left text-sm">
              <caption className="sr-only">
                Accounts, page {users.data.page} of {users.data.totalPages}
              </caption>
              <thead className="border-border text-muted-foreground border-b text-xs">
                <tr>
                  <th scope="col" className="py-2 pr-3 font-medium">
                    Email
                  </th>
                  <th scope="col" className="py-2 pr-3 font-medium">
                    Display name
                  </th>
                  <th scope="col" className="py-2 pr-3 font-medium">
                    Roles
                  </th>
                  <th scope="col" className="py-2 pr-3 font-medium">
                    Status
                  </th>
                  <th scope="col" className="py-2 font-medium">
                    Administrator
                  </th>
                </tr>
              </thead>
              <tbody>
                {users.data.items.map((user) => (
                  <tr key={user.id} className="border-border border-b last:border-0">
                    <td className="py-3 pr-3 font-mono text-xs">{user.email}</td>
                    <td className="py-3 pr-3">{user.displayName}</td>
                    <td className="py-3 pr-3">
                      <div className="flex flex-wrap gap-1">
                        {user.roles.map((role) => (
                          <Badge key={role}>{role}</Badge>
                        ))}
                      </div>
                    </td>
                    <td className="py-3 pr-3">
                      {user.isLockedOut ? (
                        <Badge tone="warning">Locked out</Badge>
                      ) : user.emailConfirmed ? (
                        <Badge tone="success">Active</Badge>
                      ) : (
                        <Badge tone="warning">Unconfirmed</Badge>
                      )}
                    </td>
                    <td className="py-3">
                      <Button
                        variant="outline"
                        disabled={setRoles.isPending}
                        onClick={() =>
                          setRoles.mutate({
                            id: user.id,
                            roles: user.roles.includes('Administrator')
                              ? user.roles.filter((role) => role !== 'Administrator')
                              : [...user.roles, 'Administrator'],
                          })
                        }
                      >
                        {user.roles.includes('Administrator') ? 'Revoke admin' : 'Make admin'}
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            <nav aria-label="Pagination" className="mt-4 flex items-center justify-between text-sm">
              <Button variant="outline" disabled={!users.data.hasPrevious} onClick={() => setPage(page - 1)}>
                Previous
              </Button>
              <span className="text-muted-foreground">
                Page {users.data.page} of {users.data.totalPages} ({users.data.totalCount} accounts)
              </span>
              <Button variant="outline" disabled={!users.data.hasNext} onClick={() => setPage(page + 1)}>
                Next
              </Button>
            </nav>
          </>
        )}
      </Card>
    </div>
  )
}

/** Reads the current display name for a row so an update does not blank it. */
function displayNameOf(result: { items: readonly UserSummary[] } | undefined, id: string): string {
  return result?.items.find((item) => item.id === id)?.displayName ?? ''
}

function CreateUserForm({ onCreated, policy }: { onCreated: () => void; policy: PasswordPolicy }) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [roles, setRoles] = useState<string[]>([])
  const [errors, setErrors] = useState<Record<string, string>>({})

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
          .refine(
            (value) => value === '' || value.length >= 2,
            'Display name must be at least 2 characters.',
          )
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
    onSuccess: async () => {
      onCreated()
      setEmail('')
      setPassword('')
      setDisplayName('')
      setRoles([])
      setErrors({})
    },
  })

  function submit(event: FormEvent) {
    event.preventDefault()

    const parsed = schema.safeParse({ email, password, displayName })

    if (!parsed.success) {
      setErrors(
        Object.fromEntries(parsed.error.issues.map((issue) => [issue.path[0] as string, issue.message])),
      )

      return
    }

    setErrors({})
    create.mutate()
  }

  const serverErrors = create.error instanceof ApiError ? create.error.fieldErrors : {}
  const summaryError =
    create.isError && !(create.error instanceof ApiError && create.error.status === 400)
      ? describeError(create.error)
      : undefined

  return (
    <Card title="Create an account" description="The password must satisfy the server's policy.">
      <form onSubmit={submit} noValidate className="space-y-5">
        <div className="grid gap-5 sm:grid-cols-3">
          <Field label="Email" htmlFor="newEmail" error={errors.email ?? serverErrors.email?.[0]}>
            <input
              id="newEmail"
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              className="border-input bg-background w-full rounded-md border px-3 py-2 text-sm"
            />
          </Field>

          <Field
            label="Password"
            htmlFor="newPassword"
            error={errors.password ?? serverErrors.password?.[0]}
            hint={describePasswordPolicy(policy)}
          >
            <input
              id="newPassword"
              type="password"
              autoComplete="new-password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              className="border-input bg-background w-full rounded-md border px-3 py-2 text-sm"
            />
          </Field>

          <Field
            label="Display name"
            htmlFor="newDisplayName"
            error={errors.displayName ?? serverErrors.displayName?.[0]}
            hint="Defaults to the email's local part."
          >
            <input
              id="newDisplayName"
              value={displayName}
              onChange={(event) => setDisplayName(event.target.value)}
              className="border-input bg-background w-full rounded-md border px-3 py-2 text-sm"
            />
          </Field>
        </div>

        <fieldset>
          <legend className="text-sm font-medium">Roles</legend>
          <div className="mt-2 flex gap-4">
            {ROLES.map((role) => (
              <label key={role} className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={roles.includes(role)}
                  onChange={(event) =>
                    setRoles(
                      event.target.checked
                        ? [...roles, role]
                        : roles.filter((entry) => entry !== role),
                    )
                  }
                />
                {role}
              </label>
            ))}
          </div>
        </fieldset>

        <div className="flex items-center gap-3">
          <Button type="submit" disabled={create.isPending}>
            {create.isPending ? 'Creating…' : 'Create account'}
          </Button>
          {summaryError && (
            <span role="alert" className="text-destructive text-sm">
              {summaryError}
            </span>
          )}
        </div>
      </form>
    </Card>
  )
}