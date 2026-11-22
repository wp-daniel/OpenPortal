import { useSession } from '@/hooks/useSession'
import { cn } from '@/lib/utils'

export function BuildVersion({ className }: { className?: string }) {
  const { session } = useSession()
  if (!session.buildVersion) {
    return null
  }

  const [version] = session.buildVersion.split('+', 1)
  return (
    <span
      className={cn('shrink-0 font-mono text-[0.65rem] tracking-normal whitespace-nowrap normal-case tabular-nums', className)}
      title={`OpenPortal v${session.buildVersion}`}
    >
      v{version}
    </span>
  )
}
