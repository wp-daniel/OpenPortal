import { Outlet } from 'react-router-dom'
import { BrandMark } from '@/components/BrandMark'
import { LanguageSwitcher } from '@/components/LanguageSwitcher'
import { MagneticCursor } from '@/components/MagneticCursor'
import { StarField } from '@/components/StarField'
import { ThemeToggle } from '@/components/ThemeToggle'

const currentYear = new Date().getFullYear()

/**
 * Chrome-free frame for the sign-in screen: starfield background, a magnetic cursor, a quiet brand header, the form, a small
 * footer and the language selector and theme toggle top-right.
 */
export function AuthLayout() {
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

      <main id="main" className="relative z-10 mx-auto flex w-full max-w-md flex-1 flex-col justify-center px-4 py-8">
        <Outlet />
      </main>

      <footer className="text-muted-foreground relative z-10 px-6 py-5 text-center text-[0.65rem] tracking-[0.25em] uppercase">
        OpenPortal · {currentYear}
      </footer>
    </div>
  )
}
