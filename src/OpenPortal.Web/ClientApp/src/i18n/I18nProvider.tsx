import { useEffect, type ReactNode } from 'react'
import { useMutation } from '@tanstack/react-query'
import { accountApi } from '@/api/auth'
import { useSession } from '@/hooks/useSession'
import { initialise, setLanguage } from './store'
import { useI18n } from './useI18n'

/**
 * Holds the app back until the first language is loaded, so no screen ever flashes raw keys.
 * It renders an empty full-height block meanwhile; the theme is already applied by the inline script in
 * index.html, so the placeholder matches the final background.
 */
export function I18nProvider({ children }: { children: ReactNode }) {
  const { ready } = useI18n()

  useEffect(() => {
    void initialise()
  }, [])

  return ready ? children : <div className="min-h-screen" aria-busy="true" />
}

/**
 * Keeps the UI language and the profile's saved language in agreement. Mount it once, below the query
 * client.
 *
 * - When the profile's language changes (sign-in, or another tab saved a new one) the UI follows it.
 * - A profile that has no language yet adopts the one currently shown, so the first sign-in on a device
 *   that already picked a language does not lose that choice.
 *
 * The effect keys on the profile value, not on the UI language. If it keyed on both, choosing a language
 * in the selector would briefly look like a disagreement with the not-yet-refreshed session and snap back.
 */
export function LanguageSync() {
  const { user } = useSession()
  const { language } = useI18n()
  const profileLanguage = user?.language ?? null
  const userId = user?.id ?? null

  const adopt = useMutation({
    mutationFn: (code: string) => accountApi.updateLanguage(code),
    meta: { handlesErrors: true },
  })

  useEffect(() => {
    if (userId === null) {
      return
    }

    if (profileLanguage !== null) {
      void setLanguage(profileLanguage)
    } else {
      adopt.mutate(language)
    }
    // Deliberately not depending on `language` or `adopt`: see the note above.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [userId, profileLanguage])

  return null
}
