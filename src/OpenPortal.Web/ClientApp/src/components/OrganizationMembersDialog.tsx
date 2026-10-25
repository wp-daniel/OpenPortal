import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2, UserPlus, X } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { myOrganizationsApi } from '@/api/organizations'
import type { OrganizationDetail, OrganizationRole } from '@/api/types'
import { MemberApplicationRoles, OrganizationDomains } from '@/components/organizations/OrganizationTenancy'
import { ErrorPanel, LoadingState } from '@/components/StatePanels'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { notify } from '@/hooks/useToast'
import { useSession } from '@/hooks/useSession'
import { useI18n } from '@/i18n/useI18n'
import { describeError } from '@/lib/errors'
import { reportFormError } from '@/lib/forms'

/**
 * Delegated management: an organization's own administrator brings people in by address (an existing account is added,
 * a new address invited), makes them administrators or members, gives them roles in the organization's applications,
 * claims the organization's email domains, and removes members, without any portal page. The organization always
 * keeps one administrator.
 */
export function OrganizationMembersDialog({ organizationId, onClose }: { organizationId: string | null; onClose: () => void }) {
  return (
    <Dialog open={organizationId !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
        {organizationId && <MembersEditor organizationId={organizationId} />}
      </DialogContent>
    </Dialog>
  )
}

function MembersEditor({ organizationId }: { organizationId: string }) {
  const { t } = useI18n()
  const { user } = useSession()
  const queryClient = useQueryClient()
  const key = ['my-organization', organizationId] as const
  const [email, setEmail] = useState('')

  const organization = useQuery({ queryKey: key, queryFn: ({ signal }) => myOrganizationsApi.get(organizationId, signal) })

  const applied = (data: OrganizationDetail) => queryClient.setQueryData(key, data)

  const invite = useMutation({
    mutationFn: () => myOrganizationsApi.invite(organizationId, { email: email.trim(), role: 'member' }),
    meta: { handlesErrors: true },
    onSuccess: async (result) => {
      setEmail('')

      if (result.organization) {
        applied(result.organization)
        notify.success(t('organizations.manage.added'))
      } else {
        notify.success(
          t('organizations.manage.invited'),
          result.invitation?.emailSent ? t('organizations.manage.invitedEmailed') : t('organizations.manage.invitedLink'),
        )
        await queryClient.invalidateQueries({ queryKey: key })
      }
    },
    onError: (failure) => reportFormError(failure, t('organizations.manage.addFailed')),
  })

  const setRole = useMutation({
    mutationFn: ({ userId, role }: { userId: string; role: OrganizationRole }) => myOrganizationsApi.setMember(organizationId, userId, role),
    onSuccess: applied,
  })

  const remove = useMutation({
    mutationFn: (userId: string) => myOrganizationsApi.removeMember(organizationId, userId),
    onSuccess: applied,
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    if (email.trim()) invite.mutate()
  }

  if (organization.isPending) return <LoadingState />
  if (organization.isError) return <ErrorPanel message={describeError(organization.error)} onRetry={() => void organization.refetch()} />

  return (
    <div className="grid gap-4">
      <DialogHeader>
        <DialogTitle>{organization.data.name}</DialogTitle>
        <DialogDescription>{t('organizations.manage.description')}</DialogDescription>
      </DialogHeader>

      <Tabs defaultValue="members">
        <TabsList variant="line">
          <TabsTrigger value="members">{t('organizations.tab.members')}</TabsTrigger>
          <TabsTrigger value="domains">{t('organizations.tab.domains')}</TabsTrigger>
        </TabsList>

        <TabsContent value="members" className="grid gap-4 pt-4">
          <form onSubmit={submit} noValidate className="flex gap-2">
            <Input
              type="email"
              placeholder={t('organizations.manage.emailPlaceholder')}
              value={email}
              onChange={(event) => setEmail(event.target.value)}
            />
            <Button type="submit" disabled={!email.trim() || invite.isPending}>
              {invite.isPending ? <Loader2 className="animate-spin" /> : <UserPlus />}
              {t('organizations.manage.invite')}
            </Button>
          </form>
          <p className="text-muted-foreground -mt-2 text-xs">{t('organizations.manage.inviteHint')}</p>

          <ul className="max-h-[50vh] divide-y overflow-y-auto rounded-md border">
            {organization.data.members.map((member) => (
              <li key={member.id} className="flex items-center gap-3 px-3 py-2">
                <div className="grid min-w-0 flex-1">
                  <span className="truncate text-sm">{member.displayName}</span>
                  <span className="text-muted-foreground truncate font-mono text-xs">{member.email}</span>
                </div>
                <MemberApplicationRoles organization={organization.data} member={member} tenancy={myOrganizationsApi} onChanged={applied} />
                <Button
                  size="sm"
                  variant="ghost"
                  disabled={setRole.isPending}
                  title={t('organizations.manage.toggleRole')}
                  onClick={() => setRole.mutate({ userId: member.id, role: member.role === 'admin' ? 'member' : 'admin' })}
                >
                  <Badge variant={member.role === 'admin' ? 'default' : 'secondary'}>{t(`organizations.roleName.${member.role}`)}</Badge>
                </Button>
                {member.id !== user?.id && (
                  <Button size="sm" variant="ghost" aria-label={t('common.delete')} disabled={remove.isPending} onClick={() => remove.mutate(member.id)}>
                    <X />
                  </Button>
                )}
              </li>
            ))}
          </ul>
        </TabsContent>

        <TabsContent value="domains" className="pt-4">
          <OrganizationDomains organization={organization.data} tenancy={myOrganizationsApi} canVouch={false} onChanged={applied} />
        </TabsContent>
      </Tabs>
    </div>
  )
}
