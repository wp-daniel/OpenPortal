import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { z } from 'zod'
import { authApi } from '@/api/auth'
import { ApiError } from '@/api/client'
import { FormField } from '@/components/FormField'
import { Section } from '@/components/Section'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { SESSION_QUERY_KEY } from '@/hooks/useSession'
import { notify } from '@/hooks/useToast'
import { useI18n } from '@/i18n/useI18n'
import type { TFunction } from '@/i18n/store'
import { zodFieldErrors } from '@/lib/forms'

/**
 * Sign-in.
 *
 * The schema checks only what the client can judge. Password complexity is deliberately not enforced here:
 * the authoritative policy is published by the server and enforced by the user store, and a client copy would
 * either drift or reject passwords the server would have accepted.
 *
 * Navigation after success is not done here: invalidating the session makes <PublicOnlyRoute> redirect to
 * the page the user originally asked for (or the dashboard).
 */
const makeSchema = (t: TFunction) =>
  z.object({
    email: z.string().min(1, t('validation.emailRequired')).email(t('validation.emailInvalid')),
    password: z.string().min(1, t('validation.passwordRequired')),
    rememberMe: z.boolean(),
  })

type Fields = z.infer<ReturnType<typeof makeSchema>>

export function SignInPage() {
  const [fields, setFields] = useState<Fields>({ email: '', password: '', rememberMe: false })
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<keyof Fields, string>>>({})
  const queryClient = useQueryClient()
  const { t } = useI18n()
  const schema = useMemo(() => makeSchema(t), [t])

  const signIn = useMutation({
    mutationFn: (values: Fields) => authApi.login(values.email, values.password, values.rememberMe),
    meta: { handlesErrors: true },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: SESSION_QUERY_KEY }),
    onError: (error) => {
      // The server deliberately does not say which of the two fields was wrong, so this text must not imply
      // that either one was. Echoing "unknown email" would confirm which addresses exist.
      if (error instanceof ApiError && error.isUnauthenticated) {
        notify.error(t('signIn.failed'), t('signIn.rejected'))
      } else {
        notify.fromError(error, t('signIn.failed'))
      }
    },
  })

  function submit(event: FormEvent) {
    event.preventDefault()

    const parsed = schema.safeParse(fields)

    if (!parsed.success) {
      setFieldErrors(zodFieldErrors<keyof Fields>(parsed.error))

      return
    }

    setFieldErrors({})
    signIn.mutate(parsed.data)
  }

  const serverErrors = signIn.error instanceof ApiError ? signIn.error.fieldErrors : {}

  return (
    <Section
      title={t('signIn.title')}
      description={t('signIn.description')}
      className="bg-card/80 shadow-lg backdrop-blur"
    >
      <form onSubmit={submit} noValidate className="grid gap-5">
        <FormField label={t('common.email')} htmlFor="email" error={fieldErrors.email ?? serverErrors.email?.[0]}>
          <Input
            name="email"
            type="email"
            autoComplete="username"
            required
            value={fields.email}
            onChange={(event) => setFields({ ...fields, email: event.target.value })}
          />
        </FormField>

        <FormField label={t('common.password')} htmlFor="password" error={fieldErrors.password ?? serverErrors.password?.[0]}>
          <Input
            name="password"
            type="password"
            autoComplete="current-password"
            required
            value={fields.password}
            onChange={(event) => setFields({ ...fields, password: event.target.value })}
          />
        </FormField>

        <div className="flex items-center gap-2">
          <Checkbox
            id="rememberMe"
            checked={fields.rememberMe}
            onCheckedChange={(checked) => setFields({ ...fields, rememberMe: checked === true })}
          />
          <Label htmlFor="rememberMe" className="font-normal">
            {t('signIn.remember')}
          </Label>
        </div>

        <Button type="submit" disabled={signIn.isPending} className="w-full">
          {signIn.isPending && <Loader2 className="animate-spin" />}
          {signIn.isPending ? t('signIn.submitting') : t('signIn.submit')}
        </Button>
      </form>
    </Section>
  )
}
