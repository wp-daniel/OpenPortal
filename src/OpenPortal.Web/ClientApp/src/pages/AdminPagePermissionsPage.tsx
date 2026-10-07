import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Search, UsersRound } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { accessKeys, pagePermissionsApi } from '@/api/access'
import type { GroupReference, PagePermissionMatrix } from '@/api/types'
import { PageTree } from '@/components/access/PageTree'
import { PageHeader } from '@/components/PageHeader'
import { Section } from '@/components/Section'
import { EmptyState, ErrorPanel, LoadingState } from '@/components/StatePanels'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { useI18n } from '@/i18n/useI18n'
import { useAccessRefresh } from '@/lib/access'
import { describeError, traceIdOf } from '@/lib/errors'
import { matchesSearch } from '@/lib/tableFilter'
import { cn } from '@/lib/utils'

/**
 * Which portal pages each group may open. Pick a group on the left, then tick its pages in the tree on the
 * right: areas select all their pages at once. Pages come from the server catalog, so a new page appears here
 * by itself; groups can grow without widening anything. Administrators open every page anyway, and this
 * screen is never delegated. The selected group is kept in `?group=`.
 */
export function AdminPagePermissionsPage() {
  const { t } = useI18n()
  const [params, setParams] = useSearchParams()
  const [groupSearch, setGroupSearch] = useState('')

  const matrix = useQuery({
    queryKey: accessKeys.pagePermissions,
    queryFn: ({ signal }) => pagePermissionsApi.matrix(signal),
  })

  const groups = matrix.data?.groups ?? []
  const selected = groups.find((group) => group.id === params.get('group')) ?? groups[0]

  const pageCount = useMemo(() => {
    const counts = new Map<string, number>()
    matrix.data?.grants.forEach((grant) => counts.set(grant.groupId, (counts.get(grant.groupId) ?? 0) + 1))

    return counts
  }, [matrix.data])

  const visibleGroups = groups.filter((group) => matchesSearch(groupSearch, group.name))

  const select = (group: GroupReference) =>
    setParams(
      (previous) => {
        const next = new URLSearchParams(previous)
        next.set('group', group.id)

        return next
      },
      { replace: true },
    )

  return (
    <div className="space-y-6">
      <PageHeader title={t('pagePermissions.title')} description={t('pagePermissions.description')} />

      {matrix.isError && (
        <ErrorPanel message={describeError(matrix.error)} traceId={traceIdOf(matrix.error)} onRetry={() => void matrix.refetch()} />
      )}

      {matrix.isPending && <LoadingState />}

      {matrix.data && groups.length === 0 && (
        <Section>
          <EmptyState title={t('pagePermissions.empty.title')} description={t('pagePermissions.empty.description')}>
            <Button asChild variant="outline" size="sm">
              <Link to="/admin/groups">{t('nav.groups')}</Link>
            </Button>
          </EmptyState>
        </Section>
      )}

      {matrix.data && selected && (
        <div className="grid items-start gap-6 lg:grid-cols-[18rem_minmax(0,1fr)]">
          <Section title={t('pagePermissions.groupsTitle')} className="lg:sticky lg:top-20">
            <div className="grid gap-3">
              <SearchBox value={groupSearch} onChange={setGroupSearch} placeholder={t('groups.searchPlaceholder')} />

              {visibleGroups.length === 0 ? (
                <p className="text-muted-foreground py-4 text-center text-sm">{t('table.noResults')}</p>
              ) : (
                <nav aria-label={t('pagePermissions.groupsTitle')} className="-mx-2 grid max-h-[60vh] gap-0.5 overflow-y-auto px-2">
                  {visibleGroups.map((group) => {
                    const active = group.id === selected.id
                    const count = pageCount.get(group.id) ?? 0

                    return (
                      <Button
                        key={group.id}
                        variant="ghost"
                        aria-current={active ? 'true' : undefined}
                        className={cn('h-auto justify-start gap-3 px-2 py-2 font-normal', active && 'bg-accent text-accent-foreground')}
                        onClick={() => select(group)}
                      >
                        <UsersRound className="text-muted-foreground" />
                        <span className="flex-1 truncate text-left">{group.name}</span>
                        <Badge variant={count > 0 ? 'secondary' : 'outline'} className="tabular-nums">
                          {count}
                        </Badge>
                      </Button>
                    )
                  })}
                </nav>
              )}
            </div>
          </Section>

          {/* Keyed by group: the page search and folded areas start fresh for each group. */}
          <GroupPages key={selected.id} group={selected} matrix={matrix.data} />
        </div>
      )}
    </div>
  )
}

/** The page tree of one group, saved as a whole set on every change. */
function GroupPages({ group, matrix }: { group: GroupReference; matrix: PagePermissionMatrix }) {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const refresh = useAccessRefresh()
  const [search, setSearch] = useState('')

  const granted = useMemo(
    () => new Set(matrix.grants.filter((grant) => grant.groupId === group.id).map((grant) => grant.pageKey)),
    [matrix.grants, group.id],
  )

  const save = useMutation({
    mutationFn: (pages: string[]) => pagePermissionsApi.setGroupPages(group.id, pages),
    // Shown at once; the server's answer replaces it, and a failure (toasted globally) puts the old set back.
    onMutate: (pages) => {
      queryClient.setQueryData<PagePermissionMatrix>(accessKeys.pagePermissions, (current) =>
        current && {
          ...current,
          grants: [
            ...current.grants.filter((grant) => grant.groupId !== group.id),
            ...pages.map((pageKey) => ({ pageKey, groupId: group.id })),
          ],
        },
      )
    },
    onSettled: () => refresh(),
  })

  const allKeys = matrix.pages.map((page) => page.key)

  return (
    <Section
      title={group.name}
      description={t('pagePermissions.groupSummary', { count: granted.size, total: allKeys.length })}
      actions={
        <div className="flex gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={save.isPending || granted.size === allKeys.length}
            onClick={() => save.mutate(allKeys)}
          >
            {t('pageTree.selectAll')}
          </Button>
          <Button variant="ghost" size="sm" disabled={save.isPending || granted.size === 0} onClick={() => save.mutate([])}>
            {t('pageTree.clear')}
          </Button>
        </div>
      }
    >
      <div className="grid gap-3">
        <SearchBox value={search} onChange={setSearch} placeholder={t('pageTree.searchPlaceholder')} />
        <p className="text-muted-foreground text-xs">{t('pagePermissions.matrixHint')}</p>
        <PageTree
          pages={matrix.pages}
          selected={granted}
          search={search}
          disabled={save.isPending}
          onChange={(pages) => save.mutate(pages)}
        />
      </div>
    </Section>
  )
}

function SearchBox({ value, onChange, placeholder }: { value: string; onChange: (value: string) => void; placeholder: string }) {
  return (
    <div className="relative">
      <Search className="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2" />
      <Input
        type="search"
        maxLength={256}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={placeholder}
        aria-label={placeholder}
        className="pl-8"
      />
    </div>
  )
}
