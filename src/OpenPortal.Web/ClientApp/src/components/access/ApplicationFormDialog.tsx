import { useMutation } from '@tanstack/react-query'
import { Loader2, Plus, Trash2 } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { z } from 'zod'
import { applicationsApi } from '@/api/access'
import type { ApplicationSecret, ApplicationSummary, GroupClaimMode } from '@/api/types'
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
import { Label } from '@/components/ui/label'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { Separator } from '@/components/ui/separator'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Textarea } from '@/components/ui/textarea'
import { notify } from '@/hooks/useToast'
import type { TFunction } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { reportFormError, zodFieldErrors } from '@/lib/forms'
import { linesToList } from '@/lib/access'

type Field = 'clientId' | 'displayName' | 'description' | 'baseUrl' | 'redirectUris' | 'postLogoutRedirectUris' | 'roles'

type FormTab = 'general' | 'roles'

/** A row of the roles editor. A role that is already saved keeps its key: the application checks that key. */
interface RoleRow {
  readonly id: number
  readonly key: string
  readonly displayName: string
  readonly description: string
  readonly saved: boolean
}

const GROUP_CLAIM_MODES: readonly GroupClaimMode[] = ['none', 'granted', 'all']

const ROLE_KEY = /^[a-z0-9][a-z0-9._:-]*$/

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

const roleList = (t: TFunction) =>
  z
    .array(z.object({ key: z.string(), displayName: z.string(), description: z.string() }))
    .max(50, t('applications.roles.tooMany', { max: 50 }))
    .refine((roles) => roles.every((role) => ROLE_KEY.test(role.key.trim()) && role.key.trim().length <= 64), t('applications.roles.keyInvalid'))
    .refine((roles) => roles.every((role) => role.displayName.trim().length <= 80), t('applications.roles.nameMax', { max: 80 }))
    .refine((roles) => roles.every((role) => role.description.trim().length <= 300), t('applications.roles.descriptionMax', { max: 300 }))
    .refine((roles) => new Set(roles.map((role) => role.key.trim())).size === roles.length, t('applications.roles.keyDuplicate'))

