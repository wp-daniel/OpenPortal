import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'

export type Theme = 'light' | 'dark'

const STORAGE_KEY = 'openportal-theme'

interface ThemeContextValue {
  readonly theme: Theme
  readonly setTheme: (theme: Theme) => void
  readonly toggleTheme: () => void
}

const ThemeContext = createContext<ThemeContextValue | null>(null)

/** Storage can throw (private windows, blocked site data), and the theme must still work without it. */
function readStoredTheme(): Theme | null {
  try {
    const value = localStorage.getItem(STORAGE_KEY)

    return value === 'light' || value === 'dark' ? value : null
  } catch {
    return null
  }
}

function initialTheme(): Theme {
  return readStoredTheme() ?? (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light')
}

/**
 * Owns the `dark` class on <html>. The inline script in index.html applies the same class before first
 * paint so a reload never flashes the wrong theme.
 */
export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<Theme>(initialTheme)

  useEffect(() => {
    document.documentElement.classList.toggle('dark', theme === 'dark')
  }, [theme])

  const setTheme = useCallback((next: Theme) => {
    setThemeState(next)

    try {
      localStorage.setItem(STORAGE_KEY, next)
    } catch {
      // Not persisting is acceptable; the choice still applies for this page view.
    }
  }, [])

  const value = useMemo<ThemeContextValue>(
    () => ({ theme, setTheme, toggleTheme: () => setTheme(theme === 'dark' ? 'light' : 'dark') }),
    [theme, setTheme],
  )

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
}

export function useTheme(): ThemeContextValue {
  const context = useContext(ThemeContext)

  if (!context) {
    throw new Error('useTheme must be used inside <ThemeProvider>.')
  }

  return context
}
