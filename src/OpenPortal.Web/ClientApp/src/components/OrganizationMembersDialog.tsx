import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2, UserPlus, X } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { myOrganizationsApi } from '@/api/organizations'
import type { OrganizationRole } from '@/api/types'
import { ErrorPanel, LoadingState } from '@/components/StatePanels'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { notify } from '@/hooks/useToast'
import { useSession } from '@/hooks/useSession'
import { useI18n } from '@/i18n/useI18n'
import { describeError } from '@/lib/errors'
import { reportFormError } from '@/lib/forms'

/**
 * Delegated management: an organization's own administrator adds people by address, makes them administrators or
 * members, and removes them, without any portal page. The organization always keeps one administrator.
 */
export function OrganizationMembersDialog({ organizationId, onClose }: { organizationId: string | null; onClose: () => void }) {
  return (
    <Dialog open={organizationId !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-lg">{organizationId && <MembersEditor organizationId={organizationId} />}</DialogContent>
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

  const applied = (data: Awaited<ReturnType<typeof myOrganizationsApi.get>>) => queryClient.setQueryData(key, data)

  const add = useMutation({
    mutationFn: () => myOrganizationsApi.addMember(organizationId, email.trim(), 'member'),
    meta: { handlesErrors: true },
    onSuccess: (data) => {
      applied(data)
      setEmail('')
      notify.success(t('organizations.manage.added'))
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
    if (email.trim()) add.mutate()
  }

  if (organization.isPending) return <LoadingState />
  if (organization.isError) return <ErrorPanel message={describeError(organization.error)} onRetry={() => void organization.refetch()} />

  return (
    <div className="grid gap-4">
      <DialogHeader>
        <DialogTitle>{organization.data.name}</DialogTitle>
        <DialogDescription>{t('organizations.manage.description')}</DialogDescription>
      </DialogHeader>

      <form onSubmit={submit} noValidate className="flex gap-2">
        <Input type="email" placeholder={t('organizations.manage.emailPlaceholder')} value={email} onChange={(event) => setEmail(event.target.value)} />
        <Button type="submit" disabled={!email.trim() || add.isPending}>
          {add.isPending ? <Loader2 className="animate-spin" /> : <UserPlus />}
          {t('organizations.manage.add')}
        </Button>
      </form>

      <ul className="max-h-[50vh] divide-y overflow-y-auto rounded-md border">
        {organization.data.members.map((member) => (
          <li key={member.id} className="flex items-center gap-3 px-3 py-2">
            <div className="grid min-w-0 flex-1">
              <span className="truncate text-sm">{member.displayName}</span>
              <span className="text-muted-foreground truncate font-mono text-xs">{member.email}</span>
            </div>
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
    </div>
  )
}
