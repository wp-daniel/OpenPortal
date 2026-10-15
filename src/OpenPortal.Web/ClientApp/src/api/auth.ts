import { api, invalidateAntiforgeryToken } from './client'
import type {
  AccountDeletion,
  CreatedPersonalAccessToken,
  PersonalAccessToken,
  PersonalAccessTokenScope,
  AccountProfile,
  ChangePasswordRequest,
  RecoveryCodes,
  Session,
  TwoFactorLoginRequest,
  TwoFactorSetup,
  TwoFactorStatus,
  UpdateProfileRequest,
} from './types'
import { avatarForm } from './users'

export const authApi = {
  session: (signal?: AbortSignal) => api.get<Session>('/api/auth/session', signal),

  /**
   * Signing in changes the identity the antiforgery token is bound to, so the cached token is discarded on
   * both success and failure. Keeping the pre-sign-in token would make the first write after sign-in fail.
   */
  async login(email: string, password: string, rememberMe: boolean): Promise<Session> {
    try {
      return await api.post<Session>('/api/auth/login', { email, password, rememberMe })
    } finally {
      invalidateAntiforgeryToken()
    }
  },

  /** The second sign-in step, after `login` failed with `identity.two_factor_required`. */
  async loginTwoFactor(body: TwoFactorLoginRequest): Promise<Session> {
    try {
      return await api.post<Session>('/api/auth/login/two-factor', body)
    } finally {
      invalidateAntiforgeryToken()
    }
  },

  async logout(): Promise<void> {
    try {
      await api.post<void>('/api/auth/logout')
    } finally {
      invalidateAntiforgeryToken()
    }
  },

  /** Starts a support session as another account (administrators only). The identity changes: a new token follows. */
  async impersonate(userId: string, reason: string): Promise<void> {
    try {
      await api.post<void>(`/api/admin/users/${userId}/impersonate`, { reason })
    } finally {
      invalidateAntiforgeryToken()
    }
  },

  /** Ends the support session: back to the administrator's own account. */
  async stopImpersonation(): Promise<void> {
    try {
      await api.post<void>('/api/auth/impersonation/stop')
    } finally {
      invalidateAntiforgeryToken()
    }
  },
}

export const accountApi = {
  profile: (signal?: AbortSignal) => api.get<AccountProfile>('/api/account/profile', signal),
  updateProfile: (body: UpdateProfileRequest) => api.put<AccountProfile>('/api/account/profile', body),
  /** Saves the preferred UI language (null clears it). */
  updateLanguage: (language: string | null) => api.put<AccountProfile>('/api/account/language', { language }),
  changePassword: (body: ChangePasswordRequest) => api.post<void>('/api/account/change-password', body),
  uploadAvatar: (image: Blob) => api.put<void>('/api/account/avatar', avatarForm(image)),
  removeAvatar: () => api.delete<void>('/api/account/avatar'),

  twoFactor: (signal?: AbortSignal) => api.get<TwoFactorStatus>('/api/account/two-factor', signal),
  /** The key to scan; the same key every time until two-factor is turned on. */
  setupTwoFactor: () => api.post<TwoFactorSetup>('/api/account/two-factor/setup'),
  enableTwoFactor: (code: string) => api.post<RecoveryCodes>('/api/account/two-factor/enable', { code }),
  disableTwoFactor: (password: string) => api.post<void>('/api/account/two-factor/disable', { password }),
  regenerateRecoveryCodes: (password: string) =>
    api.post<RecoveryCodes>('/api/account/two-factor/recovery-codes', { password }),

  /** A ZIP with everything the portal holds about the caller. */
  exportData: (password: string) => api.postForFile('/api/account/data-export', { password }),
  /** Schedules the deletion of the caller's account and signs them out (the antiforgery token goes with the identity). */
  async requestDeletion(password: string): Promise<AccountDeletion> {
    try {
      return await api.post<AccountDeletion>('/api/account/deletion', { password })
    } finally {
      invalidateAntiforgeryToken()
    }
  },
  cancelDeletion: () => api.delete<void>('/api/account/deletion'),

  tokens: (signal?: AbortSignal) => api.get<PersonalAccessToken[]>('/api/account/tokens', signal),
  createToken: (body: { name: string; scopes: PersonalAccessTokenScope[]; lifetimeDays: number }) =>
    api.post<CreatedPersonalAccessToken>('/api/account/tokens', body),
  revokeToken: (tokenId: string) => api.delete<void>(`/api/account/tokens/${tokenId}`),
}

/** The error code the password step answers with when the account still needs its second factor. */
export const TWO_FACTOR_REQUIRED = 'identity.two_factor_required'

/** The pending sign-in expired between the two steps; the password has to be entered again. */
export const TWO_FACTOR_SESSION_EXPIRED = 'identity.two_factor_session_expired'