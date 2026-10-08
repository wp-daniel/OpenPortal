import { useMutation } from '@tanstack/react-query'
import { Loader2, Tags } from 'lucide-react'
import { useId } from 'react'
import type { ApplicationRole } from '@/api/types'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Label } from '@/components/ui/label'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { useI18n } from '@/i18n/useI18n'

/** The name an administrator gave a role, or its key. */
function roleName(roles: readonly ApplicationRole[], key: string): string {
  return roles.find((role) => role.key === key)?.displayName ?? key
}

/** The role keys as badges, named from the application's roles. */
export function RoleBadges({ roles, keys }: { roles: readonly ApplicationRole[]; keys: readonly string[] }) {
  if (keys.length === 0) {
    return null
  }

  return (
    <span className="flex flex-wrap gap-1">
      {keys.map((key) => (
        <Badge key={key} variant="outline" className="font-normal">
          {roleName(roles, key)}
        </Badge>
      ))}
    </span>
  )
}

/** A checklist of the application's roles, for choosing the ones that come with a grant. */
export function RoleChecklist({
  roles,
  value,
  onChange,
  idPrefix,
  disabled,
}: {
  roles: readonly ApplicationRole[]
  value: readonly string[]
  onChange: (next: string[]) => void
  idPrefix: string
  disabled?: boolean
}) {
  const toggle = (key: string, checked: boolean) =>
    onChange(checked ? [...value, key].sort() : value.filter((entry) => entry !== key))

  return (
    <ul className="grid gap-2">
      {roles.map((role) => {
        const id = `${idPrefix}-${role.key}`

        return (
          <li key={role.key} className="flex items-start gap-2">
            <Checkbox
              id={id}
              checked={value.includes(role.key)}
              disabled={disabled}
              onCheckedChange={(checked) => toggle(role.key, checked === true)}
              className="mt-0.5"
            />
            <div className="grid gap-0.5">
              <Label htmlFor={id}>{role.displayName ?? role.key}</Label>
              <span className="text-muted-foreground font-mono text-xs">{role.key}</span>
              {role.description && <span className="text-muted-foreground text-xs">{role.description}</span>}
            </div>
          </li>
        )
      })}
    </ul>
  )
}

/**
 * The roles that come with one grant: shown as badges, changed from a popover. Each change is saved at once,
 * and the application sees it at its next token refresh. Hidden when the application defines no roles.
 */
export function RoleAssignment({
  roles,
  value,
  label,
  save,
  onSaved,
}: {
  /** The application's roles. */
  roles: readonly ApplicationRole[]
  /** Role keys on the grant. */
  value: readonly string[]
  /** Who the grant is for, for the accessible name. */
  label: string
  save: (next: string[]) => Promise<void>
  /** Refreshes what shows the grant; awaited, so the checklist stays disabled until the new roles are on screen. */
  onSaved: () => Promise<unknown>
}) {
  const { t } = useI18n()
  const id = useId()

  const change = useMutation({
    mutationFn: save,
    onSuccess: onSaved,
  })

  if (roles.length === 0) {
    return null
  }

  return (
    <div className="flex min-w-0 items-center gap-1">
      <RoleBadges roles={roles} keys={value} />
      <Popover>
        <PopoverTrigger asChild>
          <Button variant="ghost" size="sm" aria-label={t('access.roles.editNamed', { name: label })} title={t('access.roles.edit')}>
            {change.isPending ? <Loader2 className="animate-spin" /> : <Tags />}
            {value.length === 0 && <span className="text-muted-foreground text-xs">{t('access.roles.none')}</span>}
          </Button>
        </PopoverTrigger>
        <PopoverContent align="end" className="w-72">
          <div className="grid gap-3">
            <div className="grid gap-1">
              <h4 className="text-sm font-medium">{t('access.roles.title')}</h4>
              <p className="text-muted-foreground text-xs">{t('access.roles.hint', { name: label })}</p>
            </div>
            <RoleChecklist
              roles={roles}
              value={value}
              idPrefix={id}
              disabled={change.isPending}
              onChange={(next) => change.mutate(next)}
            />
          </div>
        </PopoverContent>
      </Popover>
    </div>
  )
}
