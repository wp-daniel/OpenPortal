import { useMutation } from '@tanstack/react-query'
import { BadgeCheck, Copy, Globe, KeySquare, Loader2, Plus, ShieldCheck, Trash2 } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import type { OrganizationTenancyApi } from '@/api/organizations'
import type { OrganizationDetail, OrganizationDomain, OrganizationMember, OrganizationRole } from '@/api/types'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { Switch } from '@/components/ui/switch'
import { notify } from '@/hooks/useToast'
import { formatDate } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { reportFormError } from '@/lib/forms'

/**
 * The email domains an organization claims: add one, publish its TXT record and verify it, then let people with a
 * confirmed address there join by themselves. A portal administrator may vouch for a domain instead (`canVouch`).
 */
export function OrganizationDomains({
  organization,
  tenancy,
  canVouch,
  onChanged,
}: {
  organization: OrganizationDetail
  tenancy: OrganizationTenancyApi
  canVouch: boolean
  onChanged: (organization: OrganizationDetail) => void
}) {
  const { t } = useI18n()
  const [domain, setDomain] = useState('')

  const add = useMutation({
    mutationFn: () => tenancy.addDomain(organization.id, domain.trim()),
    meta: { handlesErrors: true },
    onSuccess: (saved) => {
      setDomain('')
      onChanged(saved)
    },
    onError: (failure) => reportFormError(failure, t('organizations.domains.addFailed')),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    if (domain.trim()) add.mutate()
  }

  return (
    <section className="grid gap-3">
      <p className="text-muted-foreground text-xs">{t('organizations.domains.hint')}</p>

      <form onSubmit={submit} noValidate className="flex gap-2">
        <Input placeholder="acme.com" className="font-mono" value={domain} onChange={(event) => setDomain(event.target.value)} />
        <Button type="submit" disabled={!domain.trim() || add.isPending}>
          {add.isPending ? <Loader2 className="animate-spin" /> : <Plus />}
          {t('organizations.domains.add')}
        </Button>
      </form>

      {organization.domains.length === 0 ? (
        <p className="text-muted-foreground text-sm">{t('organizations.domains.none')}</p>
      ) : (
        <ul className="divide-y rounded-md border">
          {organization.domains.map((entry) => (
            <DomainRow key={entry.id} organization={organization} domain={entry} tenancy={tenancy} canVouch={canVouch} onChanged={onChanged} />
          ))}
        </ul>
      )}
    </section>
  )
}

function DomainRow({
  organization,
  domain,
  tenancy,
  canVouch,
  onChanged,
}: {
  organization: OrganizationDetail
  domain: OrganizationDomain
  tenancy: OrganizationTenancyApi
  canVouch: boolean
  onChanged: (organization: OrganizationDetail) => void
}) {
  const { t } = useI18n()

  const verify = useMutation({
    mutationFn: (manual: boolean) => tenancy.verifyDomain(organization.id, domain.id, manual),
    meta: { handlesErrors: true },
    onSuccess: (saved) => {
      notify.success(t('organizations.domains.verified', { domain: domain.domain }))
      onChanged(saved)
    },
    onError: (failure) => reportFormError(failure, t('organizations.domains.verifyFailed')),
  })

  const update = useMutation({
    mutationFn: ({ autoJoin, role }: { autoJoin: boolean; role: OrganizationRole }) =>
      tenancy.updateDomain(organization.id, domain.id, autoJoin, role),
    onSuccess: onChanged,
  })

  const remove = useMutation({
    mutationFn: () => tenancy.removeDomain(organization.id, domain.id),
    onSuccess: onChanged,
  })

  async function copyRecord() {
    try {
      await navigator.clipboard.writeText(domain.recordValue)
      notify.success(t('common.copied'))
    } catch {
      notify.error(t('common.copyFailed'))
    }
  }

  return (
    <li className="grid gap-3 p-3">
      <div className="flex flex-wrap items-center gap-2">
        <Globe className="text-muted-foreground size-4" />
        <span className="font-mono text-sm font-medium">{domain.domain}</span>
        {domain.verified ? (
          <Badge variant="success">
            <BadgeCheck />
            {t('organizations.domains.verifiedBadge')}
          </Badge>
        ) : (
          <Badge variant="warning">{t('organizations.domains.unverified')}</Badge>
        )}
        <Button
          className="ml-auto"
          variant="ghost"
          size="sm"
          aria-label={t('common.delete')}
          disabled={remove.isPending}
          onClick={() => remove.mutate()}
        >
          <Trash2 />
        </Button>
      </div>

      {domain.verified ? (
        <div className="flex flex-wrap items-center gap-3">
          <Switch
            id={`autojoin-${domain.id}`}
            checked={domain.autoJoin}
            disabled={update.isPending}
            onCheckedChange={(checked) => update.mutate({ autoJoin: checked, role: domain.autoJoinRole })}
          />
          <Label htmlFor={`autojoin-${domain.id}`} className="font-normal">
            {t('organizations.domains.autoJoin')}
          </Label>
          {domain.autoJoin && (
            <Button
              variant="outline"
              size="sm"
              disabled={update.isPending}
              onClick={() => update.mutate({ autoJoin: true, role: domain.autoJoinRole === 'admin' ? 'member' : 'admin' })}
            >
              {t('organizations.domains.as', { role: t(`organizations.roleName.${domain.autoJoinRole}`) })}
            </Button>
          )}
          {domain.verifiedAtUtc && (
            <span className="text-muted-foreground text-xs">
              {t(`organizations.domains.verifiedBy.${domain.verifiedBy ?? 'dns'}`, {
                date: formatDate(domain.verifiedAtUtc, { dateStyle: 'medium' }),
              })}
            </span>
          )}
        </div>
      ) : (
        <div className="grid gap-2">
          <p className="text-muted-foreground text-xs">{t('organizations.domains.publish', { domain: domain.domain })}</p>
          <div className="flex gap-2">
            <Input readOnly value={domain.recordValue} className="font-mono text-xs" onFocus={(event) => event.target.select()} />
            <Button variant="outline" size="icon" onClick={() => void copyRecord()} aria-label={t('common.copy')}>
              <Copy />
            </Button>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button size="sm" disabled={verify.isPending} onClick={() => verify.mutate(false)}>
              {verify.isPending && !verify.variables ? <Loader2 className="animate-spin" /> : <ShieldCheck />}
              {t('organizations.domains.check')}
            </Button>
            {canVouch && (
              <Button size="sm" variant="outline" disabled={verify.isPending} onClick={() => verify.mutate(true)}>
                {t('organizations.domains.vouch')}
              </Button>
            )}
          </div>
        </div>
      )}
    </li>
  )
}

/**
 * The roles a member holds in the organization's applications, within this organization only (they add to what the
 * member's grants give). A popover lists each application with its roles as check boxes; each change applies at once.
 */
export function MemberApplicationRoles({
  organization,
  member,
  tenancy,
  onChanged,
}: {
  organization: OrganizationDetail
  member: OrganizationMember
  tenancy: OrganizationTenancyApi
  onChanged: (organization: OrganizationDetail) => void
}) {
  const { t } = useI18n()

  const save = useMutation({
    mutationFn: ({ applicationId, roles }: { applicationId: string; roles: readonly string[] }) =>
      tenancy.setMemberApplicationRoles(organization.id, member.id, applicationId, roles),
    onSuccess: onChanged,
  })

  if (organization.applications.length === 0) {
    return null
  }

  const count = member.applicationRoles.reduce((total, entry) => total + entry.roles.length, 0)

  return (
    <Popover>
      <PopoverTrigger asChild>
        <Button variant="ghost" size="sm" aria-label={t('organizations.appRoles.open', { name: member.displayName })}>
          <KeySquare />
          {count > 0 && <span className="tabular-nums">{count}</span>}
        </Button>
      </PopoverTrigger>
      <PopoverContent align="end" className="grid w-72 gap-3">
        <div className="grid gap-1">
          <p className="text-sm font-medium">{t('organizations.appRoles.title')}</p>
          <p className="text-muted-foreground text-xs">{t('organizations.appRoles.hint')}</p>
        </div>
        {organization.applications.map((application) => {
          const held = member.applicationRoles.find((entry) => entry.applicationId === application.id)?.roles ?? []

          return (
            <fieldset key={application.id} className="grid gap-2">
              <legend className="mb-1 text-xs font-medium">{application.displayName}</legend>
              {application.roles.map((role) => {
                const id = `role-${member.id}-${application.id}-${role.key}`

                return (
                  <div key={role.key} className="flex items-center gap-2">
                    <Checkbox
                      id={id}
                      checked={held.includes(role.key)}
                      disabled={save.isPending}
                      onCheckedChange={(checked) =>
                        save.mutate({
                          applicationId: application.id,
                          roles: checked === true ? [...held, role.key] : held.filter((key) => key !== role.key),
                        })
                      }
                    />
                    <Label htmlFor={id} className="font-normal">
                      {role.displayName ?? role.key}
                    </Label>
                  </div>
                )
              })}
            </fieldset>
          )
        })}
      </PopoverContent>
    </Popover>
  )
}
