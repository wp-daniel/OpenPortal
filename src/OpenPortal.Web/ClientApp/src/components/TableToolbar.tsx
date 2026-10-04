import { ChevronDown, Search, X } from 'lucide-react'
import type { ReactNode } from 'react'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { useI18n } from '@/i18n/useI18n'

/**
 * The bar above a table: a search box, the table's own filters (as children), a result count and a reset
 * that appears only while something is filtered.
 */
export function TableToolbar({
  search,
  onSearchChange,
  searchPlaceholder,
  filtersActive,
  onReset,
  resultCount,
  children,
}: {
  search: string
  onSearchChange: (value: string) => void
  searchPlaceholder?: string
  filtersActive: boolean
  onReset: () => void
  resultCount?: number
  children?: ReactNode
}) {
  const { t } = useI18n()

  return (
    <div className="mb-4 flex flex-wrap items-center gap-2">
      <div className="relative w-full sm:w-72">
        <Search className="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2" />
        <Input
          type="search"
          maxLength={256}
          value={search}
          onChange={(event) => onSearchChange(event.target.value)}
          placeholder={searchPlaceholder ?? t('table.searchPlaceholder')}
          aria-label={searchPlaceholder ?? t('common.search')}
          className="pl-8"
        />
      </div>

      {children}

      {filtersActive && (
        <Button type="button" variant="ghost" size="sm" onClick={onReset}>
          <X />
          {t('table.resetFilters')}
        </Button>
      )}

      {resultCount !== undefined && (
        <span className="text-muted-foreground ml-auto text-sm" aria-live="polite">
          {t('table.results', { count: resultCount })}
        </span>
      )}
    </div>
  )
}

export interface FilterOption {
  readonly value: string
  readonly label: string
}

/** A single-choice filter. The empty value means "no filter" and is offered first as "All". */
export function FilterSelect({
  label,
  value,
  onChange,
  options,
}: {
  label: string
  value: string
  onChange: (value: string) => void
  options: readonly FilterOption[]
}) {
  const { t } = useI18n()
  const current = options.find((option) => option.value === value)

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button type="button" variant="outline" size="sm" className={value ? 'border-primary' : undefined}>
          <span className="text-muted-foreground">{label}:</span>
          {current?.label ?? t('common.all')}
          <ChevronDown className="opacity-50" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start">
        <DropdownMenuRadioGroup value={value} onValueChange={onChange}>
          <DropdownMenuRadioItem value="">{t('common.all')}</DropdownMenuRadioItem>
          {options.map((option) => (
            <DropdownMenuRadioItem key={option.value} value={option.value}>
              {option.label}
            </DropdownMenuRadioItem>
          ))}
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
