import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { z } from 'zod'
import { authApi } from '../api/auth'
import { ApiError } from '../api/client'
import { Button, Card, Field } from '../components/ui'
import { useSession } from '../hooks/useSession'

/**
 * Sign-in.
 *
 * The schema checks only what the client can judge. Password complexity is deliberately not enforced here:
 * the authoritative policy is published by the server and enforced by the user store, and a client copy would
 * either drift or reject passwords the server would have accepted.
 */
const schema = z.object({
  email: z.string().min(1, 'Email is required.').email('Enter a valid email address.'),
  password: z.string().min(1, 'Password is required.'),
  rememberMe: z.boolean(),
})

type Fields = z.infer<typeof schema>

export function SignInPage() {
  const [fields, setFields] = useState<Fields>({ email: '', password: '', rememberMe: false })
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<keyof Fields, string>>>({})
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { session } = useSession()

  const signIn = useMutation({
    mutationFn: (values: Fields) => authApi.login(values.email, values.password, values.rememberMe),
    onSuccess: async () => {
      // The session query is the app's source of truth for who is signed in; without invalidating it the
      // header would still show the anonymous state after a successful sign-in.
      await queryClient.invalidateQueries({ queryKey: ['session'] })
      await navigate('/', { replace: true })
    },
  })

  function submit(event: FormEvent) {
    event.preventDefault()

    const parsed = schema.safeParse(fields)

    if (!parsed.success) {
      setFieldErrors(
        Object.fromEntries(
          parsed.error.issues.map((issue) => [issue.path[0] as string, issue.message]),
        ),
      )

      return
    }

    setFieldErrors({})
    signIn.mutate(parsed.data)
  }

  const serverErrors = signIn.error instanceof ApiError ? signIn.error.fieldErrors : {}

  if (session.isAuthenticated) {
    return (
      <Card title="You are already signed in">
        <p className="text-muted-foreground text-sm">
          Sign out first if you want to use a different account.
        </p>
      </Card>
    )
  }

  return (
    <div className="mx-auto max-w-md">
      <Card title="Sign in" description="Use the email address of an OpenPortal account.">
        <form onSubmit={submit} noValidate className="space-y-5">
          <Field label="Email" htmlFor="email" error={fieldErrors.email ?? serverErrors.email?.[0]}>
            <input
              id="email"
              name="email"
              type="email"
              autoComplete="username"
              required
              value={fields.email}
              onChange={(event) => setFields({ ...fields, email: event.target.value })}
              aria-invalid={Boolean(fieldErrors.email ?? serverErrors.email)}
              className="border-input bg-background w-full rounded-md border px-3 py-2 text-sm"
            />
          </Field>

          <Field
            label="Password"
            htmlFor="password"
            error={fieldErrors.password ?? serverErrors.password?.[0]}
          >
            <input
              id="password"
              name="password"
              type="password"
              autoComplete="current-password"
              required
              value={fields.password}
              onChange={(event) => setFields({ ...fields, password: event.target.value })}
              aria-invalid={Boolean(fieldErrors.password ?? serverErrors.password)}
              className="border-input bg-background w-full rounded-md border px-3 py-2 text-sm"
            />
          </Field>

          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={fields.rememberMe}
              onChange={(event) => setFields({ ...fields, rememberMe: event.target.checked })}
            />
            Keep me signed in on this browser
          </label>

          {signIn.isError && (
            <p role="alert" className="text-destructive text-sm font-medium">
              {/*
                The server deliberately does not say which of the two fields was wrong, so this text must not
                imply that either one was. Echoing "unknown email" would confirm which addresses exist.
              */}
              {signIn.error instanceof ApiError && signIn.error.status === 401
                ? 'Those credentials were not accepted.'
                : signIn.error instanceof ApiError
                  ? signIn.error.summary
                  : 'The request could not be completed.'}
            </p>
          )}

          <Button type="submit" disabled={signIn.isPending} className="w-full">
            {signIn.isPending ? 'Signing in…' : 'Sign in'}
          </Button>
        </form>
      </Card>
    </div>
  )
}