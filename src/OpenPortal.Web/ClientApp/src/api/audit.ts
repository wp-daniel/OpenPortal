import { api, withQuery } from './client'
import type { AuditEntry, AuditFilter, PagedResult } from './types'

const filterParams = (filter: AuditFilter) => ({
  search: filter.search,
  category: filter.category,
  outcome: filter.outcome,
  subject: filter.subject,
  from: filter.from,
  to: filter.to,
})

/** The security audit log (administrators and holders of the audit page). Read-only. */
export const auditApi = {
  list: (params: AuditFilter & { page: number; pageSize: number }, signal?: AbortSignal) =>
    api.get<PagedResult<AuditEntry>>(
      withQuery('/api/admin/audit', { page: params.page, pageSize: params.pageSize, ...filterParams(params) }),
      signal,
    ),

  /** Where the CSV export of the same filter is downloaded from (a plain link: the session cookie goes along). */
  exportUrl: (filter: AuditFilter) => withQuery('/api/admin/audit/export', filterParams(filter)),
}

/** The action categories, as the server prefixes the action codes (`user.created` is in `user`). */
export const AUDIT_CATEGORIES = ['auth', 'account', 'user', 'group', 'access', 'pages', 'application', 'oidc', 'content', 'settings'] as const
