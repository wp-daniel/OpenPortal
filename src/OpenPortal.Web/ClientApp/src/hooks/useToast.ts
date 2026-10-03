import { toast } from 'sonner'
import { describeError } from '@/lib/errors'

/**
 * The only way application code shows a message to the user. Nothing outside this file imports `sonner`, so
 * position, duration and styling stay a single decision made in <Toaster> (components/ui/sonner.tsx).
 */
export const notify = {
  success: (message: string, description?: string) => toast.success(message, { description }),
  error: (message: string, description?: string) => toast.error(message, { description }),
  info: (message: string, description?: string) => toast.info(message, { description }),
  warning: (message: string, description?: string) => toast.warning(message, { description }),
  /** Shows a caught error, using the server's problem detail when there is one. */
  fromError: (error: unknown, fallback = 'Something went wrong') =>
    toast.error(fallback, { description: describeError(error) }),
}

/** Hook form of {@link notify}, for components. The object is module-level, so it is referentially stable. */
export function useToast() {
  return notify
}
