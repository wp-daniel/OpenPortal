import { Outlet } from 'react-router-dom'
import { AppBreadcrumbs } from '@/components/AppBreadcrumbs'
import { AppSidebar } from '@/components/AppSidebar'
import { LanguageSwitcher } from '@/components/LanguageSwitcher'
import { ThemeToggle } from '@/components/ThemeToggle'
import { Separator } from '@/components/ui/separator'
import { SidebarInset, SidebarProvider, SidebarTrigger } from '@/components/ui/sidebar'
import { useI18n } from '@/i18n/useI18n'

const currentYear = new Date().getFullYear()

/**
 * The sidebar records its expanded/collapsed state in the `sidebar_state` cookie but never reads it back
 * (shadcn leaves that to the app), so the layout starts from it to keep the choice across reloads.
 */
function sidebarStartsOpen(): boolean {
  return !document.cookie.split('; ').includes('sidebar_state=false')
}

/**
 * The frame every signed-in route renders inside: the navigation sidebar beside a column of header (sidebar
 * toggle, breadcrumbs, language and theme), growing main and footer. `main` is `flex-1`, so on a short page
 * it absorbs the spare height and the footer stays at the bottom. The skip link is first in the DOM so a
 * keyboard user can jump past the navigation.
 */
export function AppLayout() {
  const { t } = useI18n()

  return (
    <SidebarProvider defaultOpen={sidebarStartsOpen()}>
      <a
        href="#main"
        className="bg-primary text-primary-foreground sr-only focus:not-sr-only focus:absolute focus:top-2 focus:left-2 focus:z-50 focus:rounded-md focus:px-4 focus:py-2"
      >
        {t('nav.skip')}
      </a>

      <AppSidebar />

      <SidebarInset>
        <header className="bg-background/95 sticky top-0 z-30 flex h-14 shrink-0 items-center gap-2 border-b px-4 backdrop-blur">
          <SidebarTrigger className="-ml-1" aria-label={t('nav.toggleSidebar')} />
          <Separator orientation="vertical" className="mr-2 data-[orientation=vertical]:h-4" />
          <AppBreadcrumbs />
          <div className="ml-auto flex shrink-0 items-center gap-1">
            <LanguageSwitcher />
            <ThemeToggle />
          </div>
        </header>

        <main id="main" className="mx-auto w-full max-w-6xl flex-1 px-4 py-8 md:px-6">
          <Outlet />
        </main>

        <footer className="border-t">
          <div className="text-muted-foreground mx-auto max-w-6xl px-4 py-4 text-xs md:px-6">
            OpenPortal · {currentYear}
          </div>
        </footer>
      </SidebarInset>
    </SidebarProvider>
  )
}
