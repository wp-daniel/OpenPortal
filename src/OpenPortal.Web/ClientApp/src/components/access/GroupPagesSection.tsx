import { useMutation, useQuery } from '@tanstack/react-query'
import { accessKeys, pagePermissionsApi } from '@/api/access'
import type { GroupDetail } from '@/api/types'
import { PageTree } from '@/components/access/PageTree'
import { ErrorPanel, LoadingState } from '@/components/StatePanels'
import { useI18n } from '@/i18n/useI18n'
import { useAccessRefresh } from '@/lib/access'
import { describeError } from '@/lib/errors'

/**
 * The portal pages a group's members may open, as the same tree as the page permissions screen.
 * Administrators only: the page list comes from the page permission matrix, which is never delegated.
 */
export function GroupPagesSection({ group }: { group: GroupDetail }) {
  const { t } = useI18n()
  const refresh = useAccessRefresh()

  const matrix = useQuery({
    queryKey: accessKeys.pagePermissions,
    queryFn: ({ signal }) => pagePermissionsApi.matrix(signal),
  })

  const save = useMutation({
    mutationFn: (pages: string[]) => pagePermissionsApi.setGroupPages(group.id, pages),
    onSettled: () => refresh(),
  })

  return (
    <section className="grid gap-3">
      <p className="text-muted-foreground text-xs">{t('groups.sheet.pagesHint')}</p>

      {matrix.isError && <ErrorPanel message={describeError(matrix.error)} onRetry={() => void matrix.refetch()} />}
      {matrix.isPending && <LoadingState />}

      {matrix.data && (
        <PageTree
          idPrefix={`group-${group.id}`}
          pages={matrix.data.pages}
          selected={new Set(save.isPending && save.variables ? save.variables : group.pages)}
          disabled={save.isPending}
          onChange={(pages) => save.mutate(pages)}
        />
      )}
    </section>
  )
}
