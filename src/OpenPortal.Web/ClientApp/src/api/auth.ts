import { api, invalidateAntiforgeryToken } from './client'
import type { AccountProfile, ChangePasswordRequest, Session, UpdateProfileRequest } from './types'

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
}