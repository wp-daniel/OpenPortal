import { useMutation, useQueryClient } from '@tanstack/react-query'
import { ChevronDown, LogOut, Menu, User } from 'lucide-react'
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { authApi } from '@/api/auth'
import { LanguageSwitcher } from '@/components/LanguageSwitcher'
import { ThemeToggle } from '@/components/ThemeToggle'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useSession } from '@/hooks/useSession'
import { useI18n } from '@/i18n/useI18n'
import { cn } from '@/lib/utils'

interface NavEntry {
  readonly to: string
  /** Translation key of the link text. */
  readonly labelKey: string
  readonly end?: boolean
}

const userLinks: readonly NavEntry[] = [{ to: '/', labelKey: 'nav.dashboard', end: true }]

/** Administration pages, grouped under one menu so the header stays short as the portal grows. */
const adminLinks: readonly NavEntry[] = [
  { to: '/admin/users', labelKey: 'nav.users' },
  { to: '/admin/access', labelKey: 'nav.access' },
  { to: '/admin/applications', labelKey: 'nav.applications' },
  { to: '/admin/groups', labelKey: 'nav.groups' },
  { to: '/admin/content/profile', labelKey: 'nav.profile' },
  { to: '/admin/content/projects', labelKey: 'nav.projects' },
]

/** The desktop "Administration" menu; highlighted while any admin page is open. */
function AdminMenu() {
  const { t } = useI18n()
  const { pathname } = useLocation()
  const active = pathname.startsWith('/admin')

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          size="sm"
          className={cn(
            'font-normal',
            active ? 'bg-accent text-accent-foreground font-medium' : 'text-muted-foreground hover:text-foreground',
          )}
        >
          {t('nav.administration')}
          <ChevronDown className="opacity-60" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start">
        {adminLinks.map((link) => (
          <DropdownMenuItem key={link.to} asChild className={cn(pathname.startsWith(link.to) && 'font-medium')}>
            <Link to={link.to}>{t(link.labelKey)}</Link>
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

function NavItem({ to, labelKey, end }: NavEntry) {
  const { t } = useI18n()

  return (
    <NavLink
      to={to}
      end={end}
      className={({ isActive }) =>
        cn(
          'rounded-md px-3 py-1.5 text-sm transition-colors',
          isActive
            ? 'bg-accent text-accent-foreground font-medium'
            : 'text-muted-foreground hover:text-foreground',
        )
      }
    >
      {t(labelKey)}
    </NavLink>
  )
}

/**
 * The frame every signed-in route renders inside: a column of header, growing main, footer.
 * `main` is `flex-1`, so on a short page it absorbs the spare height and the footer stays at the bottom.
 * The skip link is first in the DOM so a keyboard user can jump past the navigation.
 */
const currentYear = new Date().getFullYear()

export function AppLayout() {
  const { user, isAdministrator } = useSession()
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const navigate = useNavigate()

  const signOut = useMutation({
    mutationFn: () => authApi.logout(),
    onSuccess: async () => {
      // Clear every cached read before leaving: stale data would otherwise be rendered to the next person
      // who signs in on this device.
      queryClient.clear()
      await navigate('/sign-in', { replace: true })
    },
  })

  return (
    <div className="flex min-h-screen flex-col">
      <a
        href="#main"
        className="bg-primary text-primary-foreground sr-only focus:not-sr-only focus:absolute focus:top-2 focus:left-2 focus:z-50 focus:rounded-md focus:px-4 focus:py-2"
      >
        {t('nav.skip')}
      </a>

      <header className="bg-background/95 sticky top-0 z-40 border-b backdrop-blur">
        <div className="mx-auto flex h-14 max-w-5xl items-center gap-4 px-4">
          <Link to="/" className="flex items-center gap-2 text-base font-semibold tracking-tight">
            <img src="/favicon.svg" alt="" className="size-6" />
            OpenPortal
          </Link>

          <nav aria-label={t('nav.main')} className="hidden flex-1 items-center gap-1 md:flex">
            {userLinks.map((link) => (
              <NavItem key={link.to} {...link} />
            ))}
            {isAdministrator && <AdminMenu />}
          </nav>

          <div className="ml-auto flex items-center gap-1">
            <LanguageSwitcher />
            <ThemeToggle />

            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon" className="md:hidden" aria-label={t('nav.openMenu')}>
                  <Menu />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="md:hidden">
                {userLinks.map((link) => (
                  <DropdownMenuItem key={link.to} asChild>
                    <Link to={link.to}>{t(link.labelKey)}</Link>
                  </DropdownMenuItem>
                ))}
                {isAdministrator && (
                  <>
                    <DropdownMenuSeparator />
                    <DropdownMenuLabel className="text-muted-foreground text-xs font-normal">
                      {t('nav.administration')}
                    </DropdownMenuLabel>
                    {adminLinks.map((link) => (
                      <DropdownMenuItem key={link.to} asChild>
                        <Link to={link.to}>{t(link.labelKey)}</Link>
                      </DropdownMenuItem>
                    ))}
                  </>
                )}
              </DropdownMenuContent>
            </DropdownMenu>

            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="sm" className="max-w-40">
                  <User />
                  <span className="truncate">{user?.displayName ?? user?.email}</span>
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuLabel className="text-muted-foreground font-normal">{user?.email}</DropdownMenuLabel>
                <DropdownMenuSeparator />
                <DropdownMenuItem asChild>
                  <Link to="/account">{t('nav.account')}</Link>
                </DropdownMenuItem>
                <DropdownMenuItem disabled={signOut.isPending} onSelect={() => signOut.mutate()}>
                  <LogOut />
                  {signOut.isPending ? t('nav.signingOut') : t('nav.signOut')}
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </div>
      </header>

      <main id="main" className="mx-auto w-full max-w-5xl flex-1 px-4 py-8">
        <Outlet />
      </main>

      <footer className="border-t">
        <div className="text-muted-foreground mx-auto max-w-5xl px-4 py-4 text-xs">
          OpenPortal · {currentYear}
        </div>
      </footer>
    </div>
  )
}
