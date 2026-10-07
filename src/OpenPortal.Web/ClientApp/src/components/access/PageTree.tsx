import { ChevronRight } from 'lucide-react'
import { useState } from 'react'
import type { PortalPage } from '@/api/types'
import { Badge } from '@/components/ui/badge'
import { Checkbox } from '@/components/ui/checkbox'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import { Label } from '@/components/ui/label'
import { useI18n } from '@/i18n/useI18n'
import { groupPagesByArea } from '@/lib/access'
import { matchesSearch } from '@/lib/tableFilter'
import { cn } from '@/lib/utils'

/**
 * The portal pages as a tree: areas, each with its pages. An area's box selects or clears all its pages and
 * shows a dash when only some are selected. Areas fold; while searching, the matching ones stay open.
 *
 * Controlled: `selected` is the set of granted keys and `onChange` receives the whole new set, so the caller
 * can save it in one request.
 */
export function PageTree({
  pages,
  selected,
  onChange,
  search = '',
  disabled = false,
  idPrefix = 'page-tree',
}: {
  pages: readonly PortalPage[]
  selected: ReadonlySet<string>
  onChange: (next: string[]) => void
  search?: string
  disabled?: boolean
  /** Keeps element ids unique when the tree is shown twice. */
  idPrefix?: string
}) {
  const { t } = useI18n()
  const [collapsed, setCollapsed] = useState<ReadonlySet<string>>(new Set())
  const searching = search.trim() !== ''

  const areas = groupPagesByArea(pages)
    .map(([areaKey, areaPages]) => {
      // An area whose own name matches shows all its pages; otherwise only the matching ones.
      const areaMatches = matchesSearch(search, t(areaKey))

      return {
        areaKey,
        all: areaPages,
        visible: areaMatches ? areaPages : areaPages.filter((page) => matchesSearch(search, t(page.labelKey), page.key)),
      }
    })
    .filter((area) => area.visible.length > 0)

  const change = (keys: readonly string[], value: boolean) => {
    const next = new Set(selected)
    keys.forEach((key) => (value ? next.add(key) : next.delete(key)))
    onChange([...next])
  }

  const toggleOpen = (areaKey: string, open: boolean) =>
    setCollapsed((previous) => {
      const next = new Set(previous)

      if (open) {
        next.delete(areaKey)
      } else {
        next.add(areaKey)
      }

      return next
    })

  if (areas.length === 0) {
    return <p className="text-muted-foreground py-6 text-center text-sm">{t('table.noResults')}</p>
  }

  return (
    <ul role="tree" aria-multiselectable className="divide-y rounded-md border">
      {areas.map(({ areaKey, all, visible }) => {
        const count = all.filter((page) => selected.has(page.key)).length
        const state = count === 0 ? false : count === all.length ? true : 'indeterminate'
        const open = searching || !collapsed.has(areaKey)
        const areaId = `${idPrefix}-area-${areaKey}`

        return (
          <Collapsible key={areaKey} asChild open={open} onOpenChange={(value) => toggleOpen(areaKey, value)}>
            <li role="treeitem" aria-expanded={open} aria-selected={state === true}>
              <div className="flex items-center gap-2 px-2 py-2">
                <CollapsibleTrigger asChild>
                  <button
                    type="button"
                    className="text-muted-foreground hover:text-foreground focus-visible:ring-ring/50 rounded-sm p-1 outline-none focus-visible:ring-[3px]"
                    aria-label={open ? t('pageTree.collapse', { area: t(areaKey) }) : t('pageTree.expand', { area: t(areaKey) })}
                  >
                    <ChevronRight className={cn('size-4 transition-transform duration-200', open && 'rotate-90')} />
                  </button>
                </CollapsibleTrigger>
                <Checkbox
                  id={areaId}
                  checked={state}
                  disabled={disabled}
                  onCheckedChange={() => change(all.map((page) => page.key), state !== true)}
                />
                <Label htmlFor={areaId} className="flex-1 font-medium">
                  {t(areaKey)}
                </Label>
                <Badge variant={count > 0 ? 'secondary' : 'outline'} className="tabular-nums">
                  {count}/{all.length}
                </Badge>
              </div>

              <CollapsibleContent>
                <ul role="group" className="border-border mb-2 ml-[1.375rem] border-l pl-4">
                  {visible.map((page) => {
                    const pageId = `${idPrefix}-${page.key}`

                    return (
                      <li
                        key={page.key}
                        role="treeitem"
                        aria-selected={selected.has(page.key)}
                        className="hover:bg-muted/50 flex items-center gap-3 rounded-md px-2 py-1.5"
                      >
                        <Checkbox
                          id={pageId}
                          checked={selected.has(page.key)}
                          disabled={disabled}
                          onCheckedChange={(value) => change([page.key], value === true)}
                        />
                        <Label htmlFor={pageId} className="flex-1 font-normal">
                          {t(page.labelKey)}
                        </Label>
                        <span className="text-muted-foreground font-mono text-xs">{page.key}</span>
                      </li>
                    )
                  })}
                </ul>
              </CollapsibleContent>
            </li>
          </Collapsible>
        )
      })}
    </ul>
  )
}
