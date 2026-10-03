import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { z } from 'zod'
import { accountApi } from '../api/auth'
import { ApiError } from '../api/client'
import type { PasswordPolicy } from '../api/types'
import { Badge, Button, Card, Field } from '../components/ui'
import { useSession } from '../hooks/useSession'
import { describePasswordPolicy, newPasswordSchema } from '../lib/passwordPolicy'

const profileSchema = z.object({
  displayName: z
    .string()
    .trim()
    .min(2, 'Display name must be at least 2 characters.')
    .max(120, 'Display name must be at most 120 characters.'),
})

/**
 * The signed-in account's own settings.
 *
 * Email is shown read-only: it is the sign-in identifier, so changing it needs a confirmation flow that does
 * not exist here. Offering the field would let an account lock itself out.
 */
export function AccountPage() {
  const { user, session } = useSession()
  const queryClient = useQueryClient()

  const profile = useQuery({
    queryKey: ['account-profile'],
    queryFn: ({ signal }) => accountApi.profile(signal),
  })

  if (!session.isAuthenticated) {
    return (
      <Card title="Sign in required">
        <p className="text-muted-foreground text-sm">
          <Link className="underline underline-offset-4" to="/sign-in">
            Sign in
          </Link>{' '}
          to view your account.
        </p>
      </Card>
    )
  }

  return (
    <div className="space-y-8">
      <h1 className="text-2xl font-semibold tracking-tight">Account</h1>

      {profile.isPending && (
        <p role="status" className="text-muted-foreground py-4 text-sm">
          Loading…
        </p>
      )}

      {profile.data && <ProfileForm queryClient={queryClient} initial={profile.data.displayName} />}

      <PasswordForm policy={session.passwordPolicy} />

      <Card title="Details">
        <dl className="grid gap-4 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-muted-foreground">Email</dt>
            <dd className="mt-1 font-mono text-xs">{user?.email}</dd>
          </div>
          <div>
            <dt className="text-muted-foreground">Email confirmed</dt>
            <dd className="mt-1">
              {user?.emailConfirmed ? (
                <Badge tone="success">Confirmed</Badge>
              ) : (
                <Badge tone="warning">Not confirmed</Badge>
              )}
            </dd>
          </div>
          <div>
            <dt className="text-muted-foreground">Roles</dt>
            <dd className="mt-1 flex flex-wrap gap-1">
              {(user?.roles.length ?? 0) === 0 ? (
                <span className="text-muted-foreground">None</span>
              ) : (
                user!.roles.map((role) => <Badge key={role}>{role}</Badge>)
              )}
            </dd>
          </div>
          {profile.data && (
            <div>
              <dt className="text-muted-foreground">Member since</dt>
              <dd className="mt-1">{new Date(profile.data.createdAtUtc).toLocaleDateString()}</dd>
            </div>
          )}
        </dl>
      </Card>
    </div>
  )
}

