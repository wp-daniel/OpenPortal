import { Button } from '@/components/ui/button'
import { useI18n } from '@/i18n/useI18n'

/** Previous / next under a list read a page at a time from the server, with where the reader is. */
export function ListPager({
  page,
  pageSize,
  totalCount,
  onPageChange,
  label,
}: {
  page: number
  pageSize: number
  totalCount: number
  onPageChange: (page: number) => void
  label: string
}) {
  const { t } = useI18n()
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))

  if (totalPages <= 1) {
    return null
  }

  return (
    <nav aria-label={label} className="mt-4 flex items-center justify-between gap-2 text-sm">
      <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
        {t('common.previous')}
      </Button>
      <span className="text-muted-foreground text-center">{t('table.pageOf', { page, total: totalPages, count: totalCount })}</span>
      <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>
        {t('common.next')}
      </Button>
    </nav>
  )
}
