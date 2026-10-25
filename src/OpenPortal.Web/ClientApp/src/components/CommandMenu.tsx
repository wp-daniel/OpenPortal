import { useQuery } from '@tanstack/react-query'
import { Building2, CornerDownLeft, FileText, Search, UserRound, Users } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { groupsApi } from '@/api/access'
import { organizationsApi } from '@/api/organizations'
import { userAdminApi } from '@/api/users'
import { portalLinks, useAdminBranches } from '@/components/AppSidebar'
import { Button } from '@/components/ui/button'
import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandShortcut,
} from '@/components/ui/command'
import { useDebounced } from '@/hooks/useDebounced'
import { useSession } from '@/hooks/useSession'
import { useI18n } from '@/i18n/useI18n'

const RESULTS = 5

/**
 * The portal's search (Ctrl/Cmd+K, or the header button): the pages the user may open, and — for whoever manages them —
 * people, groups and organizations found on the server as they type. Picking one opens it.
 */
export function CommandMenu() {
  const { t } = useI18n()
  const navigate = useNavigate()
  const { canOpen } = useSession()
  const branches = useAdminBranches()
  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState('')
  const term = useDebounced(search.trim())
  const searching = open && term.length >= 2

  useEffect(() => {
    function onKey(event: KeyboardEvent) {
      if (event.key.toLowerCase() === 'k' && (event.metaKey || event.ctrlKey)) {
        event.preventDefault()
        setOpen((current) => !current)
      }
    }

    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  const users = useQuery({
    queryKey: ['command-users', term],
    queryFn: ({ signal }) => userAdminApi.list({ page: 1, pageSize: RESULTS, search: term }, signal),
    enabled: searching && canOpen('users'),
  })

  const groups = useQuery({
    queryKey: ['command-groups', term],
    queryFn: ({ signal }) => groupsApi.slice({ search: term, skip: 0, take: RESULTS }, signal),
    enabled: searching && canOpen('groups'),
  })

  const organizations = useQuery({
    queryKey: ['command-organizations', term],
    queryFn: ({ signal }) => organizationsApi.slice({ search: term, skip: 0, take: RESULTS }, signal),
    enabled: searching && canOpen('organizations'),
  })

  function go(to: string) {
    setOpen(false)
    setSearch('')
    void navigate(to)
  }

  return (
    <>
      <Button
        variant="outline"
        size="sm"
        className="text-muted-foreground hidden w-56 justify-start gap-2 font-normal md:flex"
        onClick={() => setOpen(true)}
      >
        <Search />
        {t('command.open')}
        <kbd className="bg-muted ml-auto rounded px-1.5 font-mono text-[10px]">Ctrl K</kbd>
      </Button>
      <Button variant="ghost" size="icon" className="md:hidden" aria-label={t('command.open')} onClick={() => setOpen(true)}>
        <Search />
      </Button>

      <CommandDialog
        open={open}
        onOpenChange={(next) => {
          setOpen(next)
          if (!next) setSearch('')
        }}
        title={t('command.title')}
        description={t('command.description')}
      >
        <CommandInput placeholder={t('command.placeholder')} value={search} onValueChange={setSearch} />
        <CommandList>
          <CommandEmpty>{t('command.empty')}</CommandEmpty>

          <CommandGroup heading={t('command.pages')}>
            {portalLinks.map((link) => (
              <CommandItem key={link.to} value={`page ${t(link.labelKey)}`} onSelect={() => go(link.to)}>
                <link.icon />
                {t(link.labelKey)}
              </CommandItem>
            ))}
            {branches.flatMap((branch) =>
              branch.children.map((child) => (
                <CommandItem key={child.to} value={`page ${t(branch.labelKey)} ${t(child.labelKey)}`} onSelect={() => go(child.to)}>
                  <FileText />
                  {t(child.labelKey)}
                  <CommandShortcut>{t(branch.labelKey)}</CommandShortcut>
                </CommandItem>
              )),
            )}
          </CommandGroup>

          {(users.data?.items.length ?? 0) > 0 && (
            <CommandGroup heading={t('command.users')}>
              {users.data!.items.map((user) => (
                <CommandItem
                  key={user.id}
                  value={`user ${user.displayName} ${user.email} ${term}`}
                  onSelect={() => go(`/admin/users?search=${encodeURIComponent(user.email)}`)}
                >
                  <UserRound />
                  <span className="truncate">{user.displayName}</span>
                  <CommandShortcut className="truncate tracking-normal">{user.email}</CommandShortcut>
                </CommandItem>
              ))}
            </CommandGroup>
          )}

          {(groups.data?.items.length ?? 0) > 0 && (
            <CommandGroup heading={t('command.groups')}>
              {groups.data!.items.map((group) => (
                <CommandItem
                  key={group.id}
                  value={`group ${group.name} ${term}`}
                  onSelect={() => go(`/admin/groups?search=${encodeURIComponent(group.name)}`)}
                >
                  <Users />
                  {group.name}
                </CommandItem>
              ))}
            </CommandGroup>
          )}

          {(organizations.data?.items.length ?? 0) > 0 && (
            <CommandGroup heading={t('command.organizations')}>
              {organizations.data!.items.map((organization) => (
                <CommandItem
                  key={organization.id}
                  value={`organization ${organization.name} ${organization.slug} ${term}`}
                  onSelect={() => go(`/admin/organizations?search=${encodeURIComponent(organization.slug)}`)}
                >
                  <Building2 />
                  {organization.name}
                  <CommandShortcut className="tracking-normal">{organization.slug}</CommandShortcut>
                </CommandItem>
              ))}
            </CommandGroup>
          )}
        </CommandList>
        <div className="text-muted-foreground flex items-center gap-1 border-t px-3 py-2 text-xs">
          <CornerDownLeft className="size-3" />
          {t('command.hint')}
        </div>
      </CommandDialog>
    </>
  )
}
