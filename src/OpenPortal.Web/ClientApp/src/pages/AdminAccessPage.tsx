import { useMutation, useQuery } from '@tanstack/react-query'
import {
  AppWindow,
  ChevronRight,
  ChevronsDownUp,
  ChevronsUpDown,
  Loader2,
  Plus,
  Search,
  UserRound,
  Users,
  X,
} from 'lucide-react'
import { useMemo, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { accessApi, accessKeys } from '@/api/access'
import type { AccessTreeApplication, UserReference } from '@/api/types'
import { GroupPicker, UserPicker, type PickedPrincipal } from '@/components/access/PrincipalPicker'
import { ApplicationStatusBadge, OnlineIndicator } from '@/components/access/shared'
import { useAccessRefresh } from '@/lib/access'
import { FormField } from '@/components/FormField'
import { PageHeader } from '@/components/PageHeader'
import { Section } from '@/components/Section'
import { EmptyState, ErrorPanel, LoadingState } from '@/components/StatePanels'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { notify } from '@/hooks/useToast'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'
import { cn } from '@/lib/utils'

interface Revocation {
  readonly application: AccessTreeApplication
  readonly kind: 'user' | 'group'
  readonly id: string
  readonly name: string
}

/**
 * The access tree: each application, the groups that can open it (with their members), and the users who
 * were given it directly. Effective access is the union of the two branches.
 */
export function AdminAccessPage() {
  const { t } = useI18n()
  const refresh = useAccessRefresh()

  const [filter, setFilter] = useState('')
  const [expanded, setExpanded] = useState<ReadonlySet<string>>(new Set())
  const [granting, setGranting] = useState<AccessTreeApplication | null>(null)
  const [revoking, setRevoking] = useState<Revocation | null>(null)

  const tree = useQuery({
    queryKey: accessKeys.tree,
    queryFn: ({ signal }) => accessApi.tree(signal),
  })

  const revoke = useMutation({
    mutationFn: (pending: Revocation) =>
      pending.kind === 'user'
        ? accessApi.revokeUser(pending.application.id, pending.id)
        : accessApi.revokeGroup(pending.application.id, pending.id),
    onSuccess: async (_result, pending) => {
      setRevoking(null)
      notify.success(t('access.revoked'), `${pending.name} · ${pending.application.displayName}`)
      await refresh()
    },
  })

  const term = filter.trim().toLowerCase()
  const visible = useMemo(() => filterTree(tree.data?.applications ?? [], term), [tree.data, term])

  // While filtering, every node on a matching path is open, so a hit is never hidden in a closed branch.
  const isOpen = (key: string) => term !== '' || expanded.has(key)
  const setOpen = (key: string, open: boolean) =>
    setExpanded((current) => {
      const next = new Set(current)
      if (open) {
        next.add(key)
      } else {
        next.delete(key)
      }
      return next
    })

  const allKeys = () =>
    (tree.data?.applications ?? []).flatMap((application) => [
      application.id,
      `${application.id}:groups`,
      `${application.id}:users`,
      ...application.groups.map((group) => `${application.id}:${group.id}`),
    ])

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('access.title')}
        description={t('access.description')}
        actions={
          <Button asChild variant="outline">
            <Link to="/admin/groups">
              <Users />
              {t('nav.groups')}
            </Link>
          </Button>
        }
      />

      <Section>
        <div className="mb-4 flex flex-wrap items-center gap-2">
          <div className="relative w-full sm:w-80">
            <Search className="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2" />
            <Input
              value={filter}
              onChange={(event) => setFilter(event.target.value)}
              placeholder={t('access.filter')}
              aria-label={t('access.filter')}
              className="pl-8"
            />
          </div>
          <div className="ml-auto flex gap-1">
            <Button variant="ghost" size="sm" onClick={() => setExpanded(new Set(allKeys()))}>
              <ChevronsUpDown />
              {t('access.expandAll')}
            </Button>
            <Button variant="ghost" size="sm" onClick={() => setExpanded(new Set())}>
              <ChevronsDownUp />
              {t('access.collapseAll')}
            </Button>
          </div>
        </div>

        {tree.isError && (
          <ErrorPanel message={describeError(tree.error)} traceId={traceIdOf(tree.error)} onRetry={() => void tree.refetch()} />
        )}

        {tree.isPending && <LoadingState />}

        {tree.data?.applications.length === 0 && (
          <EmptyState title={t('access.empty.title')} description={t('access.empty.description')}>
            <Button asChild variant="outline" size="sm">
              <Link to="/admin/applications">{t('nav.applications')}</Link>
            </Button>
          </EmptyState>
        )}

        {tree.data && tree.data.applications.length > 0 && visible.length === 0 && (
          <p className="text-muted-foreground py-6 text-center text-sm">{t('access.noMatch')}</p>
        )}

        <ul role="tree" aria-label={t('access.title')} className="grid gap-2">
          {visible.map((application) => (
            <li key={application.id} role="treeitem" aria-expanded={isOpen(application.id)} className="rounded-lg border">
              <Collapsible open={isOpen(application.id)} onOpenChange={(open) => setOpen(application.id, open)}>
                <div className="flex flex-wrap items-center gap-2 p-2 pr-3">
                  <CollapsibleTrigger asChild>
                    <Button variant="ghost" size="sm" className="h-auto min-w-0 flex-1 justify-start gap-2 py-1.5">
                      <Chevron open={isOpen(application.id)} />
                      <AppWindow className="text-muted-foreground" />
                      <span className="truncate font-medium">{application.displayName}</span>
                      <span className="text-muted-foreground hidden truncate font-mono text-xs sm:inline">{application.clientId}</span>
                    </Button>
                  </CollapsibleTrigger>
                  <OnlineIndicator lastSeenAtUtc={application.lastSeenAtUtc} />
                  <ApplicationStatusBadge status={application.status} />
                  <span className="text-muted-foreground text-xs">
                    {t('applications.accessSummary', { groups: application.groups.length, users: application.users.length })}
                  </span>
                  <Button variant="outline" size="sm" onClick={() => setGranting(application)}>
                    <Plus />
                    {t('access.grant')}
                  </Button>
                </div>

                <CollapsibleContent>
                  <ul role="group" className="ml-6 grid gap-1 border-l pb-3 pl-3">
                    <Branch
                      label={t('access.branch.groups', { count: application.groups.length })}
                      icon={<Users className="text-muted-foreground" />}
                      open={isOpen(`${application.id}:groups`)}
                      onOpenChange={(open) => setOpen(`${application.id}:groups`, open)}
                      empty={application.groups.length === 0 ? t('access.branch.noGroups') : null}
                    >
                      {application.groups.map((group) => {
                        const key = `${application.id}:${group.id}`

                        return (
                          <li key={group.id} role="treeitem" aria-expanded={isOpen(key)}>
                            <Collapsible open={isOpen(key)} onOpenChange={(open) => setOpen(key, open)}>
                              <div className="flex items-center gap-1">
                                <CollapsibleTrigger asChild>
                                  <Button variant="ghost" size="sm" className="min-w-0 flex-1 justify-start gap-2">
                                    <Chevron open={isOpen(key)} />
                                    <Users className="text-muted-foreground" />
                                    <span className="truncate">{group.name}</span>
                                    <span className="text-muted-foreground text-xs">
                                      {t('groups.memberCount', { count: group.members.length })}
                                    </span>
                                  </Button>
                                </CollapsibleTrigger>
                                <RevokeButton
                                  label={t('access.revokeNamed', { name: group.name })}
                                  onClick={() => setRevoking({ application, kind: 'group', id: group.id, name: group.name })}
                                />
                              </div>
                              <CollapsibleContent>
                                <ul role="group" className="ml-6 grid gap-0.5 border-l py-1 pl-3">
                                  {group.members.length === 0 && (
                                    <li className="text-muted-foreground px-2 py-1 text-xs">{t('groups.sheet.noMembers')}</li>
                                  )}
                                  {group.members.map((member) => (
                                    <li key={member.id} role="treeitem">
                                      <UserLeaf user={member} />
                                    </li>
                                  ))}
                                </ul>
                              </CollapsibleContent>
                            </Collapsible>
                          </li>
                        )
                      })}
                    </Branch>

                    <Branch
                      label={t('access.branch.users', { count: application.users.length })}
                      icon={<UserRound className="text-muted-foreground" />}
                      open={isOpen(`${application.id}:users`)}
                      onOpenChange={(open) => setOpen(`${application.id}:users`, open)}
                      empty={application.users.length === 0 ? t('access.branch.noUsers') : null}
                    >
                      {application.users.map((user) => (
                        <li key={user.id} role="treeitem" className="flex items-center gap-1">
                          <UserLeaf user={user} />
                          <RevokeButton
                            label={t('access.revokeNamed', { name: user.displayName || user.email })}
                            onClick={() =>
                              setRevoking({ application, kind: 'user', id: user.id, name: user.displayName || user.email })
                            }
                          />
                        </li>
                      ))}
                    </Branch>
                  </ul>
                </CollapsibleContent>
              </Collapsible>
            </li>
          ))}
        </ul>
      </Section>

      <GrantDialog
        application={granting}
        onClose={() => setGranting(null)}
        onGranted={(application) => {
          setOpen(application.id, true)
          void refresh()
        }}
      />

      <AlertDialog open={revoking !== null} onOpenChange={(open) => !open && setRevoking(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {t('access.revokeTitle', { name: revoking?.name ?? '', application: revoking?.application.displayName ?? '' })}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {revoking?.kind === 'group' ? t('access.revokeGroupDescription') : t('access.revokeUserDescription')}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t('common.cancel')}</AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              disabled={revoke.isPending}
              onClick={(event) => {
                event.preventDefault()
                if (revoking) {
                  revoke.mutate(revoking)
                }
              }}
            >
              {t('access.revoke')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}

function Chevron({ open }: { open: boolean }) {
  return <ChevronRight className={cn('text-muted-foreground transition-transform', open && 'rotate-90')} />
}

/** A labelled, collapsible level of the tree ("Groups", "Direct users"). */
function Branch({
  label,
  icon,
  open,
  onOpenChange,
  empty,
  children,
}: {
  label: string
  icon: ReactNode
  open: boolean
  onOpenChange: (open: boolean) => void
  empty: string | null
  children: ReactNode
}) {
  return (
    <li role="treeitem" aria-expanded={open}>
      <Collapsible open={open} onOpenChange={onOpenChange}>
        <CollapsibleTrigger asChild>
          <Button variant="ghost" size="sm" className="text-muted-foreground w-full justify-start gap-2">
            <Chevron open={open} />
            {icon}
            <span className="text-xs font-medium tracking-wide uppercase">{label}</span>
          </Button>
        </CollapsibleTrigger>
        <CollapsibleContent>
          <ul role="group" className="ml-6 grid gap-0.5 border-l py-1 pl-3">
            {empty ? <li className="text-muted-foreground px-2 py-1 text-xs">{empty}</li> : children}
          </ul>
        </CollapsibleContent>
      </Collapsible>
    </li>
  )
}

function UserLeaf({ user }: { user: UserReference }) {
  const { t } = useI18n()

  return (
    <div className="flex min-w-0 flex-1 items-center gap-2 px-2 py-1">
      <UserRound className="text-muted-foreground size-4 shrink-0" />
      <span className="truncate text-sm">{user.isKnown ? user.displayName : t('access.unknownUser')}</span>
      <span className="text-muted-foreground hidden truncate font-mono text-xs sm:inline">{user.email}</span>
    </div>
  )
}

function RevokeButton({ label, onClick }: { label: string; onClick: () => void }) {
  return (
    <Button variant="ghost" size="sm" aria-label={label} title={label} onClick={onClick}>
      <X />
    </Button>
  )
}

/** Grants an application to a group or to a single user. */
function GrantDialog({
  application,
  onClose,
  onGranted,
}: {
  application: AccessTreeApplication | null
  onClose: () => void
  onGranted: (application: AccessTreeApplication) => void
}) {
  const { t } = useI18n()
  const [tab, setTab] = useState<'group' | 'user'>('group')
  const [group, setGroup] = useState<PickedPrincipal | null>(null)
  const [user, setUser] = useState<PickedPrincipal | null>(null)

  const grant = useMutation({
    mutationFn: async () => {
      if (!application) {
        return
      }

      if (tab === 'group' && group) {
        await accessApi.grantGroup(application.id, group.id)
      } else if (tab === 'user' && user) {
        await accessApi.grantUser(application.id, user.id)
      }
    },
    onSuccess: () => {
      const name = tab === 'group' ? group?.label : user?.label
      notify.success(t('access.granted'), `${name ?? ''} · ${application?.displayName ?? ''}`)
      if (application) {
        onGranted(application)
      }
      close()
    },
  })

  function close() {
    setGroup(null)
    setUser(null)
    setTab('group')
    onClose()
  }

  const selected = tab === 'group' ? group : user

  return (
    <Dialog open={application !== null} onOpenChange={(open) => !open && close()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('access.grantTitle', { application: application?.displayName ?? '' })}</DialogTitle>
          <DialogDescription>{t('access.grantDescription')}</DialogDescription>
        </DialogHeader>

        <Tabs value={tab} onValueChange={(value) => setTab(value as 'group' | 'user')}>
          <TabsList className="w-full">
            <TabsTrigger value="group">
              <Users />
              {t('access.tab.group')}
            </TabsTrigger>
            <TabsTrigger value="user">
              <UserRound />
              {t('access.tab.user')}
            </TabsTrigger>
          </TabsList>
          <TabsContent value="group" className="pt-3">
            <FormField label={t('access.tab.group')} htmlFor="grant-group" hint={t('access.tab.groupHint')}>
              <GroupPicker
                value={group}
                onChange={setGroup}
                exclude={application?.groups.map((entry) => entry.id) ?? []}
              />
            </FormField>
          </TabsContent>
          <TabsContent value="user" className="pt-3">
            <FormField label={t('access.tab.user')} htmlFor="grant-user" hint={t('access.tab.userHint')}>
              <UserPicker value={user} onChange={setUser} exclude={application?.users.map((entry) => entry.id) ?? []} />
            </FormField>
          </TabsContent>
        </Tabs>

        <DialogFooter>
          <Button variant="outline" onClick={close}>
            {t('common.cancel')}
          </Button>
          <Button disabled={!selected || grant.isPending} onClick={() => grant.mutate()}>
            {grant.isPending && <Loader2 className="animate-spin" />}
            {t('access.grant')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/**
 * Keeps the applications, groups and users that match the filter. A match on an application keeps it
 * whole; otherwise only the matching groups (or groups with a matching member) and users remain.
 */
function filterTree(applications: readonly AccessTreeApplication[], term: string): readonly AccessTreeApplication[] {
  if (term === '') {
    return applications
  }

  const matches = (...values: readonly string[]) => values.some((value) => value.toLowerCase().includes(term))
  const userMatches = (user: UserReference) => matches(user.displayName, user.email)

  return applications.flatMap((application) => {
    if (matches(application.displayName, application.clientId)) {
      return [application]
    }

    const groups = application.groups.flatMap((group) => {
      if (matches(group.name)) {
        return [group]
      }

      const members = group.members.filter(userMatches)
      return members.length > 0 ? [{ ...group, members }] : []
    })

    const users = application.users.filter(userMatches)

    return groups.length > 0 || users.length > 0 ? [{ ...application, groups, users }] : []
  })
}
