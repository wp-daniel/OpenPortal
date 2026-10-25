import { api, invalidateAntiforgeryToken, withQuery } from './client'
import type {
  CreateInvitationRequest,
  EmailChangeConfirmed,
  Invitation,
  InvitationIssued,
  InvitationPreview,
  PagedResult,
  RegisterRequest,
  RegistrationResult,
  Session,
} from './types'

/** Creating an account without an administrator: registering, confirming the address, accepting an invitation. */
export const registrationApi = {
  /** May sign the caller in (outcome `signed_in`), so the antiforgery token is dropped either way. */
  async register(body: RegisterRequest): Promise<RegistrationResult> {
    try {
      return await api.post<RegistrationResult>('/api/auth/register', body)
    } finally {
      invalidateAntiforgeryToken()
    }
  },

  confirmEmail: (userId: string, token: string) =>
    api.post<RegistrationResult>('/api/auth/confirm-email', { userId, token }),

  /** Opens the link sent to a new address. The account's own session is re-issued, so the token goes. */
  async confirmEmailChange(userId: string, token: string): Promise<EmailChangeConfirmed> {
    try {
      return await api.post<EmailChangeConfirmed>('/api/auth/email-change/confirm', { userId, token })
    } finally {
      invalidateAntiforgeryToken()
    }
  },

  /** Always succeeds, whatever the address. */
  resendConfirmation: (email: string) => api.post<void>('/api/auth/confirm-email/resend', { email }),

  /** Always succeeds (204) whatever the address; 409 only when the portal cannot send email. */
  forgotPassword: (email: string) => api.post<void>('/api/auth/password/forgot', { email }),

  /** Sets a new password from the reset link; the account is signed out everywhere. */
  async resetPassword(userId: string, token: string, newPassword: string): Promise<void> {
    try {
      await api.post<void>('/api/auth/password/reset', { userId, token, newPassword })
    } finally {
      invalidateAntiforgeryToken()
    }
  },

  /** POST, so the token stays out of URLs the server would log. */
  previewInvitation: (token: string) => api.post<InvitationPreview>('/api/auth/invitations/preview', { token }),

  /** Creates the account and signs the caller in as it. */
  async acceptInvitation(body: {
    token: string
    password: string
    firstName: string
    lastName: string
    acceptedLegalDocumentIds: readonly string[]
  }): Promise<Session> {
    try {
      return await api.post<Session>('/api/auth/invitations/accept', body)
    } finally {
      invalidateAntiforgeryToken()
    }
  },
}

/** Invitations by email (administrators and holders of the users page). */
export const invitationsApi = {
  list: (params: { page: number; pageSize: number; status?: string; search?: string }, signal?: AbortSignal) =>
    api.get<PagedResult<Invitation>>(
      withQuery('/api/admin/invitations', {
        page: params.page,
        pageSize: params.pageSize,
        status: params.status,
        search: params.search,
      }),
      signal,
    ),

  create: (body: CreateInvitationRequest) => api.post<InvitationIssued>('/api/admin/invitations', body),

  /** A new link (the old one stops working) and a fresh validity period. */
  resend: (id: string) => api.post<InvitationIssued>(`/api/admin/invitations/${id}/resend`),

  revoke: (id: string) => api.delete<void>(`/api/admin/invitations/${id}`),
}

export const invitationKeys = {
  all: ['admin-invitations'] as const,
}

/** The error codes the sign-in page explains with more than "wrong email or password". */
export const ACCOUNT_PENDING_APPROVAL = 'identity.account_pending_approval'
export const EMAIL_NOT_CONFIRMED = 'identity.email_not_confirmed'