function ProfileForm({
  queryClient,
  initial,
}: {
  queryClient: ReturnType<typeof useQueryClient>
  initial: string
}) {
  const [displayName, setDisplayName] = useState(initial)
  const [error, setError] = useState<string | undefined>()
  const [saved, setSaved] = useState(false)

  const save = useMutation({
    mutationFn: () => accountApi.updateProfile({ displayName }),
    onSuccess: async (updated) => {
      setSaved(true)
      setError(undefined)
      setDisplayName(updated.displayName)
      // The session query carries the display name shown in the header, so it is stale after this write.
      await queryClient.invalidateQueries({ queryKey: ['session'] })
    },
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    setSaved(false)

    const parsed = profileSchema.safeParse({ displayName })

    if (!parsed.success) {
      setError(parsed.error.issues[0]?.message)

      return
    }

    setError(undefined)
    save.mutate()
  }

  const serverFieldError =
    save.error instanceof ApiError ? save.error.fieldErrors.displayName?.[0] : undefined

  // A 400 has already been reported field by field; repeating it as a summary would duplicate the message.
  const summaryError =
    save.isError && !(save.error instanceof ApiError && save.error.status === 400)
      ? save.error instanceof ApiError
        ? save.error.summary
        : 'The change could not be saved.'
      : undefined

  return (
    <Card title="Display name">
      <form onSubmit={submit} noValidate className="space-y-4">
        <Field label="Display name" htmlFor="displayName" error={error ?? serverFieldError}>
          <input
            id="displayName"
            name="displayName"
            required
            value={displayName}
            onChange={(event) => setDisplayName(event.target.value)}
            aria-invalid={Boolean(error ?? serverFieldError)}
            className="border-input bg-background w-full rounded-md border px-3 py-2 text-sm"
          />
        </Field>

        <div className="flex items-center gap-3">
          <Button type="submit" disabled={save.isPending}>
            {save.isPending ? 'Saving…' : 'Save'}
          </Button>
          {saved && (
            <span role="status" className="text-muted-foreground text-sm">
              Saved.
            </span>
          )}
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

function PasswordForm({ policy }: { policy: PasswordPolicy }) {
  const [values, setValues] = useState({ currentPassword: '', newPassword: '' })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [done, setDone] = useState(false)

  // Built from the policy the server published, so there is no second copy of the rules to drift out of
  // step with the user store.
  const schema = useMemo(
    () =>
      z.object({
        currentPassword: z.string().min(1, 'Enter your current password.'),
        newPassword: newPasswordSchema(policy, 'Enter a new password.'),
      }),
    [policy],
  )

  const change = useMutation({
    mutationFn: () => accountApi.changePassword(values),
    onSuccess: () => {
      setDone(true)
      setErrors({})
      // Cleared rather than kept: leaving the new password in the form after a successful change invites it
      // to be submitted again by a second click.
      setValues({ currentPassword: '', newPassword: '' })
    },
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    setDone(false)

    const parsed = schema.safeParse(values)

    if (!parsed.success) {
      setErrors(
        Object.fromEntries(parsed.error.issues.map((issue) => [issue.path[0] as string, issue.message])),
      )

      return
    }

    setErrors({})
    change.mutate()
  }

  // The server reports a rejected current password without saying which half failed, so neither can this.
  const summaryError = change.isError
    ? change.error instanceof ApiError && change.error.status === 400
      ? 'The change was rejected. Check the current password and the new one against the policy.'
      : change.error instanceof ApiError
        ? change.error.summary
        : 'The password could not be changed.'
    : undefined

  return (
    <Card title="Change password">
      <form onSubmit={submit} noValidate className="space-y-4">
        <Field label="Current password" htmlFor="currentPassword" error={errors.currentPassword}>
          <input
            id="currentPassword"
            name="currentPassword"
            type="password"
            autoComplete="current-password"
            required
            value={values.currentPassword}
            onChange={(event) => setValues({ ...values, currentPassword: event.target.value })}
            aria-invalid={Boolean(errors.currentPassword)}
            className="border-input bg-background w-full rounded-md border px-3 py-2 text-sm"
          />
        </Field>

        <Field
          label="New password"
          htmlFor="newPassword"
          error={errors.newPassword}
          hint={describePasswordPolicy(policy)}
        >
          <input
            id="newPassword"
            name="newPassword"
            type="password"
            autoComplete="new-password"
            required
            value={values.newPassword}
            onChange={(event) => setValues({ ...values, newPassword: event.target.value })}
            aria-invalid={Boolean(errors.newPassword)}
            className="border-input bg-background w-full rounded-md border px-3 py-2 text-sm"
          />
        </Field>

        <div className="flex items-center gap-3">
          <Button type="submit" disabled={change.isPending}>
            {change.isPending ? 'Changing…' : 'Change password'}
          </Button>
          {done && (
            <span role="status" className="text-muted-foreground text-sm">
              Password changed.
            </span>
          )}
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