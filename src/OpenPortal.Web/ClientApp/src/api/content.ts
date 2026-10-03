import { ApiError, api } from './client'
import type {
  ManagedProject,
  Profile,
  ProfileRequest,
  ProjectRequest,
  SocialLinkRequest,
} from './types'

export const contentAdminApi = {
  /** Returns null when no profile exists yet, so the editor can start from an empty form. */
  profile: async (signal?: AbortSignal): Promise<Profile | null> => {
    try {
      return await api.get<Profile>('/api/manage/content/profile', signal)
    } catch (error) {
      // 404 here means "not created yet", not a failure worth surfacing.
      if (error instanceof ApiError && error.status === 404) {
        return null
      }

      throw error
    }
  },

  saveProfile: (body: ProfileRequest) => api.put<Profile>('/api/manage/content/profile', body),
  listProjects: (signal?: AbortSignal) => api.get<ManagedProject[]>('/api/manage/content/projects', signal),
  saveProject: (body: ProjectRequest) => api.put<ManagedProject>('/api/manage/content/projects', body),
  setPublished: (projectId: string, isPublished: boolean) =>
    api.post<ManagedProject>(`/api/manage/content/projects/${projectId}/published`, { isPublished }),
  deleteProject: (projectId: string) => api.delete<void>(`/api/manage/content/projects/${projectId}`),
}

/** The server replaces the whole set, so the form always submits every row it knows about. */
export function toProfileRequest(form: ProfileForm): ProfileRequest {
  return {
    id: null,
    displayName: form.displayName,
    headline: nullIfEmpty(form.headline),
    summary: nullIfEmpty(form.summary),
    location: nullIfEmpty(form.location),
    email: nullIfEmpty(form.email),
    avatarUrl: nullIfEmpty(form.avatarUrl),
    socialLinks: form.socialLinks.map<SocialLinkRequest>((link) => ({
      platform: link.platform,
      url: link.url,
      label: nullIfEmpty(link.label),
    })),
  }
}

export function toProjectRequest(form: ProjectForm): ProjectRequest {
  return {
    id: form.id,
    name: form.name,
    slug: form.slug,
    summary: nullIfEmpty(form.summary),
    description: nullIfEmpty(form.description),
    url: nullIfEmpty(form.url),
    repositoryUrl: nullIfEmpty(form.repositoryUrl),
    position: form.position === '' ? null : Number(form.position),
    isPublished: form.isPublished,
    startedOn: form.startedOn === '' ? null : form.startedOn,
    completedOn: form.completedOn === '' ? null : form.completedOn,
    technologies: form.technologies
      .split(',')
      .map((name) => name.trim())
      .filter((name) => name.length > 0),
  }
}

/** Sends an empty optional field as null rather than as "", which the server would reject as a value. */
function nullIfEmpty(value: string): string | null {
  const trimmed = value.trim()

  return trimmed.length === 0 ? null : trimmed
}

export interface SocialLinkForm {
  platform: string
  url: string
  label: string
}

export interface ProfileForm {
  displayName: string
  headline: string
  summary: string
  location: string
  email: string
  avatarUrl: string
  socialLinks: SocialLinkForm[]
}

export interface ProjectForm {
  id: string | null
  name: string
  slug: string
  summary: string
  description: string
  url: string
  repositoryUrl: string
  position: string
  isPublished: boolean
  startedOn: string
  completedOn: string
  technologies: string
}

export function profileToForm(profile: Profile | null): ProfileForm {
  return {
    displayName: profile?.displayName ?? '',
    headline: profile?.headline ?? '',
    summary: profile?.summary ?? '',
    location: profile?.location ?? '',
    email: profile?.email ?? '',
    avatarUrl: profile?.avatarUrl ?? '',
    socialLinks: (profile?.socialLinks ?? []).map((link) => ({
      platform: link.platform,
      url: link.url,
      label: link.label ?? '',
    })),
  }
}

export function projectToForm(project: ManagedProject | null): ProjectForm {
  return {
    id: project?.id ?? null,
    name: project?.name ?? '',
    slug: project?.slug ?? '',
    summary: project?.summary ?? '',
    description: project?.description ?? '',
    url: project?.url ?? '',
    repositoryUrl: project?.repositoryUrl ?? '',
    position: project?.position === null || project?.position === undefined ? '' : `${project.position}`,
    isPublished: project?.isPublished ?? false,
    startedOn: project?.startedOn ?? '',
    completedOn: project?.completedOn ?? '',
    technologies: (project?.technologies ?? []).map((technology) => technology.name).join(', '),
  }
}