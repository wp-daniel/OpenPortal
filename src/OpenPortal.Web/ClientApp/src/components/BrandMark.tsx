import { cn } from '@/lib/utils'

/**
 * The OpenPortal symbol, black on the light theme and white on the dark one. Both images are in the page and the
 * `dark` class picks one, so it follows the app's theme rather than the OS preference the favicon follows.
 */
export function BrandMark({ className }: { className?: string }) {
  return (
    <>
      <img src="/brand/openportal-symbol.svg" alt="" className={cn('object-contain dark:hidden', className)} />
      <img src="/brand/openportal-symbol-white.svg" alt="" className={cn('hidden object-contain dark:block', className)} />
    </>
  )
}
