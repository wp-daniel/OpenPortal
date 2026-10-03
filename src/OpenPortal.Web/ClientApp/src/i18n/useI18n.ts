import { useSyncExternalStore } from 'react'
import { getState, setLanguage, subscribe, type I18nState } from './store'

/**
 * The active language and its `t` function. The component re-renders when the language changes because the
 * snapshot is replaced (not mutated) on every change.
 */
export function useI18n(): I18nState & { readonly setLanguage: (code: string) => Promise<void> } {
  const state = useSyncExternalStore(subscribe, getState)

  return { ...state, setLanguage }
}
