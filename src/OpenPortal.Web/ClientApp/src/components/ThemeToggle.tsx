import { Moon, Sun } from 'lucide-react'
import { useTheme } from '@/components/theme-provider'
import { Button } from '@/components/ui/button'
import { useI18n } from '@/i18n/useI18n'

/** Light/dark switch. Both icons are stacked and cross-fade with a rotation so the swap reads as one motion. */
export function ThemeToggle({ className }: { className?: string }) {
  const { theme, toggleTheme } = useTheme()
  const { t } = useI18n()
  const isDark = theme === 'dark'

  return (
    <Button
      variant="ghost"
      size="icon"
      className={className}
      onClick={toggleTheme}
      aria-label={isDark ? t('theme.toLight') : t('theme.toDark')}
    >
      <Sun
        className={`absolute size-[1.2rem] transition-all duration-500 ${isDark ? 'scale-100 rotate-0 opacity-100' : 'scale-0 -rotate-90 opacity-0'}`}
      />
      <Moon
        className={`absolute size-[1.2rem] transition-all duration-500 ${isDark ? 'scale-0 rotate-90 opacity-0' : 'scale-100 rotate-0 opacity-100'}`}
      />
    </Button>
  )
}
