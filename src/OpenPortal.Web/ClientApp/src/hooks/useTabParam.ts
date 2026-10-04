import { useSearchParams } from 'react-router-dom'

/**
 * The selected tab of a page, kept in `?tab=` so a reload, a shared link or the back button lands on the same
 * tab. The first entry is the default and is left out of the URL; an unknown value falls back to it.
 */
export function useTabParam<T extends string>(tabs: readonly [T, ...T[]]): [T, (tab: string) => void] {
  const [params, setParams] = useSearchParams()
  const requested = params.get('tab')
  const current = tabs.find((tab) => tab === requested) ?? tabs[0]

  function select(tab: string) {
    setParams(
      (previous) => {
        const next = new URLSearchParams(previous)

        if (tab === tabs[0]) {
          next.delete('tab')
        } else {
          next.set('tab', tab)
        }

        return next
      },
      // Switching tabs is not navigation: it should not add a history entry per click.
      { replace: true },
    )
  }

  return [current, select]
}
