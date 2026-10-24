import { api } from './client'

export type AccessReviewStatus = 'open' | 'completed' | 'cancelled'
export type AccessReviewDecision = 'pending' | 'keep' | 'revoke'

export interface AccessReviewSummary {
  readonly id: string
  readonly name: string
  readonly applicationId: string
  readonly applicationName: string
  readonly status: AccessReviewStatus
  readonly itemCount: number
  readonly decidedCount: number
  readonly revokeCount: number
  readonly revokeUndecided: boolean
  readonly createdAtUtc: string
  readonly dueAtUtc: string | null
  readonly closedAtUtc: string | null
}

export interface AccessReviewItem {
  readonly id: string
  readonly subjectType: 'user' | 'group'
  readonly subjectId: string
  readonly subjectLabel: string
  readonly roles: readonly string[]
  readonly decision: AccessReviewDecision
  readonly note: string | null
  readonly decidedAtUtc: string | null
}

export interface AccessReview {
  readonly review: AccessReviewSummary
  readonly items: readonly AccessReviewItem[]
}

export const reviewKeys = {
  all: ['access-reviews'] as const,
  one: (id: string) => ['access-reviews', id] as const,
}

/** Access reviews (recertification): who can open an application, confirmed or revoked by reviewers. */
export const reviewsApi = {
  list: (signal?: AbortSignal) => api.get<AccessReviewSummary[]>('/api/admin/access-reviews', signal),
  get: (id: string, signal?: AbortSignal) => api.get<AccessReview>(`/api/admin/access-reviews/${id}`, signal),
  start: (body: { applicationId: string; name: string | null; dueAtUtc: string | null; revokeUndecided: boolean }) =>
    api.post<AccessReview>('/api/admin/access-reviews', body),
  decide: (id: string, itemId: string, decision: AccessReviewDecision, note?: string) =>
    api.put<AccessReview>(`/api/admin/access-reviews/${id}/items/${itemId}`, { decision, note }),
  complete: (id: string) => api.post<AccessReview>(`/api/admin/access-reviews/${id}/complete`),
  cancel: (id: string) => api.post<AccessReview>(`/api/admin/access-reviews/${id}/cancel`),
}
