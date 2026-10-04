import { useMutation } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { z } from 'zod'
import { applicationsApi } from '@/api/access'
import type { ApplicationSecret, ApplicationSummary } from '@/api/types'
import { FormField } from '@/components/FormField'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { notify } from '@/hooks/useToast'
import type { TFunction } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { reportFormError, zodFieldErrors } from '@/lib/forms'
import { linesToList } from '@/lib/access'

type Field = 'clientId' | 'displayName' | 'description' | 'baseUrl' | 'redirectUris' | 'postLogoutRedirectUris'

const uriList = (t: TFunction, required: boolean) =>
  z
    .string()
    .refine((value) => !required || linesToList(value).length > 0, t('applications.form.redirectRequired'))
    .refine(
      (value) =>
        linesToList(value).every((line) => {
          try {
            const url = new URL(line)
            const loopback = ['localhost', '127.0.0.1', '[::1]'].includes(url.hostname)
            return url.protocol === 'https:' || (url.protocol === 'http:' && loopback)
          } catch {
            return false
          }
        }),
      t('applications.form.redirectInvalid'),
    )

/**
 * Create or edit an application. Validation mirrors the server's rules for a quick answer; the server
 * remains the authority and its errors are reported the usual way.
 */
export function ApplicationFormDialog({
  open,
  application,
  onClose,
  onSaved,
  onCreated,
}: {
  open: boolean
  /** The application being edited, or null to create one. */
  application: ApplicationSummary | null
  onClose: () => void
  onSaved: () => void
  onCreated: (secret: ApplicationSecret) => void
}) {
  // The dialog content unmounts while closed, so the form starts from fresh state on every opening.
  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent className="sm:max-w-xl">
        <ApplicationForm application={application} onClose={onClose} onSaved={onSaved} onCreated={onCreated} />
      </DialogContent>
    </Dialog>
  )
}

function ApplicationForm({
  application,
  onClose,
  onSaved,
  onCreated,
}: {
  application: ApplicationSummary | null
  onClose: () => void
  onSaved: () => void
  onCreated: (secret: ApplicationSecret) => void
}) {
  const { t } = useI18n()
  const isEdit = application !== null

  const [clientId, setClientId] = useState(application?.clientId ?? '')
  const [displayName, setDisplayName] = useState(application?.displayName ?? '')
  const [description, setDescription] = useState(application?.description ?? '')
  const [baseUrl, setBaseUrl] = useState(application?.baseUrl ?? '')
  const [redirectUris, setRedirectUris] = useState(application?.redirectUris.join('\n') ?? '')
  const [postLogoutRedirectUris, setPostLogoutRedirectUris] = useState(application?.postLogoutRedirectUris.join('\n') ?? '')
  const [errors, setErrors] = useState<Partial<Record<Field, string>>>({})

  const schema = useMemo(
    () =>
      z.object({
        clientId: z
          .string()
          .trim()
          .min(1, t('applications.form.clientIdRequired'))
          .max(64, t('applications.form.clientIdMax', { max: 64 }))
          .regex(/^[A-Za-z0-9_-]+$/, t('applications.form.clientIdInvalid')),
        displayName: z.string().trim().min(1, t('applications.form.nameRequired')).max(120, t('applications.form.nameMax', { max: 120 })),
        description: z.string().trim().max(500, t('applications.form.descriptionMax', { max: 500 })),
        baseUrl: z.string().trim().url(t('applications.form.baseUrlInvalid')),
        redirectUris: uriList(t, true),
        postLogoutRedirectUris: uriList(t, false),
      }),
    [t],
  )

  const body = () => ({
    displayName: displayName.trim(),
    description: description.trim() === '' ? null : description.trim(),
    baseUrl: baseUrl.trim(),
    redirectUris: linesToList(redirectUris),
    postLogoutRedirectUris: linesToList(postLogoutRedirectUris),
  })

  const save = useMutation({
    mutationFn: async () => {
      if (application) {
        await applicationsApi.update(application.id, body())
        return null
      }

      return applicationsApi.create({ clientId: clientId.trim(), ...body() })
    },
    meta: { handlesErrors: true },
    onSuccess: (created) => {
      onSaved()
      onClose()

      if (created) {
        onCreated(created)
      } else {
        notify.success(t('applications.saved'), displayName.trim())
      }
    },
    onError: (failure) => reportFormError(failure, t('applications.saveFailed')),
  })

  function submit(event: FormEvent) {
    event.preventDefault()

    const parsed = schema.safeParse({
      clientId: isEdit ? 'unchanged' : clientId,
      displayName,
      description,
      baseUrl,
      redirectUris,
      postLogoutRedirectUris,
    })

    if (!parsed.success) {
      setErrors(zodFieldErrors<Field>(parsed.error))
      return
    }

    setErrors({})
    save.mutate()
  }

  return (
    <>
        <DialogHeader>
          <DialogTitle>{isEdit ? t('applications.form.editTitle') : t('applications.form.createTitle')}</DialogTitle>
          <DialogDescription>{isEdit ? t('applications.form.editDescription') : t('applications.form.createDescription')}</DialogDescription>
        </DialogHeader>

        <form id="application-form" onSubmit={submit} noValidate className="grid items-start gap-5">
          <div className="grid items-start gap-5 sm:grid-cols-2">
            <FormField
              label={t('applications.form.clientId')}
              htmlFor="app-client-id"
              hint={isEdit ? t('applications.form.clientIdLocked') : t('applications.form.clientIdHint')}
              error={errors.clientId}
            >
              <Input
                value={clientId}
                disabled={isEdit}
                className="font-mono"
                autoComplete="off"
                onChange={(event) => setClientId(event.target.value)}
              />
            </FormField>

            <FormField label={t('applications.form.name')} htmlFor="app-name" error={errors.displayName}>
              <Input value={displayName} onChange={(event) => setDisplayName(event.target.value)} />
            </FormField>
          </div>

          <FormField label={t('applications.form.description')} htmlFor="app-description" error={errors.description}>
            <Input value={description} onChange={(event) => setDescription(event.target.value)} />
          </FormField>

          <FormField label={t('applications.form.baseUrl')} htmlFor="app-base-url" hint={t('applications.form.baseUrlHint')} error={errors.baseUrl}>
            <Input type="url" placeholder="https://crm.example.com" value={baseUrl} onChange={(event) => setBaseUrl(event.target.value)} />
          </FormField>

          <FormField
            label={t('applications.form.redirectUris')}
            htmlFor="app-redirect-uris"
            hint={t('applications.form.redirectUrisHint')}
            error={errors.redirectUris}
          >
            <Textarea
              rows={2}
              className="font-mono text-xs"
              placeholder="https://crm.example.com/signin-oidc"
              value={redirectUris}
              onChange={(event) => setRedirectUris(event.target.value)}
            />
          </FormField>

          <FormField
            label={t('applications.form.postLogoutRedirectUris')}
            htmlFor="app-post-logout-uris"
            hint={t('applications.form.postLogoutRedirectUrisHint')}
            error={errors.postLogoutRedirectUris}
          >
            <Textarea
              rows={2}
              className="font-mono text-xs"
              placeholder="https://crm.example.com/signout-callback-oidc"
              value={postLogoutRedirectUris}
              onChange={(event) => setPostLogoutRedirectUris(event.target.value)}
            />
          </FormField>
        </form>

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            {t('common.cancel')}
          </Button>
          <Button type="submit" form="application-form" disabled={save.isPending}>
            {save.isPending && <Loader2 className="animate-spin" />}
            {isEdit ? t('common.save') : t('applications.form.create')}
          </Button>
        </DialogFooter>
    </>
  )
}