/**
 * Create or edit an application: its address and callbacks on the first tab, the roles it understands and
 * the groups it may see on the second. Validation mirrors the server's rules for a quick answer; the server
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
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
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

  const [tab, setTab] = useState<FormTab>('general')
  const [clientId, setClientId] = useState(application?.clientId ?? '')
  const [displayName, setDisplayName] = useState(application?.displayName ?? '')
  const [description, setDescription] = useState(application?.description ?? '')
  const [baseUrl, setBaseUrl] = useState(application?.baseUrl ?? '')
  const [redirectUris, setRedirectUris] = useState(application?.redirectUris.join('\n') ?? '')
  const [postLogoutRedirectUris, setPostLogoutRedirectUris] = useState(application?.postLogoutRedirectUris.join('\n') ?? '')
  const [roles, setRoles] = useState<RoleRow[]>(
    () =>
      application?.roles.map((role, index) => ({
        id: index,
        key: role.key,
        displayName: role.displayName ?? '',
        description: role.description ?? '',
        saved: true,
      })) ?? [],
  )
  const [nextRowId, setNextRowId] = useState(application?.roles.length ?? 0)
  const [groupClaims, setGroupClaims] = useState<GroupClaimMode>(application?.groupClaims ?? 'none')
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
        roles: roleList(t),
      }),
    [t],
  )

  const body = () => ({
    displayName: displayName.trim(),
    description: description.trim() === '' ? null : description.trim(),
    baseUrl: baseUrl.trim(),
    redirectUris: linesToList(redirectUris),
    postLogoutRedirectUris: linesToList(postLogoutRedirectUris),
    roles: roles.map((role) => ({
      key: role.key.trim(),
      displayName: role.displayName.trim() === '' ? null : role.displayName.trim(),
      description: role.description.trim() === '' ? null : role.description.trim(),
    })),
    groupClaims,
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

  function updateRole(id: number, change: Partial<Pick<RoleRow, 'key' | 'displayName' | 'description'>>) {
    setRoles((current) => current.map((role) => (role.id === id ? { ...role, ...change } : role)))
  }

  function addRole() {
    setRoles((current) => [...current, { id: nextRowId, key: '', displayName: '', description: '', saved: false }])
    setNextRowId((id) => id + 1)
  }

  function submit(event: FormEvent) {
    event.preventDefault()

    const parsed = schema.safeParse({
      clientId: isEdit ? 'unchanged' : clientId,
      displayName,
      description,
      baseUrl,
      redirectUris,
      postLogoutRedirectUris,
      roles,
    })

    if (!parsed.success) {
      const found = zodFieldErrors<Field>(parsed.error)
      setErrors(found)

      // The roles are on their own tab: show it when they are the only thing wrong.
      setTab(Object.keys(found).some((field) => field !== 'roles') ? 'general' : 'roles')

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

      <form id="application-form" onSubmit={submit} noValidate>
        <Tabs value={tab} onValueChange={(value) => setTab(value as FormTab)}>
          <TabsList variant="line" className="max-w-full flex-wrap justify-start group-data-[orientation=horizontal]/tabs:h-auto">
            <TabsTrigger value="general">{t('applications.tab.general')}</TabsTrigger>
            <TabsTrigger value="roles">{t('applications.tab.roles')}</TabsTrigger>
          </TabsList>
          <Separator className="-mt-2" />

          <TabsContent value="general" className="grid items-start gap-5 pt-4">
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
          </TabsContent>

          <TabsContent value="roles" className="grid items-start gap-6 pt-4">
            <fieldset className="grid gap-3">
              <legend className="mb-1 text-sm font-medium">{t('applications.roles.title')}</legend>
              <p className="text-muted-foreground text-xs">{t('applications.roles.hint')}</p>

              {roles.length === 0 && <p className="text-muted-foreground text-sm">{t('applications.roles.empty')}</p>}

              {roles.length > 0 && (
                <ul className="grid gap-2">
                  {roles.map((role) => (
                    <li key={role.id} className="grid items-start gap-2 rounded-md border p-2 sm:grid-cols-[10rem_1fr_auto]">
                      <Input
                        aria-label={t('applications.roles.key')}
                        placeholder={t('applications.roles.key')}
                        className="font-mono text-xs"
                        value={role.key}
                        disabled={role.saved}
                        // A row just added is where the administrator types next.
                        autoFocus={!role.saved}
                        title={role.saved ? t('applications.roles.keyLocked') : undefined}
                        autoComplete="off"
                        onChange={(event) => updateRole(role.id, { key: event.target.value.toLowerCase() })}
                      />
                      <div className="grid gap-2">
                        <Input
                          aria-label={t('applications.roles.name')}
                          placeholder={t('applications.roles.name')}
                          value={role.displayName}
                          onChange={(event) => updateRole(role.id, { displayName: event.target.value })}
                        />
                        <Input
                          aria-label={t('applications.roles.description')}
                          placeholder={t('applications.roles.description')}
                          value={role.description}
                          onChange={(event) => updateRole(role.id, { description: event.target.value })}
                        />
                      </div>
                      <Button
                        variant="ghost"
                        size="sm"
                        aria-label={t('applications.roles.remove')}
                        title={t('applications.roles.remove')}
                        onClick={() => setRoles((current) => current.filter((entry) => entry.id !== role.id))}
                      >
                        <Trash2 />
                      </Button>
                    </li>
                  ))}
                </ul>
              )}

              {errors.roles && (
                <p role="alert" className="text-destructive text-sm">
                  {errors.roles}
                </p>
              )}

              <div>
                <Button variant="outline" size="sm" onClick={addRole}>
                  <Plus />
                  {t('applications.roles.add')}
                </Button>
              </div>

              {isEdit && roles.some((role) => role.saved) && (
                <p className="text-muted-foreground text-xs">{t('applications.roles.removeHint')}</p>
              )}
            </fieldset>

            <Separator />

            <fieldset className="grid gap-3">
              <legend className="mb-1 text-sm font-medium">{t('applications.groupClaims.title')}</legend>
              <p className="text-muted-foreground text-xs">{t('applications.groupClaims.hint')}</p>
              <RadioGroup value={groupClaims} onValueChange={(value) => setGroupClaims(value as GroupClaimMode)} className="gap-2">
                {GROUP_CLAIM_MODES.map((mode) => (
                  <div key={mode} className="flex items-start gap-3">
                    <RadioGroupItem value={mode} id={`app-group-claims-${mode}`} className="mt-0.5" />
                    <div className="grid gap-0.5">
                      <Label htmlFor={`app-group-claims-${mode}`}>{t(`applications.groupClaims.${mode}`)}</Label>
                      <p className="text-muted-foreground text-xs">{t(`applications.groupClaims.${mode}Hint`)}</p>
                    </div>
                  </div>
                ))}
              </RadioGroup>
            </fieldset>
          </TabsContent>
        </Tabs>
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
