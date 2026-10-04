import { useQuery } from '@tanstack/react-query'
import { Check, ChevronsUpDown, Loader2 } from 'lucide-react'
import { useEffect, useState } from 'react'
import { accessKeys, groupsApi } from '@/api/access'
import { userAdminApi } from '@/api/users'
import { Button } from '@/components/ui/button'
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { useI18n } from '@/i18n/useI18n'
import { cn } from '@/lib/utils'

export interface PickedPrincipal {
  readonly id: string
  readonly label: string
}

interface PickerProps {
  readonly id?: string
  readonly value: PickedPrincipal | null
  readonly onChange: (value: PickedPrincipal | null) => void
  /** Ids that are already granted or already members, shown as unavailable. */
  readonly exclude?: readonly string[]
}

/** Waits for typing to pause, so the user search does not query on every keystroke. */
function useDebounced<T>(value: T, delayMs = 250): T {
  const [debounced, setDebounced] = useState(value)

  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(value), delayMs)
    return () => window.clearTimeout(timer)
  }, [value, delayMs])

  return debounced
}

/**
 * Combobox over the user accounts. Searching happens on the server (the user list is paged), so the
 * command list's own filtering is turned off.
 */
export function UserPicker({ id, value, onChange, exclude = [] }: PickerProps) {
  const { t } = useI18n()
  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState('')
  const term = useDebounced(search.trim())

  const users = useQuery({
    queryKey: ['admin-users', 'picker', term],
    queryFn: ({ signal }) => userAdminApi.list({ page: 1, pageSize: 20, search: term }, signal),
    enabled: open,
  })

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button id={id} variant="outline" role="combobox" aria-expanded={open} className="w-full justify-between font-normal">
          <span className={cn('truncate', !value && 'text-muted-foreground')}>{value?.label ?? t('access.picker.user')}</span>
          <ChevronsUpDown className="opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-(--radix-popover-trigger-width) p-0" align="start">
        <Command shouldFilter={false}>
          <CommandInput value={search} onValueChange={setSearch} placeholder={t('access.picker.searchUsers')} />
          <CommandList>
            {users.isFetching && (
              <div className="text-muted-foreground flex items-center gap-2 px-3 py-2 text-sm">
                <Loader2 className="size-4 animate-spin" />
                {t('common.loading')}
              </div>
            )}
            <CommandEmpty>{t('access.picker.noUsers')}</CommandEmpty>
            <CommandGroup>
              {users.data?.items.map((user) => {
                const taken = exclude.includes(user.id)

                return (
                  <CommandItem
                    key={user.id}
                    value={user.id}
                    disabled={taken}
                    onSelect={() => {
                      onChange({ id: user.id, label: `${user.displayName} · ${user.email}` })
                      setOpen(false)
                    }}
                  >
                    <div className="grid min-w-0">
                      <span className="truncate">{user.displayName}</span>
                      <span className="text-muted-foreground truncate font-mono text-xs">{user.email}</span>
                    </div>
                    <Check className={cn('ml-auto', value?.id === user.id ? 'opacity-100' : 'opacity-0')} />
                  </CommandItem>
                )
              })}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  )
}

/** Combobox over the groups. There are few enough to load once and filter in the list. */
export function GroupPicker({ id, value, onChange, exclude = [] }: PickerProps) {
  const { t } = useI18n()
  const [open, setOpen] = useState(false)

  const groups = useQuery({
    queryKey: accessKeys.groups,
    queryFn: ({ signal }) => groupsApi.list(signal),
    enabled: open,
  })

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button id={id} variant="outline" role="combobox" aria-expanded={open} className="w-full justify-between font-normal">
          <span className={cn('truncate', !value && 'text-muted-foreground')}>{value?.label ?? t('access.picker.group')}</span>
          <ChevronsUpDown className="opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-(--radix-popover-trigger-width) p-0" align="start">
        <Command>
          <CommandInput placeholder={t('access.picker.searchGroups')} />
          <CommandList>
            <CommandEmpty>{groups.isPending ? t('common.loading') : t('access.picker.noGroups')}</CommandEmpty>
            <CommandGroup>
              {groups.data?.map((group) => (
                <CommandItem
                  key={group.id}
                  value={group.name}
                  disabled={exclude.includes(group.id)}
                  onSelect={() => {
                    onChange({ id: group.id, label: group.name })
                    setOpen(false)
                  }}
                >
                  <span className="truncate">{group.name}</span>
                  <span className="text-muted-foreground ml-auto text-xs">
                    {t('groups.memberCount', { count: group.memberCount })}
                  </span>
                  <Check className={cn(value?.id === group.id ? 'opacity-100' : 'opacity-0')} />
                </CommandItem>
              ))}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  )
}
