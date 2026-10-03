import type { ReactNode } from 'react'
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

/** A titled card: the standard container for a block of content or a form. */
export function Section({
  title,
  description,
  actions,
  className,
  children,
}: {
  title?: ReactNode
  description?: ReactNode
  actions?: ReactNode
  className?: string
  children?: ReactNode
}) {
  return (
    <Card className={className}>
      {(title || description || actions) && (
        <CardHeader>
          {title && <CardTitle>{title}</CardTitle>}
          {description && <CardDescription>{description}</CardDescription>}
          {actions && <CardAction>{actions}</CardAction>}
        </CardHeader>
      )}
      {children && <CardContent>{children}</CardContent>}
    </Card>
  )
}
