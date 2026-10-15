import { ApiError, api } from './client'
import type { LegalDocument, LegalDocumentKind, PendingLegalDocument, PublishedLegalDocument, SaveLegalDocument } from './types'

/** The privacy notice and the terms of use: the versions in force, accepting them, and writing new ones. */
export const legalApi = {
  /** The version in force in the reader's language, or null when none is published. */
  current: async (kind: LegalDocumentKind, signal?: AbortSignal): Promise<PublishedLegalDocument | null> => {
    try {
      return await api.get<PublishedLegalDocument>(`/api/legal/${kind}`, signal)
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) {
        return null
      }
      throw error
    }
  },

  /** Accepts versions in force for the signed-in account; returns what is still pending. */
  accept: (documentIds: readonly string[]) =>
    api.post<PendingLegalDocument[]>('/api/account/legal/accept', { documentIds }),

  list: (signal?: AbortSignal) => api.get<LegalDocument[]>('/api/admin/legal', signal),

  createDraft: (kind: LegalDocumentKind, body: SaveLegalDocument) => api.post<LegalDocument>(`/api/admin/legal/${kind}`, body),

  updateDraft: (id: string, body: SaveLegalDocument) => api.put<LegalDocument>(`/api/admin/legal/${id}`, body),

  publish: (id: string) => api.post<LegalDocument>(`/api/admin/legal/${id}/publish`),

  removeDraft: (id: string) => api.delete<void>(`/api/admin/legal/${id}`),
}

export const legalKeys = {
  all: ['legal'] as const,
  current: (kind: LegalDocumentKind, language: string) => ['legal', 'current', kind, language] as const,
  admin: ['legal', 'admin'] as const,
}
