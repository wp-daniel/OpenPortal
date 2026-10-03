import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Languages } from 'lucide-react'
import { accountApi } from '@/api/auth'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { SESSION_QUERY_KEY, useSession } from '@/hooks/useSession'
import { useI18n } from '@/i18n/useI18n'

/**
 * Language selector, shown on every screen including sign-in.
 *
 * The list comes from the server, so a newly configured language appears here without a client change. The
 * choice applies immediately; when someone is signed in it is also saved to their profile (anonymous
 * visitors keep it in localStorage until they sign in).
 */
export function LanguageSwitcher({ className }: { className?: string }) {
  const { language, languages, t, setLanguage } = useI18n()
  const { user } = useSession()
  const queryClient = useQueryClient()

  const save = useMutation({
    mutationFn: (code: string) => accountApi.updateLanguage(code),
    // The session carries the saved language, so refresh it; the global MutationCache handler toasts a
    // failure, and the UI language has already changed, which is the right outcome even if saving failed.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: SESSION_QUERY_KEY }),
  })

  function choose(code: string) {
    if (code === language) {
      return
    }

    void setLanguage(code).then(() => {
      if (user) {
        save.mutate(code)
      }
    })
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="sm" className={className} aria-label={t('language.change')}>
          <Languages />
          <span className="text-xs font-medium uppercase" aria-hidden="true">
            {language.split('-')[0]}
          </span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="min-w-40">
        <DropdownMenuLabel className="text-muted-foreground text-xs font-normal">{t('language.label')}</DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuRadioGroup value={language} onValueChange={choose}>
          {languages.map((entry) => (
            <DropdownMenuRadioItem key={entry.code} value={entry.code} lang={entry.code}>
              {entry.name}
            </DropdownMenuRadioItem>
          ))}
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
