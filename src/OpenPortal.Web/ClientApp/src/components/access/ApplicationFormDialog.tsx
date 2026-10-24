import { useMutation } from '@tanstack/react-query'
import { ExternalLink, Loader2, Plus, Trash2 } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { z } from 'zod'
import { applicationsApi } from '@/api/access'
import type { ApplicationApiScope, ApplicationClientType, ApplicationSecret, ApplicationSummary, GroupClaimMode, OrganizationMode } from '@/api/types'
import { FormField } from '@/components/FormField'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
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
import { Switch } from '@/components/ui/switch'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Textarea } from '@/components/ui/textarea'
import { notify } from '@/hooks/useToast'
import type { TFunction } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { reportFormError, zodFieldErrors } from '@/lib/forms'
import { linesToList } from '@/lib/access'

type Field = 'clientId' | 'displayName' | 'description' | 'baseUrl' | 'redirectUris' | 'postLogoutRedirectUris' | 'roles'

type FormTab = 'general' | 'roles' | 'access' | 'api' | 'webhook'

/** The events an application can subscribe to (WebhookEventTypes on the server). */
const WEBHOOK_EVENTS = [
  'user.updated',
  'user.deleted',
  'access.granted',
  'access.revoked',
  'access.roles_changed',
  'group.member_added',
  'group.member_removed',
  'organization.member_added',
  'organization.member_removed',
  'organization.member_role_changed',
] as const

const ORGANIZATION_MODES: readonly OrganizationMode[] = ['none', 'optional', 'required']

/** A row of the roles editor. A role that is already saved keeps its key: the application checks that key. */
interface RoleRow {
  readonly id: number
  readonly key: string
  readonly displayName: string
  readonly description: string
  readonly saved: boolean
}

const GROUP_CLAIM_MODES: readonly GroupClaimMode[] = ['none', 'granted', 'all']

