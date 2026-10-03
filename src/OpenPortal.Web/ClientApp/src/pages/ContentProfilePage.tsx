import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { z } from 'zod'
import { ApiError } from '../api/client'
import { contentAdminApi, profileToForm, toProfileRequest, type ProfileForm } from '../api/content'
import { Button, Card, ErrorPanel, Field } from '../components/ui'
import { describeError, traceIdOf } from '../hooks/useRetryableError'

const schema = z.object({
  displayName: z.string().trim().min(2, 'Display name must be at least 2 characters.').max(120),
  headline: z.string().max(200, 'Keep the headline under 200 characters.'),
  summary: z.string().max(4_000, 'Keep the summary under 4000 characters.'),
  location: z.string().max(200, 'Keep the location under 200 characters.'),
  email: z.union([z.literal(''), z.string().email('Enter a valid public contact address.')]),
  avatarUrl: z.union([z.literal(''), z.string().url('Enter an absolute image URL.')]),
})

/** Edits the single public profile, including its ordered social links. */
export function ContentProfilePage() {
  const queryClient = useQueryClient()

  const profile = useQuery({
    queryKey: ['manage-profile'],
    queryFn: ({ signal }) => contentAdminApi.profile(signal),
  })

  const save = useMutation({
    mutationFn: (form: ProfileForm) => contentAdminApi.saveProfile(toProfileRequest(form)),
    onSuccess: async () => {
      // Both the editor and the public page read from these keys; neither may keep showing the old copy.
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['manage-profile'] }),
        queryClient.invalidateQueries({ queryKey: ['public-content'] }),
      ])
    },
  })

  if (profile.isError) {
    return (
      <ErrorPanel
        message={describeError(profile.error)}
        traceId={traceIdOf(profile.error)}
        onRetry={() => profile.refetch()}
      />
    )
  }

  if (profile.isPending) {
    return (
      <p role="status" className="text-muted-foreground py-8 text-center text-sm">
        Loading…
      </p>
    )
  }

  return (
    <ProfileForm
      initial={profileToForm(profile.data)}
      onSubmit={(form) => save.mutate(form)}
      isSaving={save.isPending}
      saved={save.isSuccess}
      error={save.isError ? save.error : undefined}
      created={profile.data !== null}
    />
  )
}

