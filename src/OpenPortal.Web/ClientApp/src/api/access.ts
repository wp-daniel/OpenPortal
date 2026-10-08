import { api } from './client'
import type {
  AccessTree,
  ApplicationSecret,
  ApplicationSummary,
  CreateApplicationRequest,
  GroupDetail,
  GroupSummary,
  LaunchpadItem,
  PagePermissionMatrix,
  SaveGroupRequest,
  UpdateApplicationRequest,
  UserAccess,
} from './types'

/** Applications that sign in through the portal (administrators and holders of the matching page). */
export const applicationsApi = {
  list: (signal?: AbortSignal) => api.get<ApplicationSummary[]>('/api/admin/applications', signal),

  create: (body: CreateApplicationRequest) => api.post<ApplicationSecret>('/api/admin/applications', body),

  update: (id: string, body: UpdateApplicationRequest) =>
    api.put<ApplicationSummary>(`/api/admin/applications/${id}`, body),

  approve: (id: string) => api.post<ApplicationSecret>(`/api/admin/applications/${id}/approve`, {}),

  regenerateSecret: (id: string) => api.post<ApplicationSecret>(`/api/admin/applications/${id}/secret`, {}),

  applyManifest: (id: string) => api.post<ApplicationSummary>(`/api/admin/applications/${id}/apply-manifest`, {}),

  disable: (id: string) => api.post<ApplicationSummary>(`/api/admin/applications/${id}/disable`, {}),

  enable: (id: string) => api.post<ApplicationSummary>(`/api/admin/applications/${id}/enable`, {}),

  remove: (id: string) => api.delete<void>(`/api/admin/applications/${id}`),
}

/** User groups (administrators and holders of the matching page). */
export const groupsApi = {
  list: (signal?: AbortSignal) => api.get<GroupSummary[]>('/api/admin/groups', signal),

  get: (id: string, signal?: AbortSignal) => api.get<GroupDetail>(`/api/admin/groups/${id}`, signal),

  create: (body: SaveGroupRequest) => api.post<GroupDetail>('/api/admin/groups', body),

  update: (id: string, body: SaveGroupRequest) => api.put<GroupDetail>(`/api/admin/groups/${id}`, body),

  remove: (id: string) => api.delete<void>(`/api/admin/groups/${id}`),

  addMember: (id: string, userId: string) => api.put<GroupDetail>(`/api/admin/groups/${id}/members/${userId}`, {}),

  removeMember: (id: string, userId: string) => api.delete<GroupDetail>(`/api/admin/groups/${id}/members/${userId}`),
}

/** Grants: who may open which application (administrators and holders of the access page). Every call is idempotent. */
export const accessApi = {
  tree: (signal?: AbortSignal) => api.get<AccessTree>('/api/admin/access/tree', signal),

  user: (userId: string, signal?: AbortSignal) => api.get<UserAccess>(`/api/admin/access/users/${userId}`, signal),

  /** Without `roles` an existing grant keeps its roles; with them, the grant carries exactly those. */
  grantUser: (applicationId: string, userId: string, roles?: readonly string[]) =>
    api.put<void>(`/api/admin/access/applications/${applicationId}/users/${userId}`, roles ? { roles } : {}),

  revokeUser: (applicationId: string, userId: string) =>
    api.delete<void>(`/api/admin/access/applications/${applicationId}/users/${userId}`),

  grantGroup: (applicationId: string, groupId: string, roles?: readonly string[]) =>
    api.put<void>(`/api/admin/access/applications/${applicationId}/groups/${groupId}`, roles ? { roles } : {}),

  revokeGroup: (applicationId: string, groupId: string) =>
    api.delete<void>(`/api/admin/access/applications/${applicationId}/groups/${groupId}`),
}

/** Which groups may open which portal pages (administrators only). Every call is idempotent. */
export const pagePermissionsApi = {
  matrix: (signal?: AbortSignal) => api.get<PagePermissionMatrix>('/api/admin/page-permissions', signal),

  grant: (pageKey: string, groupId: string) =>
    api.put<void>(`/api/admin/page-permissions/${encodeURIComponent(pageKey)}/groups/${groupId}`, {}),

  revoke: (pageKey: string, groupId: string) =>
    api.delete<void>(`/api/admin/page-permissions/${encodeURIComponent(pageKey)}/groups/${groupId}`),

  /** Replaces every page of the group in one change (a whole area at once). */
  setGroupPages: (groupId: string, pages: readonly string[]) =>
    api.put<void>(`/api/admin/page-permissions/groups/${groupId}`, { pages }),
}

/** The signed-in user's own applications. */
export const launchpadApi = {
  list: (signal?: AbortSignal) => api.get<LaunchpadItem[]>('/api/account/applications', signal),
}

/** Query keys, shared so a change in one page refreshes the others. */
export const accessKeys = {
  applications: ['admin-applications'] as const,
  groups: ['admin-groups'] as const,
  group: (id: string) => ['admin-groups', id] as const,
  tree: ['admin-access-tree'] as const,
  user: (userId: string) => ['admin-access-user', userId] as const,
  launchpad: ['launchpad'] as const,
  pagePermissions: ['admin-page-permissions'] as const,
}
