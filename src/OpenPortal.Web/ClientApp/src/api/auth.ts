import { api, invalidateAntiforgeryToken } from './client'
import type {
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
}

/** The error code the password step answers with when the account still needs its second factor. */
export const TWO_FACTOR_REQUIRED = 'identity.two_factor_required'

/** The pending sign-in expired between the two steps; the password has to be entered again. */
export const TWO_FACTOR_SESSION_EXPIRED = 'identity.two_factor_session_expired'