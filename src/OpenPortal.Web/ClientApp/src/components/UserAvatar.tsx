import { avatarUrl } from '@/api/users'
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar'
import { cn } from '@/lib/utils'

export interface AvatarUser {
  readonly id: string
  readonly displayName: string
  readonly avatarUpdatedAtUtc: string | null
}

/** Up to two initials from the display name ("Ada Lovelace" → "AL"), for when there is no picture. */
function initialsOf(displayName: string): string {
  const letters = displayName
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .map((word) => Array.from(word)[0] ?? '')

  return (letters.length > 1 ? letters[0] + letters[letters.length - 1] : (letters[0] ?? '?')).toUpperCase()
}

/**
 * A person's picture, or their initials while it loads and when there is none. `src` overrides the server
 * URL, which lets an upload preview show before the session has been refreshed.
 */
export function UserAvatar({
  user,
  src,
  className,
}: {
  user: AvatarUser
  src?: string | null
  className?: string
}) {
  const url = src === undefined ? avatarUrl(user) : src

  return (
    <Avatar className={cn('size-8', className)}>
      {url && <AvatarImage src={url} alt="" className="object-cover" />}
      <AvatarFallback className="text-xs font-medium">{initialsOf(user.displayName)}</AvatarFallback>
    </Avatar>
  )
}