function ProfileForm({
  initial,
  onSubmit,
  isSaving,
  saved,
  error,
  created,
}: {
  initial: ProfileForm
  onSubmit: (form: ProfileForm) => void
  isSaving: boolean
  saved: boolean
  error: unknown
  created: boolean
}) {
  const [form, setForm] = useState(initial)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})

  function submit(event: FormEvent) {
    event.preventDefault()

    const parsed = schema.safeParse(form)

    if (!parsed.success) {
      setFieldErrors(
        Object.fromEntries(parsed.error.issues.map((issue) => [issue.path[0] as string, issue.message])),
      )

      return
    }

    setFieldErrors({})
    onSubmit(form)
  }

  const serverErrors = error instanceof ApiError ? error.fieldErrors : {}
  const summaryError = error && !(error instanceof ApiError && error.status === 400)
    ? describeError(error)
    : undefined

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Public profile</h1>
        <p className="text-muted-foreground mt-1 text-sm">
          {created
            ? 'This is what visitors see at the top of the portal.'
            : 'No profile exists yet. Saving creates it.'}
        </p>
      </div>

      <form onSubmit={submit} noValidate className="space-y-8">
        <Card title="Identity">
          <div className="grid gap-5 sm:grid-cols-2">
            <Field label="Display name" htmlFor="displayName" error={fieldErrors.displayName ?? serverErrors.displayName?.[0]}>
              <input
                id="displayName"
                required
                value={form.displayName}
                onChange={(event) => setForm({ ...form, displayName: event.target.value })}
                className="border-input bg-background w-full rounded-md border px-3 py-2 text-sm"
              />
            </Field>

            <Field label="Headline" htmlFor="headline" error={fieldErrors.headline}>
              <input
                id="headline"
                value={form.headline}
                onChange={(event) => setForm({ ...form, headline: event.target.value })}
                className="border-input bg-background w-full rounded-md border px-3 py-2 text-sm"
              />
            </Field>

            <Field label="Location" htmlFor="location" error={fieldErrors.location}>
              <input
                id="location"
                value={form.location}
                onChange={(event) => setForm({ ...form, location: event.target.value })}
                className="border-input bg-background w-full rounded-md border px-3 py-2 text-sm"
              />
            </Field>

            <Field
              label="Public contact email"
              htmlFor="contactEmail"
              error={fieldErrors.email}
              hint="Separate from the sign-in address, which is never shown publicly."
            >
              <input
                id="contactEmail"
                type="email"
                value={form.email}
                onChange={(event) => setForm({ ...form, email: event.target.value })}
                className="border-input bg-background w-full rounded-md border px-3 py-2 text-sm"
              />
            </Field>

            <Field label="Avatar URL" htmlFor="avatarUrl" error={fieldErrors.avatarUrl}>
              <input
                id="avatarUrl"
                type="url"
                value={form.avatarUrl}
                onChange={(event) => setForm({ ...form, avatarUrl: event.target.value })}
                className="border-input bg-background w-full rounded-md border px-3 py-2 text-sm"
              />
            </Field>
          </div>

          <div className="mt-5">
            <Field label="Summary" htmlFor="summary" error={fieldErrors.summary}>
              <textarea
                id="summary"
                rows={6}
                value={form.summary}
                onChange={(event) => setForm({ ...form, summary: event.target.value })}
                className="border-input bg-background w-full rounded-md border px-3 py-2 text-sm"
              />
            </Field>
          </div>
        </Card>

        <Card
          title="Social links"
          description="Shown in order. Saving replaces the whole list."
          actions={
            <Button
              variant="outline"
              onClick={() =>
                setForm({
                  ...form,
                  socialLinks: [...form.socialLinks, { platform: '', url: '', label: '' }],
                })
              }
            >
              Add link
            </Button>
          }
        >
          {form.socialLinks.length === 0 ? (
            <p className="text-muted-foreground text-sm">No links yet.</p>
          ) : (
            <ul className="space-y-4">
              {form.socialLinks.map((link, index) => (
                <li key={index} className="grid gap-3 sm:grid-cols-[1fr_2fr_1fr_auto]">
                  <Field label="Platform" htmlFor={`platform-${index}`}>
                    <input
                      id={`platform-${index}`}
                      value={link.platform}
                      onChange={(event) =>
                        setForm({
                          ...form,
                          socialLinks: form.socialLinks.map((entry, position) =>
                            position === index ? { ...entry, platform: event.target.value } : entry,
                          ),
                        })
                      }
                      className="border-input bg-background w-full rounded-md border px-3 py-2 text-sm"
                    />
                  </Field>

                  <Field label="URL" htmlFor={`url-${index}`}>
                    <input
                      id={`url-${index}`}
                      type="url"
                      value={link.url}
                      onChange={(event) =>
                        setForm({
                          ...form,
                          socialLinks: form.socialLinks.map((entry, position) =>
                            position === index ? { ...entry, url: event.target.value } : entry,
                          ),
                        })
                      }
                      className="border-input bg-background w-full rounded-md border px-3 py-2 text-sm"
                    />
                  </Field>

                  <Field label="Label" htmlFor={`label-${index}`} hint="Optional.">
                    <input
                      id={`label-${index}`}
                      value={link.label}
                      onChange={(event) =>
                        setForm({
                          ...form,
                          socialLinks: form.socialLinks.map((entry, position) =>
                            position === index ? { ...entry, label: event.target.value } : entry,
                          ),
                        })
                      }
                      className="border-input bg-background w-full rounded-md border px-3 py-2 text-sm"
                    />
                  </Field>

                  <div className="flex items-end">
                    <Button
                      variant="ghost"
                      aria-label={`Remove link ${index + 1}`}
                      onClick={() =>
                        setForm({
                          ...form,
                          socialLinks: form.socialLinks.filter((_, position) => position !== index),
                        })
                      }
                    >
                      Remove
                    </Button>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </Card>

        <div className="flex items-center gap-3">
          <Button type="submit" disabled={isSaving}>
            {isSaving ? 'Saving…' : 'Save profile'}
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
    </div>
  )
}