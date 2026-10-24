import { api } from './client'
import type { OrganizationDetail, OrganizationMembership, OrganizationRole, OrganizationSummary } from './types'

/** Organizations (tenants) and their members (administrators and holders of the organizations page). */
export const organizationsApi = {
  list: (signal?: AbortSignal) => api.get<OrganizationSummary[]>('/api/admin/organizations', signal),

  get: (id: string, signal?: AbortSignal) => api.get<OrganizationDetail>(`/api/admin/organizations/${id}`, signal),

  create: (body: { name: string; slug: string; description: string | null }) =>
    api.post<OrganizationDetail>('/api/admin/organizations', body),

  /** The short name (slug) cannot change. */
  update: (id: string, body: { name: string; description: string | null }) =>
    api.put<OrganizationDetail>(`/api/admin/organizations/${id}`, body),

  remove: (id: string) => api.delete<void>(`/api/admin/organizations/${id}`),

  /** Adds a member, or changes their role. Idempotent. */
  setMember: (id: string, userId: string, role: OrganizationRole) =>
    api.put<OrganizationDetail>(`/api/admin/organizations/${id}/members/${userId}`, { role }),

  removeMember: (id: string, userId: string) =>
    api.delete<OrganizationDetail>(`/api/admin/organizations/${id}/members/${userId}`),

  forUser: (userId: string, signal?: AbortSignal) =>
    api.get<OrganizationMembership[]>(`/api/admin/organizations/users/${userId}`, signal),
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
}

export const organizationKeys = {
  all: ['admin-organizations'] as const,
  detail: (id: string) => ['admin-organizations', id] as const,
  forUser: (userId: string) => ['admin-organizations', 'user', userId] as const,
  mine: ['my-organizations'] as const,
}
