import { Outlet } from 'react-router-dom'
import { AnnouncementBanner } from '@/components/AnnouncementBanner'
import { BrandMark } from '@/components/BrandMark'
import { LanguageSwitcher } from '@/components/LanguageSwitcher'
import { MagneticCursor } from '@/components/MagneticCursor'
import { StarField } from '@/components/StarField'
import { LegalLinks } from '@/components/LegalLinks'
import { ThemeToggle } from '@/components/ThemeToggle'
import { cn } from '@/lib/utils'

const currentYear = new Date().getFullYear()

/**
 * Chrome-free frame for the sign-in screen: starfield background, a magnetic cursor, a quiet brand header, the form, a small
 * footer and the language selector and theme toggle top-right.
 */
export function AuthLayout({ wide = false }: { wide?: boolean }) {
  return (
    <div className="relative flex min-h-screen flex-col overflow-hidden">
      <StarField />
      <MagneticCursor />

      <header className="relative z-10 flex items-center justify-between px-6 py-5">
        <div className="flex items-center gap-2.5">
          <BrandMark className="size-6" />
          <span className="text-xs font-medium tracking-[0.25em] uppercase">OpenPortal</span>
        </div>
        <div className="flex items-center gap-1">
          <LanguageSwitcher />
          <ThemeToggle />
        </div>
      </header>

      <AnnouncementBanner className="relative z-10 mx-auto w-full max-w-xl px-4" />

      <main
        id="main"
        className={cn(
          'relative z-10 mx-auto flex w-full flex-1 flex-col justify-center px-4 py-8',
          wide ? 'max-w-3xl' : 'max-w-md',
        )}
      >
        <Outlet />
      </main>

      <footer className="text-muted-foreground relative z-10 flex flex-wrap items-center justify-center gap-x-4 gap-y-1 px-6 py-5 text-center text-[0.65rem] tracking-[0.25em] uppercase">
        <span>OpenPortal · {currentYear}</span>
        <LegalLinks />
      </footer>
    </div>
  )
}