/** In the order the server stores them; each has `applications.apiScope.<key>` and `<key>Hint` labels. */
const API_SCOPES: readonly { scope: ApplicationApiScope; key: string }[] = [
  { scope: 'portal.users.read', key: 'usersRead' },
  { scope: 'portal.access.manage', key: 'accessManage' },
  { scope: 'portal.users.create', key: 'usersCreate' },
]

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
 * the groups it may see on the second, what its own backend may do through the application API on the third.
 * Validation mirrors the server's rules for a quick answer; the server
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
  const [apiScopes, setApiScopes] = useState<ReadonlySet<ApplicationApiScope>>(() => new Set(application?.apiScopes ?? []))
  const [allowAccessRequests, setAllowAccessRequests] = useState(application?.allowAccessRequests ?? false)
  const [organizations, setOrganizations] = useState<OrganizationMode>(application?.organizations ?? 'none')
  const [clientType, setClientType] = useState<ApplicationClientType>(application?.clientType ?? 'confidential')
  const [backChannelLogoutUri, setBackChannelLogoutUri] = useState(application?.backChannelLogoutUri ?? '')
  const [webhookUrl, setWebhookUrl] = useState(application?.webhookUrl ?? '')
  const [webhookEvents, setWebhookEvents] = useState<ReadonlySet<string>>(() => new Set(application?.webhookEvents ?? []))
  const [webhookSecret, setWebhookSecret] = useState<string | null>(null)
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
    apiScopes: API_SCOPES.map((entry) => entry.scope).filter((scope) => apiScopes.has(scope)),
    allowAccessRequests,
    organizations,
    backChannelLogoutUri: backChannelLogoutUri.trim() === '' ? null : backChannelLogoutUri.trim(),
    webhookUrl: webhookUrl.trim(),
    webhookEvents: WEBHOOK_EVENTS.filter((event) => webhookEvents.has(event)),
  })

  const generateSecret = useMutation({
    mutationFn: () => applicationsApi.generateWebhookSecret(application!.id),
    onSuccess: (result) => {
      setWebhookSecret(result.secret)
      onSaved()
    },
  })

  const testWebhook = useMutation({
    mutationFn: () => applicationsApi.testWebhook(application!.id),
    onSuccess: () => notify.success(t('applications.webhook.testQueued')),
  })

  /** Creating users implies managing access (the server enforces it too): the two boxes move together. */
  function toggleApiScope(scope: ApplicationApiScope, checked: boolean) {
    setApiScopes((current) => {
      const next = new Set(current)

      if (checked) {
        next.add(scope)
        if (scope === 'portal.users.create') next.add('portal.access.manage')
      } else {
        next.delete(scope)
        if (scope === 'portal.access.manage') next.delete('portal.users.create')
      }

      return next
    })
  }

  const save = useMutation({
    mutationFn: async () => {
      if (application) {
        await applicationsApi.update(application.id, body())
        return null
      }

      return applicationsApi.create({ clientId: clientId.trim(), clientType, ...body() })
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
            <TabsTrigger value="access">{t('applications.tab.access')}</TabsTrigger>
            <TabsTrigger value="api">{t('applications.tab.api')}</TabsTrigger>
            <TabsTrigger value="webhook">{t('applications.tab.webhook')}</TabsTrigger>
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

            <FormField
              label={t('applications.form.backChannelLogoutUri')}
              htmlFor="app-back-channel"
              hint={t('applications.form.backChannelLogoutUriHint')}
            >
              <Input
                type="url"
                className="font-mono text-xs"
                placeholder="https://crm.example.com/signout-backchannel"
                value={backChannelLogoutUri}
                onChange={(event) => setBackChannelLogoutUri(event.target.value)}
              />
            </FormField>

            <fieldset className="grid gap-3">
              <legend className="mb-1 text-sm font-medium">{t('applications.clientType.title')}</legend>
              <RadioGroup
                value={clientType}
                onValueChange={(value) => setClientType(value as ApplicationClientType)}
                className="gap-2"
                disabled={isEdit}
              >
                {(['confidential', 'public'] as const).map((value) => (
                  <div key={value} className="flex items-start gap-3">
                    <RadioGroupItem value={value} id={`app-client-type-${value}`} className="mt-0.5" />
                    <div className="grid gap-0.5">
                      <Label htmlFor={`app-client-type-${value}`}>{t(`applications.clientType.${value}`)}</Label>
                      <p className="text-muted-foreground text-xs">{t(`applications.clientType.${value}Hint`)}</p>
                    </div>
                  </div>
                ))}
              </RadioGroup>
              {isEdit && <p className="text-muted-foreground text-xs">{t('applications.clientType.locked')}</p>}
            </fieldset>
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

          <TabsContent value="access" className="grid items-start gap-6 pt-4">
            <div className="flex items-start justify-between gap-6 rounded-lg border p-4">
              <div className="grid gap-1">
                <Label htmlFor="app-allow-requests">{t('applications.requests.title')}</Label>
                <p className="text-muted-foreground text-xs">{t('applications.requests.hint')}</p>
              </div>
              <Switch id="app-allow-requests" checked={allowAccessRequests} onCheckedChange={setAllowAccessRequests} />
            </div>

            <fieldset className="grid gap-3">
              <legend className="mb-1 text-sm font-medium">{t('applications.organizations.title')}</legend>
              <p className="text-muted-foreground text-xs">{t('applications.organizations.hint')}</p>
              <RadioGroup
                value={organizations}
                onValueChange={(value) => setOrganizations(value as OrganizationMode)}
                className="gap-2"
              >
                {ORGANIZATION_MODES.map((mode) => (
                  <div key={mode} className="flex items-start gap-3">
                    <RadioGroupItem value={mode} id={`app-organizations-${mode}`} className="mt-0.5" />
                    <div className="grid gap-0.5">
                      <Label htmlFor={`app-organizations-${mode}`}>{t(`applications.organizations.${mode}`)}</Label>
                      <p className="text-muted-foreground text-xs">{t(`applications.organizations.${mode}Hint`)}</p>
                    </div>
                  </div>
                ))}
              </RadioGroup>
              {organizations !== 'none' && (
                <p className="text-muted-foreground text-xs">
                  {t('applications.organizations.claims')}{' '}
                  <code className="text-foreground">org_id</code>, <code className="text-foreground">org_slug</code>,{' '}
                  <code className="text-foreground">org_name</code>, <code className="text-foreground">org_role</code>
                </p>
              )}
            </fieldset>
          </TabsContent>

          <TabsContent value="api" className="grid items-start gap-4 pt-4">
            <fieldset className="grid gap-3">
              <legend className="mb-1 text-sm font-medium">{t('applications.api.title')}</legend>
              <p className="text-muted-foreground text-xs">{t('applications.api.hint')}</p>
              {clientType === 'public' && <p className="text-muted-foreground text-xs">{t('applications.api.publicClient')}</p>}

              {API_SCOPES.map(({ scope, key }) => (
                <div key={scope} className="flex items-start gap-3">
                  <Checkbox
                    id={`app-api-${key}`}
                    className="mt-0.5"
                    checked={apiScopes.has(scope)}
                    disabled={clientType === 'public'}
                    onCheckedChange={(checked) => toggleApiScope(scope, checked === true)}
                  />
                  <div className="grid gap-0.5">
                    <Label htmlFor={`app-api-${key}`}>
                      {t(`applications.apiScope.${key}`)} <code className="text-muted-foreground text-xs font-normal">{scope}</code>
                    </Label>
                    <p className="text-muted-foreground text-xs">{t(`applications.apiScope.${key}Hint`)}</p>
                  </div>
                </div>
              ))}
            </fieldset>

            <p className="text-muted-foreground text-xs">
              {t('applications.api.howTo')}{' '}
              <a href="/scalar" target="_blank" rel="noreferrer" className="text-foreground inline-flex items-center gap-1 underline underline-offset-2">
                {t('applications.api.reference')}
                <ExternalLink className="size-3" />
              </a>
            </p>
          </TabsContent>

          <TabsContent value="webhook" className="grid items-start gap-5 pt-4">
            <p className="text-muted-foreground text-xs">{t('applications.webhook.hint')}</p>

            <FormField label={t('applications.webhook.url')} htmlFor="app-webhook-url" hint={t('applications.webhook.urlHint')}>
              <Input
                type="url"
                className="font-mono text-xs"
                placeholder="https://crm.example.com/hooks/openportal"
                value={webhookUrl}
                onChange={(event) => setWebhookUrl(event.target.value)}
              />
            </FormField>

            <fieldset className="grid gap-2">
              <legend className="mb-1 text-sm font-medium">{t('applications.webhook.events')}</legend>
              <div className="grid gap-2 sm:grid-cols-2">
                {WEBHOOK_EVENTS.map((event) => (
                  <div key={event} className="flex items-center gap-2">
                    <Checkbox
                      id={`app-webhook-${event}`}
                      checked={webhookEvents.has(event)}
                      onCheckedChange={(checked) =>
                        setWebhookEvents((current) => {
                          const next = new Set(current)
                          if (checked === true) next.add(event)
                          else next.delete(event)
                          return next
                        })
                      }
                    />
                    <Label htmlFor={`app-webhook-${event}`} className="font-mono text-xs font-normal">
                      {event}
                    </Label>
                  </div>
                ))}
              </div>
            </fieldset>

            {isEdit ? (
              <div className="grid gap-3 rounded-lg border p-4">
                <p className="text-sm">
                  {application.hasWebhookSecret ? t('applications.webhook.secretSet') : t('applications.webhook.secretMissing')}
                </p>
                {webhookSecret && (
                  <div className="grid gap-1">
                    <p className="text-muted-foreground text-xs">{t('applications.webhook.secretOnce')}</p>
                    <Input readOnly value={webhookSecret} className="font-mono text-xs" onFocus={(event) => event.target.select()} />
                  </div>
                )}
                <div className="flex flex-wrap gap-2">
                  <Button variant="outline" size="sm" onClick={() => generateSecret.mutate()} disabled={generateSecret.isPending}>
                    {generateSecret.isPending && <Loader2 className="animate-spin" />}
                    {application.hasWebhookSecret ? t('applications.webhook.rotate') : t('applications.webhook.generate')}
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => testWebhook.mutate()}
                    disabled={testWebhook.isPending || !application.webhookUrl || !(application.hasWebhookSecret || webhookSecret)}
                  >
                    {t('applications.webhook.test')}
                  </Button>
                </div>
              </div>
            ) : (
              <p className="text-muted-foreground text-xs">{t('applications.webhook.afterCreate')}</p>
            )}
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
