import type { UserApplicationAccess } from '@/api/types'
import { Badge } from '@/components/ui/badge'
import { formatDate, formatList } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { RoleBadges } from './RoleAssignment'

export function AccessExplanation({ access }: { access: UserApplicationAccess }) {
  const { t } = useI18n()

  return (
    <div className="grid gap-1.5">
      <div>
        <Badge variant={access.outcome === 'granted' ? 'success' : 'secondary'}>
          {t(`access.effective.${access.outcome}`)}
        </Badge>
      </div>
      <ul className="text-muted-foreground grid gap-0.5 text-xs">
        {access.sources.map((source) => (
          <li key={source.groupPath.at(-1)?.id ?? 'direct'} className="flex flex-wrap items-baseline gap-x-2">
            <span className="wrap-anywhere">
              {source.groupPath.length === 0
                ? t('access.user.direct')
                : t('access.user.viaGroup', { name: source.groupPath.map((group) => group.name).join(' → ') })}
            </span>
            {source.expiresAtUtc && (
              <span>
                {t(source.active ? 'access.expiry.badge' : 'access.effective.ended', {
                  date: formatDate(source.expiresAtUtc, { dateStyle: 'short', timeStyle: 'short' }),
                })}
              </span>
            )}
          </li>
        ))}
      </ul>
      {access.organizations.length > 0 && (
        <p className="text-muted-foreground text-xs">
          {t('access.effective.organizations', { names: formatList(access.organizations) })}
          {access.organizationRequiresMultiFactor && ` · ${t('access.effective.organizationMfa')}`}
        </p>
      )}
      {access.roles.length > 0 && (
        <div className="flex flex-wrap items-center gap-1">
          <span className="text-muted-foreground text-xs">{t('access.user.roles')}</span>
          <RoleBadges roles={access.roleCatalog} keys={access.roles} />
        </div>
      )}
    </div>
  )
}
