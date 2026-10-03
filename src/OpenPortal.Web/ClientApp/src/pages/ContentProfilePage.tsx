import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2, Plus, Trash2 } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { z } from 'zod'
import { contentAdminApi, profileToForm, toProfileRequest, type ProfileForm as ProfileFormValues } from '@/api/content'
import { FormField } from '@/components/FormField'
import { PageHeader } from '@/components/PageHeader'
import { Section } from '@/components/Section'
import { ErrorPanel, LoadingState } from '@/components/StatePanels'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import type { TFunction } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'
import { notify } from '@/hooks/useToast'
import { reportFormError, serverFieldErrors, zodFieldErrors } from '@/lib/forms'

const makeSchema = (t: TFunction) =>
  z.object({
    displayName: z.string().trim().min(2, t('validation.displayNameMin', { min: 2 })).max(120),
    headline: z.string().max(200, t('validation.maxLength', { max: 200 })),
    summary: z.string().max(4_000, t('validation.maxLength', { max: 4000 })),
    location: z.string().max(200, t('validation.maxLength', { max: 200 })),
    email: z.union([z.literal(''), z.string().email(t('validation.contactEmail'))]),
    avatarUrl: z.union([z.literal(''), z.string().url(t('validation.absoluteImageUrl'))]),
  })

type FieldName = keyof z.infer<ReturnType<typeof makeSchema>>

/** Edits the single profile, including its ordered social links. */
export function ContentProfilePage() {
  const queryClient = useQueryClient()
  const { t } = useI18n()

  const profile = useQuery({
    queryKey: ['manage-profile'],
    queryFn: ({ signal }) => contentAdminApi.profile(signal),
  })

  const save = useMutation({
    mutationFn: (form: ProfileFormValues) => contentAdminApi.saveProfile(toProfileRequest(form)),
    meta: { handlesErrors: true },
    onSuccess: async () => {
      notify.success(t('profile.saved'))
      await queryClient.invalidateQueries({ queryKey: ['manage-profile'] })
    },
    onError: (failure) => reportFormError(failure, t('profile.saveFailed')),
  })

  if (profile.isError) {
    return (
      <ErrorPanel
        message={describeError(profile.error)}
        traceId={traceIdOf(profile.error)}
        onRetry={() => void profile.refetch()}
      />
    )
  }

  if (profile.isPending) {
    return <LoadingState />
  }

  return (
    <ProfileEditor
      initial={profileToForm(profile.data)}
      onSubmit={(form) => save.mutate(form)}
      isSaving={save.isPending}
      error={save.error}
      created={profile.data !== null}
    />
  )
}

function ProfileEditor({
  initial,
  onSubmit,
  isSaving,
  error,
  created,
}: {
  initial: ProfileFormValues
  onSubmit: (form: ProfileFormValues) => void
  isSaving: boolean
  error: unknown
  created: boolean
}) {
  const { t } = useI18n()
  const schema = useMemo(() => makeSchema(t), [t])
  const [form, setForm] = useState(initial)
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<FieldName, string>>>({})

  function submit(event: FormEvent) {
    event.preventDefault()

    const parsed = schema.safeParse(form)

    if (!parsed.success) {
      setFieldErrors(zodFieldErrors<FieldName>(parsed.error))

      return
    }

    setFieldErrors({})
    onSubmit(form)
  }

  const serverErrors = serverFieldErrors(error)
  const errorFor = (name: FieldName) => fieldErrors[name] ?? serverErrors[name]?.[0]

  const updateLink = (index: number, patch: Partial<ProfileFormValues['socialLinks'][number]>) =>
    setForm({
      ...form,
      socialLinks: form.socialLinks.map((entry, position) => (position === index ? { ...entry, ...patch } : entry)),
    })

  return (
    <>
      <PageHeader
        title={t('profile.title')}
        description={created ? t('profile.exists') : t('profile.missing')}
      />

      <form onSubmit={submit} noValidate className="grid gap-6">
        <Section title={t('profile.identity')}>
          <div className="grid items-start gap-5 sm:grid-cols-2">
            <FormField label={t('common.displayName')} htmlFor="displayName" error={errorFor('displayName')}>
              <Input required value={form.displayName} onChange={(e) => setForm({ ...form, displayName: e.target.value })} />
            </FormField>

            <FormField label={t('profile.headline')} htmlFor="headline" error={errorFor('headline')}>
              <Input value={form.headline} onChange={(e) => setForm({ ...form, headline: e.target.value })} />
            </FormField>

            <FormField label={t('profile.location')} htmlFor="location" error={errorFor('location')}>
              <Input value={form.location} onChange={(e) => setForm({ ...form, location: e.target.value })} />
            </FormField>

            <FormField
              label={t('profile.contactEmail')}
              htmlFor="contactEmail"
              error={errorFor('email')}
              hint={t('profile.contactEmailHint')}
            >
              <Input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
            </FormField>

            <FormField label={t('profile.avatarUrl')} htmlFor="avatarUrl" error={errorFor('avatarUrl')} className="sm:col-span-2">
              <Input type="url" value={form.avatarUrl} onChange={(e) => setForm({ ...form, avatarUrl: e.target.value })} />
            </FormField>

            <FormField label={t('profile.summary')} htmlFor="summary" error={errorFor('summary')} className="sm:col-span-2">
              <Textarea rows={6} value={form.summary} onChange={(e) => setForm({ ...form, summary: e.target.value })} />
            </FormField>
          </div>
        </Section>

        <Section
          title={t('profile.links.title')}
          description={t('profile.links.description')}
          actions={
            <Button
              variant="outline"
              size="sm"
              onClick={() =>
                setForm({ ...form, socialLinks: [...form.socialLinks, { platform: '', url: '', label: '' }] })
              }
            >
              <Plus /> {t('profile.links.add')}
            </Button>
          }
        >
          {form.socialLinks.length === 0 ? (
            <p className="text-muted-foreground text-sm">{t('profile.links.none')}</p>
          ) : (
            <ul className="grid gap-4">
              {form.socialLinks.map((link, index) => (
                <li key={index} className="grid items-end gap-3 sm:grid-cols-[1fr_2fr_1fr_auto]">
                  <FormField label={t('profile.links.platform')} htmlFor={`platform-${index}`}>
                    <Input value={link.platform} onChange={(e) => updateLink(index, { platform: e.target.value })} />
                  </FormField>

                  <FormField label={t('profile.links.url')} htmlFor={`url-${index}`}>
                    <Input type="url" value={link.url} onChange={(e) => updateLink(index, { url: e.target.value })} />
                  </FormField>

                  <FormField label={t('profile.links.label')} htmlFor={`label-${index}`}>
                    <Input value={link.label} onChange={(e) => updateLink(index, { label: e.target.value })} />
                  </FormField>

                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={t('profile.links.remove', { n: index + 1 })}
                    onClick={() =>
                      setForm({ ...form, socialLinks: form.socialLinks.filter((_, position) => position !== index) })
                    }
                  >
                    <Trash2 />
                  </Button>
                </li>
              ))}
            </ul>
          )}
        </Section>

        <div>
          <Button type="submit" disabled={isSaving}>
            {isSaving && <Loader2 className="animate-spin" />}
            {isSaving ? t('common.saving') : t('profile.submit')}
          </Button>
        </div>
      </form>
    </>
  )
}
