import { api, withQuery } from './client'
import type { InvitationIssued, OrganizationDetail, OrganizationMembership, OrganizationRole, OrganizationSummary } from './types'

/** The domain and per-application role operations, the same for the portal's screens and an organization's own administrators. */
export interface OrganizationTenancyApi {
  addDomain: (id: string, domain: string) => Promise<OrganizationDetail>
  verifyDomain: (id: string, domainId: string, manual?: boolean) => Promise<OrganizationDetail>
  updateDomain: (id: string, domainId: string, autoJoin: boolean, role: OrganizationRole) => Promise<OrganizationDetail>
  removeDomain: (id: string, domainId: string) => Promise<OrganizationDetail>
  setMemberApplicationRoles: (id: string, userId: string, applicationId: string, roles: readonly string[]) => Promise<OrganizationDetail>
}

function tenancyApi(base: string): OrganizationTenancyApi {
  return {
    addDomain: (id, domain) => api.post<OrganizationDetail>(`${base}/${id}/domains`, { domain }),
    verifyDomain: (id, domainId, manual = false) =>
      api.post<OrganizationDetail>(`${base}/${id}/domains/${domainId}/verify`, base.includes('/admin/') ? { manual } : undefined),
    updateDomain: (id, domainId, autoJoin, role) => api.put<OrganizationDetail>(`${base}/${id}/domains/${domainId}`, { autoJoin, role }),
    removeDomain: (id, domainId) => api.delete<OrganizationDetail>(`${base}/${id}/domains/${domainId}`),
    setMemberApplicationRoles: (id, userId, applicationId, roles) =>
      api.put<OrganizationDetail>(`${base}/${id}/members/${userId}/applications/${applicationId}`, { roles }),
  }
}

/** Organizations (tenants) and their members (administrators and holders of the organizations page). */
export const organizationsApi = {
  list: (signal?: AbortSignal) => api.get<OrganizationSummary[]>('/api/admin/organizations', signal),

  /** Organizations whose name or short name matches, a page at a time. */
  slice: (params: { search?: string; skip: number; take: number }, signal?: AbortSignal) =>
    api.getSlice<OrganizationSummary>(withQuery('/api/admin/organizations', params), signal),

  get: (id: string, signal?: AbortSignal) => api.get<OrganizationDetail>(`/api/admin/organizations/${id}`, signal),

  create: (body: { name: string; slug: string; description: string | null }) =>
    api.post<OrganizationDetail>('/api/admin/organizations', body),

  /** The short name (slug) cannot change. */
  update: (id: string, body: { name: string; description: string | null; requireMultiFactor?: boolean }) =>
    api.put<OrganizationDetail>(`/api/admin/organizations/${id}`, body),

  remove: (id: string) => api.delete<void>(`/api/admin/organizations/${id}`),

  /** Adds a member, or changes their role. Idempotent. */
  setMember: (id: string, userId: string, role: OrganizationRole) =>
    api.put<OrganizationDetail>(`/api/admin/organizations/${id}/members/${userId}`, { role }),

  removeMember: (id: string, userId: string) =>
    api.delete<OrganizationDetail>(`/api/admin/organizations/${id}/members/${userId}`),

  forUser: (userId: string, signal?: AbortSignal) =>
    api.get<OrganizationMembership[]>(`/api/admin/organizations/users/${userId}`, signal),

  ...tenancyApi('/api/admin/organizations'),
}

/** The organizations the signed-in user belongs to. */
export const myOrganizationsApi = {
  list: (signal?: AbortSignal) => api.get<OrganizationMembership[]>('/api/account/organizations', signal),

  /** For the organization's own administrators: its members, managed from their account. */
  get: (id: string, signal?: AbortSignal) => api.get<OrganizationDetail>(`/api/account/organizations/${id}`, signal),

  /** Adds an existing account by its address. */
  addMember: (id: string, email: string, role: OrganizationRole) =>
    api.post<OrganizationDetail>(`/api/account/organizations/${id}/members`, { email, role }),

  setMember: (id: string, userId: string, role: OrganizationRole) =>
    api.put<OrganizationDetail>(`/api/account/organizations/${id}/members/${userId}`, { role }),

  removeMember: (id: string, userId: string) => api.delete<OrganizationDetail>(`/api/account/organizations/${id}/members/${userId}`),

  /** Adds the account with this address, or invites the address when it has none. */
  invite: (id: string, body: { email: string; role: OrganizationRole }) =>
    api.post<{ outcome: 'added' | 'invited'; organization: OrganizationDetail | null; invitation: InvitationIssued | null }>(
      `/api/account/organizations/${id}/invitations`,
      body,
    ),

  ...tenancyApi('/api/account/organizations'),
}

export const organizationKeys = {
  all: ['admin-organizations'] as const,
  detail: (id: string) => ['admin-organizations', id] as const,
  forUser: (userId: string) => ['admin-organizations', 'user', userId] as const,
  mine: ['my-organizations'] as const,
}
