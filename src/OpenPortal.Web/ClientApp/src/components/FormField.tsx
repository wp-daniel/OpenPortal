import { cloneElement, isValidElement, type ReactElement, type ReactNode } from 'react'
import { Label } from '@/components/ui/label'
import { cn } from '@/lib/utils'

/**
 * Label + control + hint + error, laid out the same way everywhere.
 *
 * The control is cloned with `id`, `aria-invalid` and `aria-describedby`, so a field cannot forget to be
 * wired to its message. The hint and error share one slot below the control, which keeps a row of fields
 * aligned at the top whether or not any of them is currently showing an error.
 */
export function FormField({
  label,
  htmlFor,
  hint,
  error,
  className,
  children,
}: {
  label: ReactNode
  htmlFor: string
  hint?: ReactNode
  error?: string | undefined
  className?: string
  children: ReactElement<Record<string, unknown>>
}) {
  const describedBy = error ? `${htmlFor}-error` : hint ? `${htmlFor}-hint` : undefined

  return (
    <div className={cn('grid content-start gap-2', className)}>
      <Label htmlFor={htmlFor}>{label}</Label>
      {isValidElement(children)
        ? cloneElement(children, {
            id: htmlFor,
            'aria-invalid': error ? true : undefined,
            'aria-describedby': describedBy,
          })
        : children}
      {error ? (
        <p id={`${htmlFor}-error`} className="text-destructive text-xs font-medium">
          {error}
        </p>
      ) : hint ? (
        <p id={`${htmlFor}-hint`} className="text-muted-foreground text-xs">
          {hint}
        </p>
      ) : null}
    </div>
  )
}
